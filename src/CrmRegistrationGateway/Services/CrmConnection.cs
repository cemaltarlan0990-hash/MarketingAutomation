using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;
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
            using (var client = new CrmServiceClient(options.BuildConnectionString()))
            {
                if (!client.IsReady)
                {
                    throw new InvalidOperationException(
                        "CRM connection could not be established.",
                        client.LastCrmException);
                }

                // The client itself implements IOrganizationService. The operation
                // completes before the client is disposed, so no disposed proxy escapes.
                return operation(client);
            }
        }
    }
}
