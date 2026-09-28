using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;
using System;
using RelatedEntegrasyonu.CrmGateway.Configuration;

namespace RelatedEntegrasyonu
{
    internal class AltiumService
    {
        private static volatile AltiumService _serviceInstance;
        private IOrganizationService _organizationService;
        private Guid _controlID;
        private static object _lockObject = new object();
        private readonly object _operationLock = new object();
        private CrmServiceClient _crmClient;

        public IOrganizationService OrganizationService
        {
            get { return _organizationService; }
        }
        public Guid ControlID
        {
            get { return _controlID; }
        }

        private AltiumService(Guid controlID)
        {
            CrmOptions options = CrmOptions.Load();
            options.ValidateForConnection();
            string conn = options.BuildConnectionString();
            var service = new CrmServiceClient(conn);
            if (!service.IsReady)
            {
                string error = service.LastCrmError;
                Exception cause = service.LastCrmException;
                service.Dispose();
                throw new InvalidOperationException(
                    "CRM bağlantısı başarısız: " + error, cause);
            }

            // CrmServiceClient implements IOrganizationService. The singleton keeps
            // it alive so later requests do not use a disposed web proxy.
            _crmClient = service;
            _organizationService = _crmClient;
            _controlID = controlID;
        }

        public static AltiumService GetService()
        {
            if (_serviceInstance == null)
            {
                lock (_lockObject)
                {
                    if (_serviceInstance == null)
                        _serviceInstance = new AltiumService(Guid.NewGuid());
                }
            }
            return _serviceInstance;
        }

        public TResult Execute<TResult>(Func<IOrganizationService, TResult> operation)
        {
            if (operation == null)
                throw new ArgumentNullException("operation");

            lock (_operationLock)
            {
                return operation(_organizationService);
            }
        }
    }
}
