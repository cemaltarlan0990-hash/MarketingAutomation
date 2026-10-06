using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using RelatedEntegrasyonu.CrmGateway.Configuration;
using RelatedEntegrasyonu.CrmGateway.Infrastructure;
using RelatedEntegrasyonu.CrmGateway.Models;
using RelatedEntegrasyonu.CrmGateway.Services;

// Compiled with the production model/writer sources; no connection or token acquisition is included.
internal static class WebLeadTests
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
        Console.WriteLine("[OK] " + name);
    }

    private static Dictionary<string, object> Payload()
    {
        return new Dictionary<string, object> {
            { "firstName", " Ahmet " }, { "lastName", "Yılmaz" }, { "email", "ahmet@example.invalid" },
            { "phone", "+905551112233" }, { "company", "ABC" }, { "message", "Ürün hakkında bilgi\nistiyorum." },
            { "consent", true }, { "website", "" }, { "captchaToken", "test-token" }
        };
    }
    private static WebLeadRequest Parse(Dictionary<string, object> payload)
    { return WebLeadRequest.FromJson(new JavaScriptSerializer().Serialize(payload)); }
    private static void Reject<T>(Action action, string name) where T : Exception
    {
        try { action(); }
        catch (T) { Check(true, name); return; }
        throw new Exception("Expected rejection: " + name);
    }

    public static int Main()
    {
        try
        {
            var data = Parse(Payload());
            Check(data.Registration.FirstName == "Ahmet", "Names normalize correctly");
            Check(data.Registration.LastName == "Yılmaz", "Turkish Unicode preserved");
            var received = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
            data.PrepareCrmRecord("form-v1", received);
            Check(data.Registration.Description.Contains("Ürün hakkında bilgi\nistiyorum."), "Message preserved");
            Check(data.Registration.Description.Contains("form-v1") && data.Registration.Description.Contains("2026-09-30T12:00:00"), "Consent version and server timestamp stored");
            Check(data.Registration.Description.Length <= 2000, "Description within CRM limit");
            foreach (object consent in new object[] { false, "true", 1, null })
            {
                var bad = Payload(); bad["consent"] = consent;
                Reject<RequestValidationException>(() => Parse(bad), "Strict boolean consent: " + (consent ?? "null"));
            }
            var missing = Payload(); missing.Remove("consent");
            Reject<RequestValidationException>(() => Parse(missing), "Missing consent rejected");
            var spam = Payload(); spam["website"] = "spam";
            Reject<WebLeadRejectedException>(() => Parse(spam), "Honeypot rejected");
            var captcha = Payload(); captcha["captchaToken"] = "";
            Reject<WebLeadRejectedException>(() => Parse(captcha), "Missing CAPTCHA rejected");
            var trusted = Payload(); trusted.Remove("captchaToken");
            var trustedForm = WebLeadRequest.FromAuthenticatedJson(new JavaScriptSerializer().Serialize(trusted));
            trustedForm.PrepareCrmRecord(null, received);
            Check(trustedForm.Registration.Email == "ahmet@example.invalid", "Authenticated website payload accepted without replaying CAPTCHA");
            Check(!trustedForm.Registration.Description.Contains("Metin sürümü"), "Missing consent version is omitted, not invented");
            trusted["consent"] = false;
            Reject<RequestValidationException>(() => WebLeadRequest.FromAuthenticatedJson(new JavaScriptSerializer().Serialize(trusted)), "Authenticated payload still requires consent");
            trusted["consent"] = true; trusted["website"] = "spam";
            Reject<WebLeadRejectedException>(() => WebLeadRequest.FromAuthenticatedJson(new JavaScriptSerializer().Serialize(trusted)), "Authenticated payload still rejects honeypot");
            var email = Payload(); email["email"] = "not-an-email";
            Reject<RequestValidationException>(() => Parse(email), "Invalid email rejected");
            var longText = Payload(); longText["message"] = new string('x', 1501);
            Reject<RequestValidationException>(() => Parse(longText), "Message limit enforced");
            var longToken = Payload(); longToken["captchaToken"] = new string('x', 2049);
            Reject<RequestValidationException>(() => Parse(longToken), "Token limit enforced");
            var type = Payload(); type["firstName"] = new object[] { "Ahmet" };
            Reject<RequestValidationException>(() => Parse(type), "Array cannot replace text");
            var control = Payload(); control["company"] = "ABC\r\nInjected";
            Reject<RequestValidationException>(() => Parse(control), "Control characters rejected");
            Reject<RequestValidationException>(() => WebLeadRequest.FromJson("{"), "Malformed JSON rejected");
            Reject<RequestValidationException>(() => WebLeadRequest.FromJson("[]"), "Root array rejected");
            Reject<RequestValidationException>(() => WebLeadRequest.FromJson("null"), "Null root rejected");

            Environment.SetEnvironmentVariable("CRM_TARGET_ENTITY", "lead");
            Environment.SetEnvironmentVariable("CRM_FIRSTNAME_ATTRIBUTE", "firstname");
            Environment.SetEnvironmentVariable("CRM_LASTNAME_ATTRIBUTE", "lastname");
            Environment.SetEnvironmentVariable("CRM_EMAIL_ATTRIBUTE", "emailaddress1");
            Environment.SetEnvironmentVariable("CRM_PHONE_ATTRIBUTE", "mobilephone");
            Environment.SetEnvironmentVariable("CRM_COMPANY_ATTRIBUTE", "companyname");
            CrmOptions options = CrmOptions.Load();
            var fake = new RecordingService();
            Guid id = CrmRegistrationWriter.Write(fake, options, data.Registration);
            Check(id == fake.Result && fake.Creates == 1 && fake.Record.LogicalName == "lead", "Exactly one CRM Lead creation");
            Check((string)fake.Record["firstname"] == "Ahmet" && (string)fake.Record["emailaddress1"] == "ahmet@example.invalid", "Core Lead mapping");
            Check((string)fake.Record["mobilephone"] == "+905551112233" && (string)fake.Record["companyname"] == "ABC", "Optional Lead mapping");
            Check((string)fake.Record["subject"] == "Web sitesi form talebi" && (string)fake.Record["description"] == data.Registration.Description, "Subject and description mapping");
            Check(!fake.Record.Contains("donotbulkemail") && !fake.Record.Contains("leadsourcecode"), "No inferred marketing permission or unverified choice value");
            var legacy = CrmRegistrationRequest.Create("Test", "Legacy", "test@example.invalid", null, null, null);
            CrmRegistrationWriter.Write(fake, options, legacy);
            Check(!fake.Record.Contains("subject") && !fake.Record.Contains("description"), "Legacy writer contract preserved");

            var eventPayload = Payload();
            eventPayload["eventTitle"] = "TEST Kayıttır Silmeyin";
            eventPayload["eventUrl"] = "https://altium.net/tr/etkinlik-kayit/test-kayittir-silmeyin";
            eventPayload["jobTitle"] = "Mühendis";
            eventPayload["city"] = "İSTANBUL";
            eventPayload["marketingConsent"] = true;
            eventPayload["emailConsent"] = true;
            eventPayload["smsConsent"] = false;
            eventPayload["phoneConsent"] = false;
            var eventData = Parse(eventPayload);
            eventData.ValidateEventOrigin("https://altium.net");
            eventData.PrepareCrmRecord("event-v1", received);
            CrmRegistrationWriter.Write(fake, options, eventData.Registration);
            Check((string)fake.Record["subject"] == "Etkinlik Kaydı: TEST Kayıttır Silmeyin", "Event title retained in Lead subject");
            string description = (string)fake.Record["description"];
            Check(description.Contains("Mühendis") && description.Contains("İSTANBUL") && description.Contains("https://altium.net/tr/etkinlik-kayit/"), "Job title city and event URL stored");
            Check(description.Contains("Pazarlama onayı: Evet") && description.Contains("E-posta kanalı: Evet") && description.Contains("SMS kanalı: Hayır"), "Distinct marketing and channel declarations retained");
            Reject<RequestValidationException>(() => eventData.ValidateEventOrigin("https://attacker.example"), "Cross-origin event URL rejected");
            var wrongUrl = new Dictionary<string, object>(eventPayload); wrongUrl["eventUrl"] = "https://altium.net/tr/iletisim";
            Reject<RequestValidationException>(() => Parse(wrongUrl).ValidateEventOrigin("https://altium.net"), "Non-event URL rejected");
            var channel = new Dictionary<string, object>(eventPayload); channel["marketingConsent"] = false;
            Reject<RequestValidationException>(() => Parse(channel), "Channel requires explicit marketing consent");
            var emptyChannels = new Dictionary<string, object>(eventPayload); emptyChannels["emailConsent"] = false;
            Reject<RequestValidationException>(() => Parse(emptyChannels), "Marketing consent requires a selected channel");
            var missingChannel = new Dictionary<string, object>(eventPayload); missingChannel.Remove("smsConsent");
            Reject<RequestValidationException>(() => Parse(missingChannel), "No silent default for missing marketing channel");
            var badMarketing = new Dictionary<string, object>(eventPayload); badMarketing["marketingConsent"] = "true";
            Reject<RequestValidationException>(() => Parse(badMarketing), "Marketing boolean is strict");
            var longEventMessage = new Dictionary<string, object>(eventPayload); longEventMessage["message"] = new string('x', 1001);
            Reject<RequestValidationException>(() => Parse(longEventMessage), "Event message limit reserves audit space");

            Check(!fake.Record.Contains("twbs_isunvani") && !fake.Record.Contains("twbs_sehir") && !fake.Record.Contains("twbs_kvkkonayi"), "Unmapped profile fields are not written");
            Environment.SetEnvironmentVariable("CRM_JOBTITLE_ATTRIBUTE", "twbs_isunvani");
            Environment.SetEnvironmentVariable("CRM_CITY_LOOKUP_ATTRIBUTE", "twbs_sehir");
            Environment.SetEnvironmentVariable("CRM_CITY_ENTITY", "twbs_sehir");
            Environment.SetEnvironmentVariable("CRM_CITY_NAME_ATTRIBUTE", "twbs_sehiradi");
            Environment.SetEnvironmentVariable("CRM_KVKK_ATTRIBUTE", "twbs_kvkkonayi");
            CrmOptions profileOptions = CrmOptions.Load();
            var cities = new RecordingService { CityId = Guid.NewGuid() };
            CrmRegistrationWriter.Write(cities, profileOptions, eventData.Registration);
            Check((string)cities.Record["twbs_isunvani"] == "Mühendis", "Job title written to its CRM column");
            var city = (EntityReference)cities.Record["twbs_sehir"];
            Check(city.LogicalName == "twbs_sehir" && city.Id == cities.CityId, "City linked to the CRM city record");
            Check(cities.LastCityQuery == "twbs_sehir:twbs_sehiradi=İSTANBUL", "City looked up by its exact form value");
            Check((bool)cities.Record["twbs_kvkkonayi"], "KVKK consent written to its CRM column");
            Check(!cities.Record.Contains("twbs_elektronikiletionayi") && !cities.Record.Contains("altium_iysepostadurumu"), "Commercial message and IYS fields untouched");
            var unknownCity = new RecordingService();
            CrmRegistrationWriter.Write(unknownCity, profileOptions, eventData.Registration);
            Check(unknownCity.Creates == 1 && !unknownCity.Record.Contains("twbs_sehir") && ((string)unknownCity.Record["description"]).Contains("İSTANBUL"), "Unknown city keeps the registration and the description");
            var legacyProfile = new RecordingService();
            CrmRegistrationWriter.Write(legacyProfile, profileOptions, legacy);
            Check(!legacyProfile.Record.Contains("twbs_kvkkonayi") && legacyProfile.CityQueries == 0, "Legacy form post claims no KVKK consent and skips city lookup");
            Check(!cities.Record.Contains("telephone1") && !cities.Record.Contains("leadsourcecode") && cities.LeadUpdates.Count == 0, "Source and İYS features stay off until configured");

            Environment.SetEnvironmentVariable("CRM_IYS_PHONE_ATTRIBUTE", "telephone1");
            Environment.SetEnvironmentVariable("CRM_IYS_CONSENT_ENABLED", "true");
            Environment.SetEnvironmentVariable("CRM_LEAD_SOURCE_ATTRIBUTE", "leadsourcecode");
            Environment.SetEnvironmentVariable("CRM_DEFAULT_LEAD_SOURCE", "CO | Web Formu");
            Environment.SetEnvironmentVariable("CRM_CAMPAIGN_LOOKUP_ATTRIBUTE", "campaignid");
            Environment.SetEnvironmentVariable("CRM_CAMPAIGN_ENTITY", "campaign");
            Environment.SetEnvironmentVariable("CRM_CAMPAIGN_NAME_ATTRIBUTE", "name");
            CrmOptions iysOptions = CrmOptions.Load();
            Func<Dictionary<string, object>, WebLeadRequest> prepared = changes =>
            {
                var p = new Dictionary<string, object>(eventPayload);
                foreach (var change in changes) p[change.Key] = change.Value;
                var parsed = Parse(p); parsed.ValidateEventOrigin("https://altium.net"); parsed.PrepareCrmRecord("event-v1", received); return parsed;
            };
            var iysForm = prepared(new Dictionary<string, object> { { "smsConsent", true }, { "sourceCampaign", "Kalite'26" }, { "leadSource", "co |  fuar" } });
            var crm = new RecordingService { CampaignMatches = 1 };
            Guid created = CrmRegistrationWriter.Write(crm, iysOptions, iysForm.Registration);
            Check(created == crm.Result && (string)crm.Record["telephone1"] == "0 555 111 22 33" && (string)crm.Record["mobilephone"] == "0 555 111 22 33", "Turkish mobile formatted into business and mobile phone");
            var landline = new RecordingService();
            CrmRegistrationWriter.Write(landline, iysOptions, prepared(new Dictionary<string, object> { { "phone", "+902122223344" } }).Registration);
            Check((string)landline.Record["telephone1"] == "0 212 222 33 44" && !landline.Record.Contains("mobilephone"), "Turkish landline only in business phone");
            var abroad = new RecordingService();
            CrmRegistrationWriter.Write(abroad, iysOptions, prepared(new Dictionary<string, object> { { "phone", "+4915112345678" } }).Registration);
            Check((string)abroad.Record["telephone1"] == "+4915112345678" && !abroad.Record.Contains("mobilephone"), "Foreign number unchanged and only in business phone");
            Check(((OptionSetValue)crm.Record["leadsourcecode"]).Value == 12, "Lead source label resolved to its real choice value");
            Check(((EntityReference)crm.Record["campaignid"]).Id == crm.CampaignId, "Unambiguous campaign linked by name");
            Check(crm.LeadUpdates.Count == 1 && (bool)crm.LeadUpdates[0]["altium_emailonayi"] && (bool)crm.LeadUpdates[0]["altium_mesajonayi"], "Selected channels sent to the existing İYS plugins");
            Check(!crm.LeadUpdates[0].Contains("altium_aramaonayi"), "Unselected channel is not touched or rejected");
            Check(crm.Activities.Count == 2 && crm.Activities.All(a => ((OptionSetValue)a["altium_onaykaynagi"]).Value == 1), "Form consent activities marked as Webform");
            Check(crm.Activities.All(a => ((OptionSetValue)a["statecode"]).Value == 1 && ((OptionSetValue)a["statuscode"]).Value == 2), "Activities returned to their completed state");
            IysConsentWriter.Apply(crm, "lead", crm.Result, iysForm.Registration, "+905551112233");
            Check(crm.LeadUpdates.Count == 1 && crm.Activities.Count == 2, "Retry does not duplicate İYS activities");

            var defaults = new RecordingService { CampaignMatches = 2 };
            CrmRegistrationWriter.Write(defaults, iysOptions, prepared(new Dictionary<string, object> { { "sourceCampaign", "BSS SOFTWARE DOWNLOAD" } }).Registration);
            Check(((OptionSetValue)defaults.Record["leadsourcecode"]).Value == 250160001, "Missing lead source falls back to the configured default");
            Check(!defaults.Record.Contains("campaignid"), "Ambiguous campaign name is not linked");
            var unknown = new RecordingService();
            CrmRegistrationWriter.Write(unknown, iysOptions, prepared(new Dictionary<string, object> { { "leadSource", "Uydurma Kaynak" } }).Registration);
            Check(!unknown.Record.Contains("leadsourcecode"), "Unknown lead source label is not guessed");

            var foreign = new RecordingService();
            CrmRegistrationWriter.Write(foreign, iysOptions, prepared(new Dictionary<string, object> { { "phone", "+4915112345678" }, { "smsConsent", true } }).Registration);
            Check(foreign.LeadUpdates.Count == 1 && foreign.LeadUpdates[0].Contains("altium_emailonayi") && !foreign.LeadUpdates[0].Contains("altium_mesajonayi"), "Non-Turkish number skips phone İYS channels only");
            var noMarketing = new RecordingService();
            CrmRegistrationWriter.Write(noMarketing, iysOptions, prepared(new Dictionary<string, object> { { "marketingConsent", false }, { "emailConsent", false } }).Registration);
            Check(noMarketing.LeadUpdates.Count == 0 && noMarketing.Activities.Count == 0, "No commercial consent means no İYS change");
            var failing = new RecordingService { FailLeadUpdates = true };
            Check(CrmRegistrationWriter.Write(failing, iysOptions, iysForm.Registration) == failing.Result, "İYS failure does not lose the created Lead");
            var legacyIys = new RecordingService();
            CrmRegistrationWriter.Write(legacyIys, iysOptions, legacy);
            Check(!legacyIys.Record.Contains("leadsourcecode") && legacyIys.LeadUpdates.Count == 0, "Legacy form post gets no source default or İYS change");
            Environment.SetEnvironmentVariable("CRM_WEB_FORM_CAMPAIGN", "Web");
            CrmOptions webCampaign = CrmOptions.Load();
            var fixedCampaign = new RecordingService { CampaignMatches = 1 };
            CrmRegistrationWriter.Write(fixedCampaign, webCampaign, iysForm.Registration);
            Check(fixedCampaign.LastCampaignName == "Web" && ((EntityReference)fixedCampaign.Record["campaignid"]).Id == fixedCampaign.CampaignId, "Web form Lead always linked to the Web campaign, not the form value");
            var legacyCampaign = new RecordingService { CampaignMatches = 1 };
            CrmRegistrationWriter.Write(legacyCampaign, webCampaign, legacy);
            Check(!legacyCampaign.Record.Contains("campaignid") && legacyCampaign.LastCampaignName == null, "Legacy form post gets no campaign");

            Check(!fixedCampaign.Record.Contains("altium_epostaizni"), "Permission fields stay off until configured");
            Environment.SetEnvironmentVariable("CRM_EMAIL_PERMISSION_ATTRIBUTE", "altium_epostaizni");
            Environment.SetEnvironmentVariable("CRM_SMS_PERMISSION_ATTRIBUTE", "altium_mesajizni");
            Environment.SetEnvironmentVariable("CRM_CALL_PERMISSION_ATTRIBUTE", "altium_aramaizni");
            CrmOptions permissionOptions = CrmOptions.Load();
            var permissions = new RecordingService();
            CrmRegistrationWriter.Write(permissions, permissionOptions, iysForm.Registration);
            Check((bool)permissions.Record["altium_epostaizni"] && (bool)permissions.Record["altium_mesajizni"] && !(bool)permissions.Record["altium_aramaizni"], "Checked channels Evet, unchecked channel Hayır");
            var noPermission = new RecordingService();
            CrmRegistrationWriter.Write(noPermission, permissionOptions, prepared(new Dictionary<string, object> { { "marketingConsent", false }, { "emailConsent", false } }).Registration);
            Check(!(bool)noPermission.Record["altium_epostaizni"] && !(bool)noPermission.Record["altium_mesajizni"] && !(bool)noPermission.Record["altium_aramaizni"], "No marketing consent writes Hayır to all three");
            var legacyPermission = new RecordingService();
            CrmRegistrationWriter.Write(legacyPermission, permissionOptions, legacy);
            Check(!legacyPermission.Record.Contains("altium_epostaizni") && !legacyPermission.Record.Contains("altium_mesajizni") && !legacyPermission.Record.Contains("altium_aramaizni"), "Legacy form post leaves permission fields untouched");

            Func<string, string> tr = CrmRegistrationWriter.NormalizeTurkishPhone;
            Check(tr("+905551112233") == "05551112233" && tr("0555 111 22 33") == "05551112233" && tr("5551112233") == "05551112233"
                && tr("00905551112233") == "05551112233" && tr("905551112233") == "05551112233", "Turkish number variants normalized");
            Check(tr("+4915112345678") == null && tr("+12125551234") == null && tr("") == null, "Foreign numbers are not treated as Turkish");
            Check(CrmRegistrationWriter.FormatTurkishPhone("05551112233") == "0 555 111 22 33", "Turkish number formatted as 0 555 555 55 55");

            Check(TurnstileVerifier.IsValidResponse("{\"success\":true,\"hostname\":\"www.example.com\",\"action\":\"web-lead\"}", "www.example.com", "web-lead"), "CAPTCHA result accepted only for matching site and action");
            Check(!TurnstileVerifier.IsValidResponse("{\"success\":true,\"hostname\":\"attacker.example\",\"action\":\"web-lead\"}", "www.example.com", "web-lead"), "CAPTCHA hostname mismatch rejected");
            Check(!TurnstileVerifier.IsValidResponse("{\"success\":true,\"hostname\":\"www.example.com\",\"action\":\"login\"}", "www.example.com", "web-lead"), "CAPTCHA action mismatch rejected");
            Check(!TurnstileVerifier.IsValidResponse("{\"success\":false}", "www.example.com", "web-lead"), "Failed CAPTCHA rejected");
            Check(TurnstileVerifier.IsValidResponse("{\"success\":true,\"hostname\":\"www.example.com\",\"action\":\"\"}", "www.example.com", null), "Unconfigured action accepts widget without data-action");
            Check(!TurnstileVerifier.IsValidResponse("{\"success\":true,\"hostname\":\"attacker.example\",\"action\":\"\"}", "www.example.com", null), "Unconfigured action still requires matching hostname");
            Check(!TurnstileVerifier.IsValidResponse("{\"success\":false,\"hostname\":\"www.example.com\"}", "www.example.com", null), "Unconfigured action still requires CAPTCHA success");
            Environment.SetEnvironmentVariable("WEB_LEAD_ALLOWED_ORIGINS", "https://www.example.com,http://localhost:3000");
            var web = WebLeadOptions.Load();
            Check(web.Allows("https://www.example.com") && !web.Allows("https://www.example.com.attacker.example"), "Exact origin allowlist");
            Check(!web.Allows(null) && !web.Allows("null"), "Missing and null origins rejected");
            Environment.SetEnvironmentVariable("WEB_LEAD_ALLOWED_ORIGINS", "*");
            Reject<ConfigurationErrorsException>(() => WebLeadOptions.Load(), "Wildcard CORS configuration rejected");
            Environment.SetEnvironmentVariable("WEB_LEAD_ALLOWED_ORIGINS", "https://www.example.com");
            Environment.SetEnvironmentVariable("TURNSTILE_SECRET_KEY", "1x0000000000000000000000000000000AA");
            Environment.SetEnvironmentVariable("WEB_LEAD_CONSENT_VERSION", "v1");
            Reject<ConfigurationErrorsException>(() => WebLeadOptions.Load().ValidateForSubmission(), "Public endpoint forbids CAPTCHA test secrets");
            int retry;
            for (int i = 0; i < 5; i++) Check(WebLeadRateLimiter.TryAcquire("test-address", out retry), "Rate limit accepts request " + (i + 1));
            Check(!WebLeadRateLimiter.TryAcquire("test-address", out retry) && retry > 0, "Per-address limit enforced");
            for (int i = 0; i < 25; i++)
                if (!WebLeadRateLimiter.TryAcquire("address-" + i, out retry)) throw new Exception("Unexpected global rejection");
            Check(!WebLeadRateLimiter.TryAcquire("new-address", out retry), "Global process limit enforced");
            Console.WriteLine("Passed " + checks + " checks. No CRM requests made.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    // In-memory CRM. Lead consent flags simulate the ManuelMkvyOnay plugins verified in TEST CRM:
    // each flag creates a closed İYS activity with source "Manuel"; closed activities reject edits.
    private sealed class RecordingService : IOrganizationService
    {
        public Entity Record;
        public int Creates;
        public readonly Guid Result = Guid.NewGuid();
        public Guid? CityId;
        public int CityQueries;
        public string LastCityQuery;
        public int CampaignMatches;
        public readonly Guid CampaignId = Guid.NewGuid();
        public string LastCampaignName;
        public bool FailLeadUpdates;
        public readonly List<Entity> LeadUpdates = new List<Entity>();
        public readonly List<Entity> Activities = new List<Entity>();
        public Guid Create(Entity entity) { Record = entity; Creates++; return Result; }
        public Entity Retrieve(string entityName, Guid id, ColumnSet columns) { throw new NotSupportedException(); }
        public void Update(Entity entity)
        {
            if (entity.LogicalName == "lead")
            {
                if (FailLeadUpdates) throw new InvalidOperationException("plugin failure");
                LeadUpdates.Add(entity);
                foreach (var flag in new[] { Tuple.Create("altium_emailonayi", 1), Tuple.Create("altium_aramaonayi", 2), Tuple.Create("altium_mesajonayi", 3) })
                    if (entity.Contains(flag.Item1) && (bool)entity[flag.Item1])
                    {
                        var activity = new Entity("altium_iysaktivitesi", Guid.NewGuid());
                        activity["altium_iystipii"] = new OptionSetValue(flag.Item2);
                        activity["altium_onaykaynagi"] = new OptionSetValue(2);
                        activity["statecode"] = new OptionSetValue(1);
                        activity["statuscode"] = new OptionSetValue(2);
                        Activities.Add(activity);
                    }
                return;
            }
            var target = Activities.First(a => a.Id == entity.Id);
            bool closed = ((OptionSetValue)target["statecode"]).Value != 0;
            if (closed && !entity.Contains("statecode")) throw new InvalidOperationException("Cannot update Closed or Cancelled Activity");
            foreach (var attribute in entity.Attributes) target[attribute.Key] = attribute.Value;
        }
        public void Delete(string entityName, Guid id) { throw new NotSupportedException(); }
        public OrganizationResponse Execute(OrganizationRequest request)
        {
            // Mirrors TEST CRM: stale English labels name other options and are listed first.
            var metadata = new PicklistAttributeMetadata { OptionSet = new OptionSetMetadata() };
            var automation = new Label(new LocalizedLabel("CO | Web Formu", 1033), new[] { new LocalizedLabel("CO | Web Formu", 1033), new LocalizedLabel("CM | Otomasyon", 1055) });
            metadata.OptionSet.Options.Add(new OptionMetadata(automation, 15));
            metadata.OptionSet.Options.Add(new OptionMetadata(new Label("CO | Web Formu", 1055), 250160001));
            metadata.OptionSet.Options.Add(new OptionMetadata(new Label("CO | Fuar", 1055), 12));
            var response = new RetrieveAttributeResponse();
            response.Results["AttributeMetadata"] = metadata;
            return response;
        }
        public void Associate(string entityName, Guid id, Relationship relationship, EntityReferenceCollection entities) { throw new NotSupportedException(); }
        public void Disassociate(string entityName, Guid id, Relationship relationship, EntityReferenceCollection entities) { throw new NotSupportedException(); }
        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            var expression = (QueryExpression)query;
            var result = new EntityCollection();
            if (expression.EntityName == "campaign")
            {
                LastCampaignName = (string)expression.Criteria.Conditions.First(c => c.AttributeName == "name").Values[0];
                for (int i = 0; i < CampaignMatches; i++) result.Entities.Add(new Entity("campaign", i == 0 ? CampaignId : Guid.NewGuid()));
                return result;
            }
            if (expression.EntityName == "altium_iysaktivitesi")
            {
                IEnumerable<Entity> items = Activities;
                foreach (ConditionExpression c in expression.Criteria.Conditions)
                {
                    if (c.AttributeName == "altium_onaykaynagi") items = items.Where(a => ((OptionSetValue)a["altium_onaykaynagi"]).Value == (int)c.Values[0]);
                    if (c.AttributeName == "altium_iystipii") items = items.Where(a => c.Values.Contains(((OptionSetValue)a["altium_iystipii"]).Value));
                }
                foreach (var item in items.ToList()) result.Entities.Add(item);
                return result;
            }
            var condition = expression.Criteria.Conditions[0];
            CityQueries++;
            LastCityQuery = expression.EntityName + ":" + condition.AttributeName + "=" + condition.Values[0];
            if (CityId.HasValue) result.Entities.Add(new Entity(expression.EntityName, CityId.Value));
            return result;
        }
    }
}
