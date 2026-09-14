using System;
using Microsoft.Xrm.Sdk;
using RelatedEntegrasyonu.CrmGateway.Configuration;
using RelatedEntegrasyonu.CrmGateway.Models;

namespace RelatedEntegrasyonu.CrmGateway.Services
{
    internal static class CrmRegistrationWriter
    {
        public static Guid Write(
            IOrganizationService service,
            CrmOptions options,
            CrmRegistrationRequest request)
        {
            if (service == null)
                throw new ArgumentNullException("service");
            if (options == null)
                throw new ArgumentNullException("options");
            if (request == null)
                throw new ArgumentNullException("request");

            var record = new Entity(options.TargetEntity);
            record[options.FirstNameAttribute] = request.FirstName;
            record[options.LastNameAttribute] = request.LastName;
            record[options.EmailAttribute] = request.Email;
            SetWhenMapped(record, options.PhoneAttribute, request.Phone);
            SetWhenMapped(record, options.CompanyAttribute, request.Company);

            return service.Create(record);
        }

        private static void SetWhenMapped(Entity entity, string attribute, string value)
        {
            if (!string.IsNullOrWhiteSpace(attribute) && !string.IsNullOrWhiteSpace(value))
                entity[attribute] = value;
        }
    }
}
