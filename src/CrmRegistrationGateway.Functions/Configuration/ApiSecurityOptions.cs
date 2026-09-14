using Microsoft.Extensions.Configuration;

namespace CrmRegistrationGateway.Configuration;

public sealed class ApiSecurityOptions
{
    public const string HeaderName = "X-Integration-Key";

    public string InboundApiKey { get; init; } = string.Empty;

    public static ApiSecurityOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new ApiSecurityOptions
        {
            InboundApiKey = (configuration["INBOUND_API_KEY"] ??
                             configuration["ApiSecurity:InboundApiKey"] ??
                             string.Empty).Trim()
        };
    }

    public void Validate()
    {
        if (InboundApiKey.Length < 32)
        {
            throw new InvalidOperationException("INBOUND_API_KEY must contain at least 32 characters.");
        }
    }
}
