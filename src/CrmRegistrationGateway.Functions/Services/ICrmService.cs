using CrmRegistrationGateway.Models;

namespace CrmRegistrationGateway.Services;

public interface ICrmService
{
    Task<CrmOperationResult> CreateLeadIfNotExistsAsync(
        CrmLead lead,
        CancellationToken cancellationToken);
}
