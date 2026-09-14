using CrmRegistrationGateway.Models;
using CrmRegistrationGateway.Validation;

namespace CrmRegistrationGateway.Functions.Tests.Validation;

public sealed class EventRegistrationValidatorTests
{
    private readonly EventRegistrationValidator _validator = new();

    [Fact]
    public void Validate_WhenRequiredFieldsAreMissing_ReturnsFieldErrors()
    {
        var result = _validator.Validate(new EventRegistrationRequest());

        Assert.False(result.IsValid);
        Assert.Contains("firstName", result.Errors.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("lastName", result.Errors.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("email", result.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    [InlineData("a b@example.com")]
    public void Validate_WhenEmailIsInvalid_ReturnsEmailError(string email)
    {
        var request = ValidRequest(email);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("email", result.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WhenRequestIsValid_ReturnsSuccess()
    {
        var result = _validator.Validate(ValidRequest("cemal@example.com"));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenOptionalFieldIsTooLong_ReturnsFieldError()
    {
        var request = new EventRegistrationRequest
        {
            FirstName = "Cemal",
            LastName = "Tarlan",
            Email = "cemal@example.com",
            EventName = new string('x', 201)
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("eventName", result.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    private static EventRegistrationRequest ValidRequest(string email) =>
        new()
        {
            FirstName = "Cemal",
            LastName = "Tarlan",
            Email = email,
            WorkPhone = "+902120000000",
            MobilePhone = "+905551234567",
            Company = "ABC Teknoloji",
            JobTitle = "Computer Engineer",
            Department = "Ar-Ge",
            City = "İstanbul",
            EventName = "Altium Day 2026"
        };
}
