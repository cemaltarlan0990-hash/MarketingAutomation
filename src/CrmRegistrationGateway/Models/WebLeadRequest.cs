using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using RelatedEntegrasyonu.CrmGateway.Infrastructure;

namespace RelatedEntegrasyonu.CrmGateway.Models
{
    internal sealed class WebLeadRequest
    {
        public CrmRegistrationRequest Registration { get; private set; }
        public string CaptchaToken { get; private set; }
        public string Message { get; private set; }
        public string JobTitle { get; private set; }
        public string City { get; private set; }
        public string EventTitle { get; private set; }
        public string EventUrl { get; private set; }
        public string SourceCampaign { get; private set; }
        public string LeadSource { get; private set; }
        public string CountryCode { get; private set; }
        public string CountryName { get; private set; }
        public bool? MarketingConsent { get; private set; }
        public bool EmailConsent { get; private set; }
        public bool SmsConsent { get; private set; }
        public bool PhoneConsent { get; private set; }

        public static WebLeadRequest FromJson(string json)
        {
            return Parse(json, true);
        }

        // Only the authenticated, server-to-server endpoint may use this entry point.
        // The website has already verified its own CAPTCHA before forwarding.
        public static WebLeadRequest FromAuthenticatedJson(string json)
        {
            return Parse(json, false);
        }

        private static WebLeadRequest Parse(string json, bool requireCaptcha)
        {
            Dictionary<string, object> data;
            try
            {
                data = new JavaScriptSerializer { MaxJsonLength = 16384, RecursionLimit = 8 }
                    .Deserialize<Dictionary<string, object>>(json);
            }
            catch (ArgumentException) { throw new RequestValidationException("Geçerli bir JSON nesnesi gönderilmelidir."); }
            catch (InvalidOperationException) { throw new RequestValidationException("Geçerli bir JSON nesnesi gönderilmelidir."); }
            if (data == null) throw new RequestValidationException("İstek gövdesi boş olamaz.");

            string website = Text(data, "website", 200, false);
            if (website.Length != 0) throw new WebLeadRejectedException();
            object consent;
            if (!data.TryGetValue("consent", out consent) || !(consent is bool) || !(bool)consent)
                throw new RequestValidationException("Form onayı verilmelidir.");

            string firstName = Text(data, "firstName", 50, false);
            string lastName = Text(data, "lastName", 50, false);
            string email = Text(data, "email", 100, false);
            string phone = Text(data, "phone", 50, false);
            string company = Text(data, "company", 100, false);
            string eventUrl = Text(data, "eventUrl", 400, false);
            string eventTitle = Text(data, "eventTitle", 150, false);
            string message = Text(data, "message", eventUrl.Length == 0 ? 1500 : 1000, true);
            bool? marketing = Boolean(data, "marketingConsent", false);
            bool emailConsent = Boolean(data, "emailConsent", marketing.HasValue) ?? false;
            bool smsConsent = Boolean(data, "smsConsent", marketing.HasValue) ?? false;
            bool phoneConsent = Boolean(data, "phoneConsent", marketing.HasValue) ?? false;
            if ((!marketing.HasValue || !marketing.Value) && (emailConsent || smsConsent || phoneConsent))
                throw new RequestValidationException("İletişim kanalları için pazarlama onayı gereklidir.");
            if (marketing == true && !(emailConsent || smsConsent || phoneConsent))
                throw new RequestValidationException("Pazarlama onayı için en az bir iletişim kanalı seçilmelidir.");
            string countryCode = Text(data, "countryCode", 3, false);
            if (countryCode.Length != 0 && !countryCode.All(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')))
                throw new RequestValidationException("countryCode alanı geçersiz.");
            string captcha = Text(data, "captchaToken", 2048, false);
            if (requireCaptcha && string.IsNullOrWhiteSpace(captcha)) throw new WebLeadRejectedException();
            return new WebLeadRequest
            {
                Registration = CrmRegistrationRequest.Create(firstName, lastName, email, phone, company, null),
                Message = message,
                CaptchaToken = captcha,
                JobTitle = Text(data, "jobTitle", 100, false),
                City = Text(data, "city", 100, false),
                EventTitle = eventTitle,
                EventUrl = eventUrl,
                SourceCampaign = Text(data, "sourceCampaign", 200, false),
                LeadSource = Text(data, "leadSource", 200, false),
                CountryCode = countryCode,
                CountryName = Text(data, "countryName", 100, false),
                MarketingConsent = marketing,
                EmailConsent = emailConsent,
                SmsConsent = smsConsent,
                PhoneConsent = phoneConsent
            };
        }

        public void PrepareCrmRecord(string consentVersion, DateTime receivedAtUtc)
        {
            // Business rules (2026-10-09): the subject is the event name exactly as on the page and
            // the description is only the visitor's message. Profile, country, event URL and
            // permissions have their own Lead fields; consentVersion and receivedAtUtc are no
            // longer written to CRM (the website keeps its own registration record).
            Registration.SetWebFormDetails(EventTitle.Length == 0 ? "Web sitesi form talebi" : EventTitle, Message);
            // Parsing already rejected requests without consent:true.
            Registration.SetProfileDetails(JobTitle, City, true);
            Registration.SetSourceDetails(SourceCampaign, LeadSource);
            Registration.SetEventUrl(EventUrl);
            Registration.SetCountry(CountryCode, CountryName);
            // Channels only count when marketing consent was explicitly given (parsing enforces this).
            bool marketing = MarketingConsent == true;
            Registration.SetChannelConsents(marketing && EmailConsent, marketing && SmsConsent, marketing && PhoneConsent);
        }

        public void ValidateEventOrigin(params string[] origins)
        {
            if (string.IsNullOrEmpty(EventUrl)) return;
            Uri url;
            if (!Uri.TryCreate(EventUrl, UriKind.Absolute, out url) ||
                !origins.Contains(url.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(url.UserInfo) ||
                !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) ||
                !url.AbsolutePath.StartsWith("/tr/etkinlik-kayit/", StringComparison.Ordinal))
                throw new RequestValidationException("Etkinlik form adresi geçersiz.");
            if (string.IsNullOrWhiteSpace(EventTitle))
                throw new RequestValidationException("Etkinlik başlığı zorunludur.");
        }

        private static bool? Boolean(Dictionary<string, object> data, string name, bool required)
        {
            object value;
            if (!data.TryGetValue(name, out value))
            {
                if (required) throw new RequestValidationException(name + " alanı zorunludur.");
                return null;
            }
            if (!(value is bool)) throw new RequestValidationException(name + " boolean olmalıdır.");
            return (bool)value;
        }

        private static string Text(Dictionary<string, object> data, string name, int limit, bool multiline)
        {
            object value;
            if (!data.TryGetValue(name, out value)) return "";
            if (!(value is string)) throw new RequestValidationException(name + " alanı metin olmalıdır.");
            string text = (string)value;
            if (text.Length > limit || text.Any(c => char.IsControl(c) &&
                !(multiline && (c == '\r' || c == '\n' || c == '\t'))))
                throw new RequestValidationException(name + " alanı geçersiz veya çok uzun.");
            return text.Trim();
        }
    }

    internal sealed class WebLeadRejectedException : Exception { }
}
