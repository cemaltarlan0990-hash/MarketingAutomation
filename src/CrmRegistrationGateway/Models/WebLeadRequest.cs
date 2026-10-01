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
        public bool? MarketingConsent { get; private set; }
        public bool EmailConsent { get; private set; }
        public bool SmsConsent { get; private set; }
        public bool PhoneConsent { get; private set; }

        public static WebLeadRequest FromJson(string json)
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
            string captcha = Text(data, "captchaToken", 2048, false);
            if (string.IsNullOrWhiteSpace(captcha)) throw new WebLeadRejectedException();
            return new WebLeadRequest
            {
                Registration = CrmRegistrationRequest.Create(firstName, lastName, email, phone, company, null),
                Message = message,
                CaptchaToken = captcha,
                JobTitle = Text(data, "jobTitle", 100, false),
                City = Text(data, "city", 100, false),
                EventTitle = eventTitle,
                EventUrl = eventUrl,
                MarketingConsent = marketing,
                EmailConsent = emailConsent,
                SmsConsent = smsConsent,
                PhoneConsent = phoneConsent
            };
        }

        public void PrepareCrmRecord(string consentVersion, DateTime receivedAtUtc)
        {
            // The form's acknowledgement is recorded separately from marketing permission.
            // No donotbulkemail/marketing flag is inferred from this checkbox.
            var description = new StringBuilder(Message);
            description.AppendLine().AppendLine();
            AddLine(description, "Unvan", JobTitle);
            AddLine(description, "Şehir", City);
            AddLine(description, "Etkinlik formu", EventUrl);
            if (MarketingConsent.HasValue)
            {
                AddLine(description, "Pazarlama onayı", MarketingConsent.Value ? "Evet" : "Hayır");
                AddLine(description, "E-posta kanalı", EmailConsent ? "Evet" : "Hayır");
                AddLine(description, "SMS kanalı", SmsConsent ? "Evet" : "Hayır");
                AddLine(description, "Telefon kanalı", PhoneConsent ? "Evet" : "Hayır");
            }
            AddLine(description, "Form onayı", "true");
            AddLine(description, "Metin sürümü", consentVersion);
            AddLine(description, "Alınma zamanı (UTC)", receivedAtUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            Registration.SetWebFormDetails(EventTitle.Length == 0 ? "Web sitesi form talebi" : "Etkinlik Kaydı: " + EventTitle,
                description.ToString());
        }

        public void ValidateEventOrigin(string origin)
        {
            if (string.IsNullOrEmpty(EventUrl)) return;
            Uri url;
            if (!Uri.TryCreate(EventUrl, UriKind.Absolute, out url) ||
                url.GetLeftPart(UriPartial.Authority) != origin || !string.IsNullOrEmpty(url.UserInfo) ||
                !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) ||
                !url.AbsolutePath.StartsWith("/tr/etkinlik-kayit/", StringComparison.Ordinal))
                throw new RequestValidationException("Etkinlik form adresi geçersiz.");
            if (string.IsNullOrWhiteSpace(EventTitle))
                throw new RequestValidationException("Etkinlik başlığı zorunludur.");
        }

        private static void AddLine(StringBuilder text, string label, string value)
        {
            if (!string.IsNullOrEmpty(value)) text.Append(label).Append(": ").AppendLine(value);
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
