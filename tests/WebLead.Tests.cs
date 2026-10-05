using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;
using Microsoft.Xrm.Sdk;
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

    private sealed class RecordingService : IOrganizationService
    {
        public Entity Record;
        public int Creates;
        public readonly Guid Result = Guid.NewGuid();
        public Guid Create(Entity entity) { Record = entity; Creates++; return Result; }
        public Entity Retrieve(string entityName, Guid id, ColumnSet columns) { throw new NotSupportedException(); }
        public void Update(Entity entity) { throw new NotSupportedException(); }
        public void Delete(string entityName, Guid id) { throw new NotSupportedException(); }
        public OrganizationResponse Execute(OrganizationRequest request) { throw new NotSupportedException(); }
        public void Associate(string entityName, Guid id, Relationship relationship, EntityReferenceCollection entities) { throw new NotSupportedException(); }
        public void Disassociate(string entityName, Guid id, Relationship relationship, EntityReferenceCollection entities) { throw new NotSupportedException(); }
        public EntityCollection RetrieveMultiple(QueryBase query) { throw new NotSupportedException(); }
    }
}
