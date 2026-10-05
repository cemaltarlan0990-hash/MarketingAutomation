using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
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
            SetWhenMapped(record, "subject", request.Subject);
            SetWhenMapped(record, "description", request.Description);

            // Profile fields are written only when their CRM columns are configured;
            // the description keeps the same values as an audit trail either way.
            SetWhenMapped(record, options.JobTitleAttribute, request.JobTitle);
            if (request.KvkkConsent == true && !string.IsNullOrWhiteSpace(options.KvkkConsentAttribute))
                record[options.KvkkConsentAttribute] = true;
            Guid? cityId = FindCity(service, options, request.City);
            if (cityId.HasValue)
                record[options.CityLookupAttribute] = new EntityReference(options.CityEntity, cityId.Value);

            return service.Create(record);
        }

        // An unknown city leaves the lookup empty instead of failing the registration.
        private static Guid? FindCity(IOrganizationService service, CrmOptions options, string cityName)
        {
            if (!options.HasCityLookup || string.IsNullOrWhiteSpace(cityName))
                return null;

            var query = new QueryExpression(options.CityEntity)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 1
            };
            query.Criteria.AddCondition(options.CityNameAttribute, ConditionOperator.Equal, cityName);

            EntityCollection matches = service.RetrieveMultiple(query);
            return matches.Entities.Count > 0 ? matches.Entities[0].Id : (Guid?)null;
        }

        private static void SetWhenMapped(Entity entity, string attribute, string value)
        {
            if (!string.IsNullOrWhiteSpace(attribute) && !string.IsNullOrWhiteSpace(value))
                entity[attribute] = value;
        }
    }
}
