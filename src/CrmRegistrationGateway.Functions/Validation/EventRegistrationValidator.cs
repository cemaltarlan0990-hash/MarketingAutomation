using System.Net.Mail;
using CrmRegistrationGateway.Models;

namespace CrmRegistrationGateway.Validation;

public sealed class EventRegistrationValidator
{
    private static readonly IReadOnlyDictionary<string, int> MaximumLengths =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(EventRegistrationRequest.FirstName)] = 50,
            [nameof(EventRegistrationRequest.LastName)] = 50,
            [nameof(EventRegistrationRequest.Email)] = 254,
            [nameof(EventRegistrationRequest.Phone)] = 50,
            [nameof(EventRegistrationRequest.WorkPhone)] = 50,
            [nameof(EventRegistrationRequest.MobilePhone)] = 50,
            [nameof(EventRegistrationRequest.Company)] = 160,
            [nameof(EventRegistrationRequest.JobTitle)] = 100,
            [nameof(EventRegistrationRequest.Department)] = 100,
            [nameof(EventRegistrationRequest.City)] = 100,
            [nameof(EventRegistrationRequest.EventName)] = 200
        };

    public ValidationResult Validate(EventRegistrationRequest? request)
    {
        var result = new ValidationResult();
        if (request is null)
        {
            result.Add("body", "Request body is required.");
            return result;
        }

        Require(result, "firstName", request.FirstName);
        Require(result, "lastName", request.LastName);
        Require(result, "email", request.Email);

        CheckLength(result, "firstName", request.FirstName, MaximumLengths[nameof(request.FirstName)]);
        CheckLength(result, "lastName", request.LastName, MaximumLengths[nameof(request.LastName)]);
        CheckLength(result, "email", request.Email, MaximumLengths[nameof(request.Email)]);
        CheckLength(result, "phone", request.Phone, MaximumLengths[nameof(request.Phone)]);
        CheckLength(result, "workPhone", request.WorkPhone, MaximumLengths[nameof(request.WorkPhone)]);
        CheckLength(result, "mobilePhone", request.MobilePhone, MaximumLengths[nameof(request.MobilePhone)]);
        CheckLength(result, "company", request.Company, MaximumLengths[nameof(request.Company)]);
        CheckLength(result, "jobTitle", request.JobTitle, MaximumLengths[nameof(request.JobTitle)]);
        CheckLength(result, "department", request.Department, MaximumLengths[nameof(request.Department)]);
        CheckLength(result, "city", request.City, MaximumLengths[nameof(request.City)]);
        CheckLength(result, "eventName", request.EventName, MaximumLengths[nameof(request.EventName)]);

        if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email.Trim()))
        {
            result.Add("email", "A valid email address is required.");
        }

        return result;
    }

    private static void Require(ValidationResult result, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Add(field, $"{field} is required.");
        }
    }

    private static void CheckLength(
        ValidationResult result,
        string field,
        string? value,
        int maximumLength)
    {
        if (value?.Trim().Length > maximumLength)
        {
            result.Add(field, $"{field} must be at most {maximumLength} characters.");
        }
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            var parsed = new MailAddress(value);
            return string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
