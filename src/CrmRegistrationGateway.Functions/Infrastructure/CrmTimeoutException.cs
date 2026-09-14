namespace CrmRegistrationGateway.Infrastructure;

public sealed class CrmTimeoutException : TimeoutException
{
    public CrmTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
