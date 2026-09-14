using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrmRegistrationGateway.Configuration;
using CrmRegistrationGateway.Infrastructure;
using CrmRegistrationGateway.Models;
using Microsoft.Extensions.Logging;

namespace CrmRegistrationGateway.Services;

public sealed class CrmService : ICrmService
{
    public const string HttpClientName = "Dataverse";

    private const int MaximumErrorBodyLength = 16_384;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICrmAuthenticationService _authenticationService;
    private readonly CrmFieldMapping _mapping;
    private readonly ILogger<CrmService> _logger;

    public CrmService(
        IHttpClientFactory httpClientFactory,
        ICrmAuthenticationService authenticationService,
        CrmFieldMapping mapping,
        ILogger<CrmService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CrmOperationResult> CreateLeadIfNotExistsAsync(
        CrmLead lead,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lead);

        var emailFingerprint = CreateFingerprint(lead.Email);
        var existingId = await FindLeadIdByEmailAsync(lead.Email, cancellationToken)
            .ConfigureAwait(false);

        if (existingId.HasValue)
        {
            _logger.LogInformation(
                "Lead already exists. LeadId={LeadId}; EmailFingerprint={EmailFingerprint}",
                existingId.Value,
                emailFingerprint);
            return CrmOperationResult.AlreadyExists(existingId.Value);
        }

        _logger.LogInformation(
            "Lead not found. EmailFingerprint={EmailFingerprint}",
            emailFingerprint);

        try
        {
            var createdId = await CreateLeadAsync(lead, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Lead created. LeadId={LeadId}; EmailFingerprint={EmailFingerprint}",
                createdId,
                emailFingerprint);
            return CrmOperationResult.Created(createdId);
        }
        catch (CrmApiException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            // If a Dataverse alternate key or duplicate rule rejects a concurrent create,
            // resolve the winning record and return the operation as idempotent.
            existingId = await FindLeadIdByEmailAsync(lead.Email, cancellationToken)
                .ConfigureAwait(false);
            if (existingId.HasValue)
            {
                _logger.LogWarning(
                    "Concurrent duplicate detected. LeadId={LeadId}; EmailFingerprint={EmailFingerprint}",
                    existingId.Value,
                    emailFingerprint);
                return CrmOperationResult.AlreadyExists(existingId.Value);
            }

            throw;
        }
    }

    private async Task<Guid?> FindLeadIdByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var filter = $"{_mapping.Email} eq '{EscapeODataString(email)}'";
        var requestUri =
            $"{_mapping.LeadEntitySet}?$select={_mapping.LeadId}" +
            $"&$filter={Uri.EscapeDataString(filter)}&$top=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await SendAuthorizedAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Dataverse connection and lead lookup succeeded.");

        await using var responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(responseStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("value", out var values) ||
            values.ValueKind != JsonValueKind.Array ||
            values.GetArrayLength() == 0)
        {
            return null;
        }

        var idText = values[0].GetProperty(_mapping.LeadId).GetString();
        if (!Guid.TryParse(idText, out var id))
        {
            throw new CrmApiException(
                "Dataverse returned an invalid lead identifier.",
                HttpStatusCode.BadGateway);
        }

        return id;
    }

    private async Task<Guid> CreateLeadAsync(
        CrmLead lead,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [_mapping.FirstName] = lead.FirstName,
            [_mapping.LastName] = lead.LastName,
            [_mapping.Email] = lead.Email
        };

        AddOptional(payload, _mapping.Subject, lead.Subject);
        AddOptional(payload, _mapping.CompanyName, lead.CompanyName);
        AddOptional(payload, _mapping.Department, lead.Department);
        AddOptional(payload, _mapping.JobTitle, lead.JobTitle);
        AddOptional(payload, _mapping.City, lead.City);
        AddOptional(payload, _mapping.WorkPhone, lead.WorkPhone);
        AddOptional(payload, _mapping.MobilePhone, lead.MobilePhone);

        using var request = new HttpRequestMessage(HttpMethod.Post, _mapping.LeadEntitySet)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");

        using var response = await SendAuthorizedAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var idFromHeader = GetIdFromEntityHeader(response);
        if (idFromHeader.HasValue)
        {
            return idFromHeader.Value;
        }

        if (response.Content.Headers.ContentLength == 0)
        {
            throw new CrmApiException(
                "Dataverse did not return the created lead identifier.",
                HttpStatusCode.BadGateway);
        }

        await using var responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(responseStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (document.RootElement.TryGetProperty(_mapping.LeadId, out var idElement) &&
            Guid.TryParse(idElement.GetString(), out var id))
        {
            return id;
        }

        throw new CrmApiException(
            "Dataverse returned an invalid created lead identifier.",
            HttpStatusCode.BadGateway);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var accessToken = await _authenticationService
            .GetAccessTokenAsync(cancellationToken)
            .ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            return await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Dataverse request timed out.");
            throw new CrmTimeoutException("Dataverse request timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Dataverse network request failed.");
            throw new CrmApiException("Dataverse network request failed.", HttpStatusCode.BadGateway);
        }
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorCode = await ReadCrmErrorCodeAsync(response, cancellationToken).ConfigureAwait(false);
        _logger.LogError(
            "Dataverse API error. StatusCode={StatusCode}; CrmErrorCode={CrmErrorCode}",
            (int)response.StatusCode,
            errorCode ?? "unavailable");

        throw new CrmApiException(
            "Dataverse rejected the request.",
            response.StatusCode,
            errorCode);
    }

    private static async Task<string?> ReadCrmErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (body.Length > MaximumErrorBodyLength)
        {
            body = body[..MaximumErrorBodyLength];
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement
                .GetProperty("error")
                .GetProperty("code")
                .GetString();
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static Guid? GetIdFromEntityHeader(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("OData-EntityId", out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var openParenthesis = value.LastIndexOf('(');
        var closeParenthesis = value.LastIndexOf(')');
        return openParenthesis >= 0 && closeParenthesis > openParenthesis &&
               Guid.TryParse(value[(openParenthesis + 1)..closeParenthesis], out var id)
            ? id
            : null;
    }

    private static void AddOptional(
        IDictionary<string, object> payload,
        string? fieldName,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(fieldName) && !string.IsNullOrWhiteSpace(value))
        {
            payload[fieldName] = value;
        }
    }

    private static string EscapeODataString(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string CreateFingerprint(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
        return Convert.ToHexString(hash)[..12];
    }
}
