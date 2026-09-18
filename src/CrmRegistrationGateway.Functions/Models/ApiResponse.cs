using System.Text.Json.Serialization;

namespace CrmRegistrationGateway.Models;

public sealed class ApiResponse
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("crmId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CrmId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    [JsonPropertyName("authentication")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CrmAuthenticationDiagnostic? Authentication { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }
}
