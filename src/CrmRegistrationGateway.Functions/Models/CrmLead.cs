namespace CrmRegistrationGateway.Models;

public sealed record CrmLead(
    string FirstName,
    string LastName,
    string Email,
    string? Subject,
    string? CompanyName,
    string? Department,
    string? JobTitle,
    string? City,
    string? WorkPhone,
    string? MobilePhone)
{
    public static CrmLead FromRequest(EventRegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CrmLead(
            request.FirstName!.Trim(),
            request.LastName!.Trim(),
            request.Email!.Trim().ToLowerInvariant(),
            NormalizeOptional(request.EventName),
            NormalizeOptional(request.Company),
            NormalizeOptional(request.Department),
            NormalizeOptional(request.JobTitle),
            NormalizeOptional(request.City),
            NormalizeOptional(request.WorkPhone),
            NormalizeOptional(request.MobilePhone) ?? NormalizeOptional(request.Phone));
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
