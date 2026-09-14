using Microsoft.Extensions.Configuration;

namespace CrmRegistrationGateway.Configuration;

public sealed class CrmOptions
{
    public string Url { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = "v9.2";
    public int RequestTimeoutSeconds { get; init; } = 30;

    public static CrmOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new CrmOptions
        {
            Url = Read(configuration, "CRM_URL", "Crm:Url"),
            TenantId = Read(configuration, "TENANT_ID", "Crm:TenantId"),
            ClientId = Read(configuration, "CLIENT_ID", "Crm:ClientId"),
            ClientSecret = Read(configuration, "CLIENT_SECRET", "Crm:ClientSecret"),
            ApiVersion = Read(configuration, "CRM_API_VERSION", "Crm:ApiVersion", "v9.2"),
            RequestTimeoutSeconds = ReadInt(
                configuration,
                "CRM_REQUEST_TIMEOUT_SECONDS",
                "Crm:RequestTimeoutSeconds",
                30)
        };
    }

    public void Validate()
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("CRM_URL must be a valid HTTPS URL.");
        }

        if (!Guid.TryParse(TenantId, out _))
        {
            throw new InvalidOperationException("TENANT_ID must be a valid GUID.");
        }

        if (!Guid.TryParse(ClientId, out _))
        {
            throw new InvalidOperationException("CLIENT_ID must be a valid GUID.");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException("CLIENT_SECRET is required.");
        }

        if (!ApiVersion.StartsWith('v') || ApiVersion.Any(character =>
                !char.IsDigit(character) && character is not 'v' and not '.'))
        {
            throw new InvalidOperationException("CRM_API_VERSION is invalid.");
        }

        if (RequestTimeoutSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException("CRM_REQUEST_TIMEOUT_SECONDS must be between 1 and 300.");
        }
    }

    public Uri GetApiBaseUri()
    {
        var root = Url.TrimEnd('/');
        return new Uri($"{root}/api/data/{ApiVersion}/", UriKind.Absolute);
    }

    public string GetDataverseScope() => $"{Url.TrimEnd('/')}/.default";

    private static string Read(
        IConfiguration configuration,
        string environmentName,
        string sectionName,
        string defaultValue = "") =>
        (configuration[environmentName] ?? configuration[sectionName] ?? defaultValue).Trim();

    private static int ReadInt(
        IConfiguration configuration,
        string environmentName,
        string sectionName,
        int defaultValue) =>
        int.TryParse(Read(configuration, environmentName, sectionName), out var value)
            ? value
            : defaultValue;
}
