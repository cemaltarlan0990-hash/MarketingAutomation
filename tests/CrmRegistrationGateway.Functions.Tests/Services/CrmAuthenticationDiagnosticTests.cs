using System.Text.Json;
using CrmRegistrationGateway.Models;
using Microsoft.Identity.Client;

namespace CrmRegistrationGateway.Functions.Tests.Services;

public sealed class CrmAuthenticationDiagnosticTests
{
    [Theory]
    [InlineData("7000215", "CLIENT_SECRET gecersiz")]
    [InlineData("7000222", "CLIENT_SECRET suresi dolmus")]
    [InlineData("700016", "CLIENT_ID ve TENANT_ID")]
    public void StructuredIdentityCodesGiveRelevantActionsWithoutExposingTheBody(string code, string action)
    {
        var exception = new MsalServiceException("invalid_client", "Private exception message", 401)
        {
            ResponseBody = "{\"error_codes\":[" + code + "],\"error_description\":\"private-secret-and-token\"}"
        };
        var diagnostic = CrmAuthenticationDiagnostic.FromException(exception);
        Assert.Equal("AADSTS" + code, diagnostic.IdentityCode);
        Assert.Equal(401, diagnostic.TokenHttpStatus);
        Assert.Contains(action, diagnostic.RecommendedAction);
        Assert.DoesNotContain("private-secret-and-token", JsonSerializer.Serialize(diagnostic));
        Assert.DoesNotContain("Private exception message", JsonSerializer.Serialize(diagnostic));
    }

    [Fact]
    public void MalformedBodyFallsBackToOnlyTheNumericCodeInTheMessage()
    {
        var exception = new MsalServiceException("invalid_client", "AADSTS7000215: credential=sensitive-value")
        {
            ResponseBody = "not-json-sensitive-value"
        };
        var diagnostic = CrmAuthenticationDiagnostic.FromException(exception);
        Assert.Equal("AADSTS7000215", diagnostic.IdentityCode);
        Assert.DoesNotContain("sensitive-value", JsonSerializer.Serialize(diagnostic));
        Assert.Null(diagnostic.TokenHttpStatus);
    }

    [Fact]
    public void UnknownClientFailureDoesNotInventAnIdentityCodeOrExposeTheMessage()
    {
        var diagnostic = CrmAuthenticationDiagnostic.FromException(
            new MsalClientException("http_request_failed", "private-host-and-credential"));
        Assert.Equal("http_request_failed", diagnostic.ErrorCode);
        Assert.Null(diagnostic.IdentityCode);
        Assert.Null(diagnostic.TokenHttpStatus);
        Assert.DoesNotContain("private-host-and-credential", JsonSerializer.Serialize(diagnostic));
    }

    [Fact]
    public void InvalidErrorCodeIsReplacedWithAFixedValue()
    {
        var diagnostic = CrmAuthenticationDiagnostic.FromException(
            new MsalClientException("bad code private-credential", "private-message"));
        Assert.Equal("unrecognized_identity_error", diagnostic.ErrorCode);
        Assert.DoesNotContain("private-credential", JsonSerializer.Serialize(diagnostic));
    }

    [Fact]
    public void AuthenticationDiagnosticsAreOptionalAndOnlySafeFieldsAreSerialized()
    {
        var diagnostic = CrmAuthenticationDiagnostic.FromException(
            new MsalServiceException("invalid_client", "AADSTS7000222: secret=private-value"));
        var response = new ApiResponse
        {
            Success = false, Status = "authentication_error", Message = "CRM authentication failed.",
            CorrelationId = "test-correlation", Authentication = diagnostic
        };
        var json = JsonSerializer.Serialize(response);
        Assert.Contains("\"authentication\"", json);
        Assert.Contains("AADSTS7000222", json);
        Assert.DoesNotContain("private-value", json);
        Assert.DoesNotContain("innerException", json);

        var success = new ApiResponse
        {
            Success = true, Status = "created", Message = "Created", CorrelationId = "test"
        };
        Assert.DoesNotContain("authentication", JsonSerializer.Serialize(success));
    }
}
