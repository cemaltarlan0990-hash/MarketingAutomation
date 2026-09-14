using CrmRegistrationGateway.Configuration;

namespace CrmRegistrationGateway.Functions.Tests.Configuration;

public sealed class ApiSecurityOptionsTests
{
    [Fact]
    public void Validate_WhenKeyIsTooShort_Throws()
    {
        var options = new ApiSecurityOptions { InboundApiKey = "short-key" };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("32", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenKeyIsLongEnough_Succeeds()
    {
        var options = new ApiSecurityOptions
        {
            InboundApiKey = "a-secure-random-key-with-32-characters"
        };

        options.Validate();
    }
}
