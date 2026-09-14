using CrmRegistrationGateway.Models;

namespace CrmRegistrationGateway.Services;

public sealed class LeadRegistrationProcessor
{
    private readonly ICrmService _crmService;

    public LeadRegistrationProcessor(ICrmService crmService)
    {
        _crmService = crmService ?? throw new ArgumentNullException(nameof(crmService));
    }

    public Task<CrmOperationResult> ProcessAsync(
        EventRegistrationRequest request,
        CancellationToken cancellationToken) =>
        _crmService.CreateLeadIfNotExistsAsync(
            CrmLead.FromRequest(request),
            cancellationToken);
}
