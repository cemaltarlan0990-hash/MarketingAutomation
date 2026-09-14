using System.Net;

namespace CrmRegistrationGateway.Infrastructure;

public sealed class CrmApiException : Exception
{
    public CrmApiException(
        string message,
        HttpStatusCode statusCode,
        string? crmErrorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        CrmErrorCode = crmErrorCode;
    }

    public HttpStatusCode StatusCode { get; }
    public string? CrmErrorCode { get; }
}
