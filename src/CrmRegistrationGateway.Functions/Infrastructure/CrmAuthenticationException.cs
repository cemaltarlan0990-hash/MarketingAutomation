namespace CrmRegistrationGateway.Infrastructure;

public sealed class CrmAuthenticationException : Exception
{
    public CrmAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
