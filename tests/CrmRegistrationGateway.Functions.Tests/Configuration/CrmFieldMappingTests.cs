using CrmRegistrationGateway.Configuration;

namespace CrmRegistrationGateway.Functions.Tests.Configuration;

public sealed class CrmFieldMappingTests
{
    [Fact]
    public void Defaults_MatchVerifiedLeadLogicalNames()
    {
        var mapping = new CrmFieldMapping();

        Assert.Equal("leads", mapping.LeadEntitySet);
        Assert.Equal("leadid", mapping.LeadId);
        Assert.Equal("subject", mapping.Subject);
        Assert.Equal("companyname", mapping.CompanyName);
        Assert.Equal("firstname", mapping.FirstName);
        Assert.Equal("lastname", mapping.LastName);
        Assert.Equal("emailaddress1", mapping.Email);
        Assert.Equal("twbs_department", mapping.Department);
        Assert.Equal("twbs_isunvani", mapping.JobTitle);
        Assert.Equal("twbs_sehir", mapping.City);
        Assert.Equal("telephone1", mapping.WorkPhone);
        Assert.Equal("mobilephone", mapping.MobilePhone);
    }
}
