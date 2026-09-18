using CrmRegistrationGateway.Models;

namespace CrmRegistrationGateway.Infrastructure;

public sealed class CrmAuthenticationException : Exception
{
    public CrmAuthenticationDiagnostic? Diagnostic { get; }

    public CrmAuthenticationException(string message, Exception innerException,
        CrmAuthenticationDiagnostic? diagnostic = null)
        : base(message, innerException)
    {
        Diagnostic = diagnostic;
    }
}
