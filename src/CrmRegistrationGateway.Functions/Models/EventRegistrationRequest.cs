using System.Text.Json.Serialization;

namespace CrmRegistrationGateway.Models;

public sealed class EventRegistrationRequest
{
    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("workPhone")]
    public string? WorkPhone { get; init; }

    [JsonPropertyName("mobilePhone")]
    public string? MobilePhone { get; init; }

    [JsonPropertyName("company")]
    public string? Company { get; init; }

    [JsonPropertyName("jobTitle")]
    public string? JobTitle { get; init; }

    [JsonPropertyName("department")]
    public string? Department { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("eventName")]
    public string? EventName { get; init; }
}
