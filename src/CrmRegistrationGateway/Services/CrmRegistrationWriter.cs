using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using RelatedEntegrasyonu.CrmGateway.Configuration;
using RelatedEntegrasyonu.CrmGateway.Models;

namespace RelatedEntegrasyonu.CrmGateway.Services
{
    internal static class CrmRegistrationWriter
    {
        private const int TurkishLanguageCode = 1055;
        private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

        // Choice labels rarely change; cached per process ("entity.attribute" → label → value).
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> ChoiceCache =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>(StringComparer.Ordinal);

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
            // Business rule (2026-10-05): Turkish numbers as "0 5xx xxx xx xx".
            string turkish = NormalizeTurkishPhone(request.Phone);
            string formatted = turkish == null ? request.Phone : FormatTurkishPhone(turkish);
            if (!string.IsNullOrWhiteSpace(options.BusinessPhoneAttribute))
            {
                // Business rule (2026-10-06/09): Turkish landlines (02/03/04…) only to the business
                // phone; Turkish mobiles (05…) and foreign numbers (unchanged) to both phones.
                bool landline = turkish != null && !turkish.StartsWith("05", StringComparison.Ordinal);
                SetWhenMapped(record, options.BusinessPhoneAttribute, formatted);
                if (!landline)
                    SetWhenMapped(record, options.PhoneAttribute, formatted);
            }
            else if (string.IsNullOrWhiteSpace(options.IysPhoneAttribute))
            {
                SetWhenMapped(record, options.PhoneAttribute, formatted);
            }
            else
            {
                // Every number goes to the business phone read by the İYS plugins,
                // Turkish mobiles (05…) also to mobile.
                SetWhenMapped(record, options.IysPhoneAttribute, formatted);
                if (turkish != null && turkish.StartsWith("05", StringComparison.Ordinal))
                    SetWhenMapped(record, options.PhoneAttribute, formatted);
            }
            SetWhenMapped(record, options.CompanyAttribute, request.Company);
            SetWhenMapped(record, "subject", request.Subject);
            SetWhenMapped(record, "description", request.Description);
            SetWhenMapped(record, options.EventUrlAttribute, request.EventUrl);

            // Profile fields are written only when their CRM columns are configured;
            // the description keeps the same values as an audit trail either way.
            SetWhenMapped(record, options.JobTitleAttribute, request.JobTitle);
            if (request.KvkkConsent == true && !string.IsNullOrWhiteSpace(options.KvkkConsentAttribute))
                record[options.KvkkConsentAttribute] = true;
            Guid? cityId = FindCity(service, options, request.City);
            if (cityId.HasValue)
                record[options.CityLookupAttribute] = new EntityReference(options.CityEntity, cityId.Value);
            Guid? countryId = FindCountry(service, options, request.CountryCode, request.CountryName);
            if (countryId.HasValue)
                record[options.CountryLookupAttribute] = new EntityReference(options.CountryEntity, countryId.Value);

            // Web form posts carry a consent declaration; legacy posts get no source defaults.
            if (request.KvkkConsent == true)
            {
                int? leadSource = FindChoice(service, options.TargetEntity, options.LeadSourceAttribute,
                    string.IsNullOrWhiteSpace(request.LeadSource) ? options.DefaultLeadSource : request.LeadSource);
                if (leadSource.HasValue)
                    record[options.LeadSourceAttribute] = new OptionSetValue(leadSource.Value);
                Guid? campaignId = FindCampaign(service, options,
                    string.IsNullOrWhiteSpace(options.WebFormCampaign) ? request.SourceCampaign : options.WebFormCampaign);
                if (campaignId.HasValue)
                    record[options.CampaignLookupAttribute] = new EntityReference(options.CampaignEntity, campaignId.Value);

                // Business rule (2026-10-06): checked channel boxes are Evet, unchecked ones Hayır.
                SetBooleanWhenMapped(record, options.EmailPermissionAttribute, request.EmailConsent);
                SetBooleanWhenMapped(record, options.SmsPermissionAttribute, request.SmsConsent);
                SetBooleanWhenMapped(record, options.CallPermissionAttribute, request.CallConsent);
            }

            Guid leadId = service.Create(record);

            if (options.IysConsentEnabled)
            {
                try
                {
                    IysConsentWriter.Apply(service, options.TargetEntity, leadId, request,
                        string.IsNullOrWhiteSpace(options.IysPhoneAttribute) ? null : request.Phone);
                }
                catch (Exception ex)
                {
                    // The Lead already exists; a consent failure must not report the registration as lost.
                    System.Diagnostics.Trace.TraceError("İYS consent processing failed. LeadId={0}; Type={1}; Message={2}",
                        leadId, ex.GetType().Name, ex.Message);
                }
            }

            return leadId;
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

        // Business rule (2026-10-09): the country chosen with the phone number fills the Lead country.
        // CRM country codes are not all ISO (UK for GB) and some repeat (HR, KW, SL, GI), so the
        // name is tried first, then the code; nothing is linked unless exactly one active row matches.
        private static Guid? FindCountry(IOrganizationService service, CrmOptions options, string code, string name)
        {
            if (!options.HasCountryLookup || (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name)))
                return null;

            var query = new QueryExpression(options.CountryEntity)
            {
                ColumnSet = new ColumnSet(options.CountryNameAttribute, options.CountryCodeAttribute),
                TopCount = 20
            };
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            FilterExpression either = query.Criteria.AddFilter(LogicalOperator.Or);
            if (!string.IsNullOrWhiteSpace(name))
                either.AddCondition(options.CountryNameAttribute, ConditionOperator.Equal, name);
            if (!string.IsNullOrWhiteSpace(code))
                either.AddCondition(options.CountryCodeAttribute, ConditionOperator.Equal, code);

            DataCollection<Entity> rows = service.RetrieveMultiple(query).Entities;
            Guid? match = SingleMatch(rows, options.CountryNameAttribute, name) ?? SingleMatch(rows, options.CountryCodeAttribute, code);
            if (!match.HasValue)
                System.Diagnostics.Trace.TraceWarning("Country not linked. Code={0}; Candidates={1}", code, rows.Count);
            return match;
        }

        private static Guid? SingleMatch(IEnumerable<Entity> rows, string attribute, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            List<Entity> hits = rows.Where(row => string.Compare((row.GetAttributeValue<string>(attribute) ?? "").Trim(),
                value.Trim(), Turkish, CompareOptions.IgnoreCase) == 0).Take(2).ToList();
            return hits.Count == 1 ? hits[0].Id : (Guid?)null;
        }

        // Campaign names are not unique in CRM; link only an unambiguous active campaign.
        private static Guid? FindCampaign(IOrganizationService service, CrmOptions options, string campaignName)
        {
            if (!options.HasCampaignLookup || string.IsNullOrWhiteSpace(campaignName))
                return null;

            var query = new QueryExpression(options.CampaignEntity)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 2
            };
            query.Criteria.AddCondition(options.CampaignNameAttribute, ConditionOperator.Equal, campaignName);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);

            EntityCollection matches = service.RetrieveMultiple(query);
            if (matches.Entities.Count == 1)
                return matches.Entities[0].Id;
            System.Diagnostics.Trace.TraceWarning("Campaign not linked. Matches={0}", matches.Entities.Count);
            return null;
        }

        // Resolves a Choice label (e.g. "CO | Web Formu") to its numeric value; never guesses.
        private static int? FindChoice(IOrganizationService service, string entity, string attribute, string label)
        {
            if (string.IsNullOrWhiteSpace(attribute) || string.IsNullOrWhiteSpace(label))
                return null;

            ConcurrentDictionary<string, int> options = ChoiceCache.GetOrAdd(entity + "." + attribute, key =>
            {
                var response = (RetrieveAttributeResponse)service.Execute(new RetrieveAttributeRequest
                {
                    EntityLogicalName = entity,
                    LogicalName = attribute,
                    RetrieveAsIfPublished = false
                });
                // Turkish labels win: in TEST CRM several English (1033) labels are stale and
                // name a different option (e.g. 1033 "CO | Web Formu" is 1055 "CM | Otomasyon").
                var labels = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var choices = ((EnumAttributeMetadata)response.AttributeMetadata).OptionSet.Options.Where(o => o.Value.HasValue).ToList();
                foreach (OptionMetadata option in choices)
                    foreach (LocalizedLabel text in option.Label.LocalizedLabels.Where(l => l.LanguageCode == TurkishLanguageCode))
                        labels.TryAdd(NormalizeLabel(text.Label), option.Value.Value);
                foreach (OptionMetadata option in choices)
                    foreach (LocalizedLabel text in option.Label.LocalizedLabels.Where(l => l.LanguageCode != TurkishLanguageCode))
                        labels.TryAdd(NormalizeLabel(text.Label), option.Value.Value);
                return labels;
            });

            int value;
            if (options.TryGetValue(NormalizeLabel(label), out value))
                return value;
            System.Diagnostics.Trace.TraceWarning("Lead source label not found in CRM choices.");
            return null;
        }

        // Returns 11 digits starting with 0 for a Turkish number, otherwise null.
        // "+" numbers count as Turkish only with the +90 country code.
        internal static string NormalizeTurkishPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;
            string trimmed = phone.Trim();
            string digits = new string(trimmed.Where(char.IsDigit).ToArray());
            if (trimmed.StartsWith("+", StringComparison.Ordinal))
                return digits.StartsWith("90", StringComparison.Ordinal) && digits.Length == 12 ? "0" + digits.Substring(2) : null;
            if (digits.StartsWith("0090", StringComparison.Ordinal) && digits.Length == 14)
                return "0" + digits.Substring(4);
            if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length == 12)
                return "0" + digits.Substring(2);
            if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length == 11)
                return digits;
            if (!digits.StartsWith("0", StringComparison.Ordinal) && digits.Length == 10)
                return "0" + digits;
            return null;
        }

        // "05551112233" → "0 555 111 22 33" (same layout as the CRM İYS records).
        internal static string FormatTurkishPhone(string elevenDigits)
        {
            return elevenDigits.Substring(0, 1) + " " + elevenDigits.Substring(1, 3) + " " + elevenDigits.Substring(4, 3) + " " +
                   elevenDigits.Substring(7, 2) + " " + elevenDigits.Substring(9, 2);
        }

        private static string NormalizeLabel(string label)
        {
            return string.Join(" ", (label ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static void SetBooleanWhenMapped(Entity entity, string attribute, bool value)
        {
            if (!string.IsNullOrWhiteSpace(attribute))
                entity[attribute] = value;
        }

        private static void SetWhenMapped(Entity entity, string attribute, string value)
        {
            if (!string.IsNullOrWhiteSpace(attribute) && !string.IsNullOrWhiteSpace(value))
                entity[attribute] = value;
        }
    }
}
