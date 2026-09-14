using CrmRegistrationGateway.Models;
using CrmRegistrationGateway.Services;
using NSubstitute;

namespace CrmRegistrationGateway.Functions.Tests.Services;

public sealed class LeadRegistrationProcessorTests
{
    [Fact]
    public async Task ProcessAsync_UsesMockedCrmServiceAndMapsLeadFields()
    {
        var crmService = Substitute.For<ICrmService>();
        var expected = CrmOperationResult.AlreadyExists(Guid.NewGuid());
        crmService
            .CreateLeadIfNotExistsAsync(Arg.Any<CrmLead>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var processor = new LeadRegistrationProcessor(crmService);
        var request = new EventRegistrationRequest
        {
            FirstName = " Cemal ",
            LastName = " Tarlan ",
            Email = " CEMAL@example.com ",
            Company = " ABC Teknoloji ",
            EventName = " Altium Day 2026 ",
            Department = " Ar-Ge ",
            JobTitle = " Bilgisayar Mühendisi ",
            City = " İstanbul ",
            WorkPhone = " +90 212 000 00 00 ",
            MobilePhone = " +90 555 000 00 00 "
        };

        var actual = await processor.ProcessAsync(request, CancellationToken.None);

        Assert.Equal(expected, actual);
        await crmService.Received(1).CreateLeadIfNotExistsAsync(
            Arg.Is<CrmLead>(lead =>
                lead.FirstName == "Cemal" &&
                lead.LastName == "Tarlan" &&
                lead.Email == "cemal@example.com" &&
                lead.CompanyName == "ABC Teknoloji" &&
                lead.Subject == "Altium Day 2026" &&
                lead.Department == "Ar-Ge" &&
                lead.JobTitle == "Bilgisayar Mühendisi" &&
                lead.City == "İstanbul" &&
                lead.WorkPhone == "+90 212 000 00 00" &&
                lead.MobilePhone == "+90 555 000 00 00"),
            CancellationToken.None);
    }

    [Fact]
    public async Task ProcessAsync_UsesLegacyPhoneAsMobilePhoneWhenMobilePhoneIsMissing()
    {
        var crmService = Substitute.For<ICrmService>();
        crmService
            .CreateLeadIfNotExistsAsync(Arg.Any<CrmLead>(), Arg.Any<CancellationToken>())
            .Returns(CrmOperationResult.Created(Guid.NewGuid()));
        var processor = new LeadRegistrationProcessor(crmService);
        var request = new EventRegistrationRequest
        {
            FirstName = "Cemal",
            LastName = "Tarlan",
            Email = "cemal@example.com",
            Phone = "+90 555 123 45 67"
        };

        await processor.ProcessAsync(request, CancellationToken.None);

        await crmService.Received(1).CreateLeadIfNotExistsAsync(
            Arg.Is<CrmLead>(lead => lead.MobilePhone == "+90 555 123 45 67"),
            CancellationToken.None);
    }
}
