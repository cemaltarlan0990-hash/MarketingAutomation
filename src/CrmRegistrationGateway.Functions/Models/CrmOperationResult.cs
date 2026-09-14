namespace CrmRegistrationGateway.Models;

public enum CrmOperationStatus
{
    Created,
    AlreadyExists
}

public sealed record CrmOperationResult(CrmOperationStatus Status, Guid CrmId)
{
    public static CrmOperationResult Created(Guid crmId) =>
        new(CrmOperationStatus.Created, crmId);

    public static CrmOperationResult AlreadyExists(Guid crmId) =>
        new(CrmOperationStatus.AlreadyExists, crmId);
}
