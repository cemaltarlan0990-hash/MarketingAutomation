using System;
using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Web.Script.Serialization;
using RelatedEntegrasyonu.CrmGateway.Infrastructure;

namespace RelatedEntegrasyonu.CrmGateway.Models
{
    /// <summary>
    /// Power Automate "Kayit" Compose çıktısının birebir karşılığı.
    /// Alan adları paketteki JSON adlarıyla aynı olmalıdır.
    /// </summary>
    public sealed class EtkinlikKaydiPayload
    {
        public string baslik { get; set; }
        public string etkinlikTarihi { get; set; }
        public string lokasyon { get; set; }
        public string adSoyad { get; set; }
        public string eposta { get; set; }
        public string telefon { get; set; }
        public string firma { get; set; }
        public string unvan { get; set; }
        public string sehir { get; set; }
        public string not { get; set; }
        public string kvkk { get; set; }
        public string pazarlama { get; set; }
        public string kayitTarihi { get; set; }
    }

    /// <summary>
    /// Doğrulanmış ve CRM'e yazılmaya hazır etkinlik kaydı.
    /// </summary>
    internal sealed class EtkinlikKaydiRequest
    {
        private const int DescriptionMaximumLength = 2000;
        private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string FullName { get; private set; }
        public string Email { get; private set; }
        public string Phone { get; private set; }
        public string Company { get; private set; }
        public string JobTitle { get; private set; }
        public string City { get; private set; }
        public string EventTitle { get; private set; }
        public string EventDate { get; private set; }
        public string EventLocation { get; private set; }
        public string Note { get; private set; }
        public string KvkkText { get; private set; }
        public string MarketingText { get; private set; }
        public string RegisteredAt { get; private set; }
        public bool MarketingConsent { get; private set; }

        /// <summary>CRM'deki Lead konusu; mükerrer kontrolünde de kullanılır.</summary>
        public string Subject
        {
            get
            {
                return string.IsNullOrEmpty(EventTitle)
                    ? "Etkinlik Kaydı"
                    : "Etkinlik Kaydı: " + EventTitle;
            }
        }

        /// <summary>"İSTANBUL" -> "İstanbul"; CRM'deki şehir adlarıyla eşleşmesi için.</summary>
        public string CityForLookup
        {
            get
            {
                return string.IsNullOrEmpty(City)
                    ? City
                    : Turkish.TextInfo.ToTitleCase(City.ToLower(Turkish));
            }
        }

        public static EtkinlikKaydiRequest FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new RequestValidationException("İstek gövdesi boş olamaz.");

            EtkinlikKaydiPayload payload;
            try
            {
                payload = new JavaScriptSerializer().Deserialize<EtkinlikKaydiPayload>(json);
            }
            catch (ArgumentException)
            {
                throw new RequestValidationException("İstek gövdesi geçerli bir JSON değil.");
            }
            catch (InvalidOperationException)
            {
                throw new RequestValidationException("İstek gövdesi beklenen yapıda değil.");
            }

            if (payload == null)
                throw new RequestValidationException("İstek gövdesi boş olamaz.");

            return Create(payload);
        }

        public static EtkinlikKaydiRequest Create(EtkinlikKaydiPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException("payload");

            string fullName = Normalize(payload.adSoyad, 200, "adSoyad");
            string firstName;
            string lastName;
            SplitFullName(fullName, out firstName, out lastName);

            var request = new EtkinlikKaydiRequest
            {
                FullName = fullName,
                FirstName = firstName,
                LastName = lastName,
                Email = Normalize(payload.eposta, 254, "eposta"),
                Phone = Normalize(payload.telefon, 50, "telefon"),
                Company = Normalize(payload.firma, 200, "firma"),
                JobTitle = Normalize(payload.unvan, 200, "unvan"),
                City = Normalize(payload.sehir, 100, "sehir"),
                EventTitle = Normalize(payload.baslik, 250, "baslik"),
                EventDate = Normalize(payload.etkinlikTarihi, 50, "etkinlikTarihi"),
                EventLocation = Normalize(payload.lokasyon, 200, "lokasyon"),
                Note = Normalize(payload.not, 1000, "not"),
                KvkkText = Normalize(payload.kvkk, 50, "kvkk"),
                MarketingText = Normalize(payload.pazarlama, 50, "pazarlama"),
                RegisteredAt = Normalize(payload.kayitTarihi, 50, "kayitTarihi")
            };
            request.MarketingConsent = IsYes(request.MarketingText);

            Require(request.FullName, "Ad soyad zorunludur.");
            Require(request.Email, "E-posta zorunludur.");
            try
            {
                var parsed = new MailAddress(request.Email);
                if (!string.Equals(parsed.Address, request.Email, StringComparison.OrdinalIgnoreCase))
                    throw new FormatException();
            }
            catch (FormatException)
            {
                throw new RequestValidationException("Geçerli bir e-posta adresi gönderilmelidir.");
            }

            return request;
        }

        /// <summary>Lead açıklamasına yazılacak etkinlik ayrıntıları.</summary>
        public string BuildDescription(bool cityLinked)
        {
            var text = new StringBuilder();
            AppendLine(text, "Etkinlik", EventTitle);
            AppendLine(text, "Etkinlik tarihi", EventDate);
            AppendLine(text, "Lokasyon", EventLocation);
            AppendLine(text, "Ad soyad", FullName);
            AppendLine(text, "Ünvan", JobTitle);
            if (!cityLinked)
                AppendLine(text, "Şehir", City);
            AppendLine(text, "KVKK onayı", KvkkText);
            AppendLine(text, "Ticari ileti (pazarlama) onayı", MarketingText);
            AppendLine(text, "Kayıt tarihi", RegisteredAt);
            AppendLine(text, "Not", Note);
            text.Append("Kaynak: Etkinlik kayıt maili (Power Automate)");

            string description = text.ToString();
            return description.Length <= DescriptionMaximumLength
                ? description
                : description.Substring(0, DescriptionMaximumLength);
        }

        // "Ayşe Nur Yılmaz" -> Ad: "Ayşe Nur", Soyad: "Yılmaz".
        // Tek kelimelik isimde soyad da aynı değeri alır (CRM'de soyad zorunludur).
        private static void SplitFullName(string fullName, out string firstName, out string lastName)
        {
            int lastSpace = fullName.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                firstName = fullName.Substring(0, lastSpace).Trim();
                lastName = fullName.Substring(lastSpace + 1).Trim();
            }
            else
            {
                firstName = fullName;
                lastName = fullName;
            }
        }

        private static bool IsYes(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.StartsWith("Evet", StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendLine(StringBuilder text, string label, string value)
        {
            if (!string.IsNullOrEmpty(value))
                text.Append(label).Append(": ").AppendLine(value);
        }

        private static string Normalize(string value, int maximumLength, string fieldName)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length > maximumLength)
            {
                throw new RequestValidationException(
                    fieldName + " alanı izin verilen uzunluğu aşıyor.");
            }

            return normalized;
        }

        private static void Require(string value, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new RequestValidationException(message);
        }
    }
}
