using System;

namespace RelatedEntegrasyonu.CrmGateway.Infrastructure
{
    internal sealed class RequestValidationException : Exception
    {
        public RequestValidationException(string message)
            : base(message)
        {
        }
    }
}

