using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using RelatedEntegrasyonu.CrmGateway.Configuration;
using RelatedEntegrasyonu.CrmGateway.Models;

namespace RelatedEntegrasyonu.CrmGateway.Services
{
    internal sealed class EtkinlikKaydiResult
    {
        public EtkinlikKaydiResult(Guid crmId, bool created)
        {
            CrmId = crmId;
            Created = created;
        }

        public Guid CrmId { get; private set; }
        public bool Created { get; private set; }
    }

    /// <summary>
    /// Etkinlik kaydını Lead olarak yazar. Ad, soyad, e-posta, telefon ve firma
    /// mevcut web formu servisiyle aynı eşlemeleri kullanır. Konu, açıklama ve
    /// toplu e-posta izni standart Lead alanlarıdır. Ünvan ve şehir yalnızca
    /// ilgili ayarlar tanımlıysa ayrı alanlara yazılır; değilse açıklamaya eklenir.
    /// </summary>
    internal static class EtkinlikKaydiWriter
    {
        private const string SubjectAttribute = "subject";
        private const string DescriptionAttribute = "description";
        private const string DoNotBulkEmailAttribute = "donotbulkemail";
        private const string StateCodeAttribute = "statecode";
        private const int OpenStateCode = 0;

        public static EtkinlikKaydiResult Write(
            IOrganizationService service,
            CrmOptions options,
            EtkinlikKaydiRequest request)
        {
            if (service == null)
                throw new ArgumentNullException("service");
            if (options == null)
                throw new ArgumentNullException("options");
            if (request == null)
                throw new ArgumentNullException("request");

            // Aynı mail ikinci kez işlenirse ikinci bir Lead açılmasın.
            Guid? existingId = FindOpenRegistration(service, options, request);
            if (existingId.HasValue)
                return new EtkinlikKaydiResult(existingId.Value, false);

            Guid? cityId = FindCity(service, options, request.CityForLookup);

            var record = new Entity(options.TargetEntity);
            record[options.FirstNameAttribute] = request.FirstName;
            record[options.LastNameAttribute] = request.LastName;
            record[options.EmailAttribute] = request.Email;
            SetWhenMapped(record, options.PhoneAttribute, request.Phone);
            SetWhenMapped(record, options.CompanyAttribute, request.Company);
            SetWhenMapped(record, options.JobTitleAttribute, request.JobTitle);

            record[SubjectAttribute] = request.Subject;
            record[DoNotBulkEmailAttribute] = !request.MarketingConsent;
            if (cityId.HasValue)
                record[options.CityLookupAttribute] = new EntityReference(options.CityEntity, cityId.Value);
            record[DescriptionAttribute] = request.BuildDescription(cityId.HasValue);

            return new EtkinlikKaydiResult(service.Create(record), true);
        }

        private static Guid? FindOpenRegistration(
            IOrganizationService service,
            CrmOptions options,
            EtkinlikKaydiRequest request)
        {
            var query = new QueryExpression(options.TargetEntity)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 1
            };
            query.Criteria.AddCondition(options.EmailAttribute, ConditionOperator.Equal, request.Email);
            query.Criteria.AddCondition(SubjectAttribute, ConditionOperator.Equal, request.Subject);
            query.Criteria.AddCondition(StateCodeAttribute, ConditionOperator.Equal, OpenStateCode);

            EntityCollection matches = service.RetrieveMultiple(query);
            return matches.Entities.Count > 0 ? matches.Entities[0].Id : (Guid?)null;
        }

        // Şehir eşlemesi isteğe bağlıdır: CRM_CITY_* ayarları tanımlı değilse
        // hiç sorgu yapılmaz ve şehir açıklamaya yazılır.
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
