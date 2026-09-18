using CrmRegistrationGateway.Configuration;
using CrmRegistrationGateway.Infrastructure;
using CrmRegistrationGateway.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace CrmRegistrationGateway.Services;

public sealed class CrmAuthenticationService : ICrmAuthenticationService
{
    private readonly IConfidentialClientApplication _confidentialClient;
    private readonly string[] _scopes;
    private readonly ILogger<CrmAuthenticationService> _logger;

    public CrmAuthenticationService(
        CrmOptions options,
        ILogger<CrmAuthenticationService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _confidentialClient = ConfidentialClientApplicationBuilder
            .Create(options.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, options.TenantId)
            .WithClientSecret(options.ClientSecret)
            .Build();

        _scopes = [options.GetDataverseScope()];
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            // MSAL's application token cache prevents a token request for every CRM call.
            var result = await _confidentialClient
                .AcquireTokenForClient(_scopes)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Dataverse authentication succeeded. TokenExpiresOn={TokenExpiresOn}",
                result.ExpiresOn);

            return result.AccessToken;
        }
        catch (MsalServiceException exception)
        {
            var diagnostic = CrmAuthenticationDiagnostic.FromException(exception);
            _logger.LogError(
                "Dataverse authentication service error. ErrorCode={ErrorCode}; StatusCode={StatusCode}; IdentityCode={IdentityCode}",
                diagnostic.ErrorCode,
                diagnostic.TokenHttpStatus,
                diagnostic.IdentityCode ?? "unavailable");
            throw new CrmAuthenticationException("Dataverse authentication failed.", exception, diagnostic);
        }
        catch (MsalClientException exception)
        {
            var diagnostic = CrmAuthenticationDiagnostic.FromException(exception);
            _logger.LogError(
                "Dataverse authentication client error. ErrorCode={ErrorCode}",
                diagnostic.ErrorCode);
            throw new CrmAuthenticationException("Dataverse authentication failed.", exception, diagnostic);
        }
    }
}
