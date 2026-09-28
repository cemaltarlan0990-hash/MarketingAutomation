using System;
using Microsoft.Xrm.Sdk;
using RelatedEntegrasyonu.CrmGateway.Configuration;

namespace RelatedEntegrasyonu.CrmGateway.Services
{
    internal static class CrmConnection
    {
        public static TResult Execute<TResult>(
            CrmOptions options,
            Func<IOrganizationService, TResult> operation)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            if (operation == null)
                throw new ArgumentNullException("operation");

            options.ValidateForWrite();
            return ExecuteConnected(options, operation);
        }

        public static TResult ExecuteReadOnly<TResult>(
            CrmOptions options,
            Func<IOrganizationService, TResult> operation)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            if (operation == null)
                throw new ArgumentNullException("operation");

            options.ValidateForConnection();
            return ExecuteConnected(options, operation);
        }

        private static TResult ExecuteConnected<TResult>(
            CrmOptions options,
            Func<IOrganizationService, TResult> operation)
        {
            return RelatedEntegrasyonu.AltiumService.GetService().Execute(operation);
        }
    }
}
