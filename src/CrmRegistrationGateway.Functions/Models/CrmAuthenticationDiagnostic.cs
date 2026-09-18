using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Identity.Client;

namespace CrmRegistrationGateway.Models;

public sealed class CrmAuthenticationDiagnostic
{
    [JsonPropertyName("errorCode")]
    public required string ErrorCode { get; init; }

    [JsonPropertyName("identityCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdentityCode { get; init; }

    [JsonPropertyName("tokenHttpStatus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TokenHttpStatus { get; init; }

    [JsonPropertyName("recommendedAction")]
    public required string RecommendedAction { get; init; }

    public static CrmAuthenticationDiagnostic FromException(MsalException exception)
    {
        var identityCode = ReadIdentityCode(exception);
        // Only structured codes are exposed. Never return MSAL messages, bodies or inner exceptions.
        var errorCode = Regex.IsMatch(exception.ErrorCode ?? string.Empty, @"\A[a-zA-Z0-9_.-]{1,80}\z")
            ? exception.ErrorCode!
            : "unrecognized_identity_error";
        return new CrmAuthenticationDiagnostic
        {
            ErrorCode = errorCode,
            IdentityCode = identityCode,
            TokenHttpStatus = exception is MsalServiceException { StatusCode: >= 400 and <= 599 } service
                ? service.StatusCode : null,
            RecommendedAction = identityCode switch
            {
                "AADSTS7000215" => "CLIENT_SECRET gecersiz. Secret ID yerine Value kullanildigini ve secret'in CLIENT_ID ile ayni uygulamaya ait oldugunu kontrol edin.",
                "AADSTS7000222" => "CLIENT_SECRET suresi dolmus. Yetkili yoneticiden gecerli bir secret Value alin ve Azure CLIENT_SECRET ayarini guncelleyin.",
                "AADSTS700016" => "Uygulama belirtilen tenant'ta bulunamadi. CLIENT_ID ve TENANT_ID degerlerinin ayni App Registration'a ait oldugunu kontrol edin.",
                _ => "Token alinamadi. Bu hata koduyla CRM yoneticinizle CLIENT_ID, TENANT_ID, CLIENT_SECRET ve CRM_URL ayarlarini kontrol edin."
            }
        };
    }

    private static string? ReadIdentityCode(MsalException exception)
    {
        if (exception is MsalServiceException { ResponseBody: not null } service)
        {
            try
            {
                using var document = JsonDocument.Parse(service.ResponseBody);
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("error_codes", out var codes) &&
                    codes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var code in codes.EnumerateArray())
                    {
                        if (code.ValueKind == JsonValueKind.Number && code.TryGetInt64(out var number) &&
                            number is > 0 and <= 999_999_999)
                        {
                            return $"AADSTS{number}";
                        }
                    }
                }
            }
            catch (JsonException) { }
        }
        var match = Regex.Match(exception.Message, @"\bAADSTS\d{1,9}\b");
        return match.Success ? match.Value : null;
    }
}
