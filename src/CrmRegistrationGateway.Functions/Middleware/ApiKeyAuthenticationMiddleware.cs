using System.Net;
using System.Security.Cryptography;
using System.Text;
using CrmRegistrationGateway.Configuration;
using CrmRegistrationGateway.Models;

namespace CrmRegistrationGateway.Middleware;

public sealed class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly byte[] _expectedKeyHash;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ApiSecurityOptions securityOptions,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        ArgumentNullException.ThrowIfNull(securityOptions);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _expectedKeyHash = Hash(securityOptions.InboundApiKey);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var submittedKey = context.Request.Headers[ApiSecurityOptions.HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(submittedKey) ||
            !CryptographicOperations.FixedTimeEquals(_expectedKeyHash, Hash(submittedKey)))
        {
            _logger.LogWarning(
                "Unauthorized API request rejected. CorrelationId={CorrelationId}",
                context.TraceIdentifier);
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsJsonAsync(
                new ApiResponse
                {
                    Success = false,
                    Status = "unauthorized",
                    Message = "Unauthorized request.",
                    CorrelationId = context.TraceIdentifier
                },
                context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static byte[] Hash(string value) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
