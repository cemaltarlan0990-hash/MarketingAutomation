using System;
using System.Net.Mail;
using RelatedEntegrasyonu.CrmGateway.Infrastructure;

namespace RelatedEntegrasyonu.CrmGateway.Models
{
    internal sealed class CrmRegistrationRequest
    {
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Email { get; private set; }
        public string Phone { get; private set; }
        public string Company { get; private set; }
        public string EventId { get; private set; }

        public static CrmRegistrationRequest Create(
            string firstName,
            string lastName,
            string email,
            string phone,
            string company,
            string eventId)
        {
            var request = new CrmRegistrationRequest
            {
                FirstName = Normalize(firstName, 100, "firstname"),
                LastName = Normalize(lastName, 100, "lastname"),
                Email = Normalize(email, 254, "email"),
                Phone = Normalize(phone, 50, "phone"),
                Company = Normalize(company, 200, "company"),
                EventId = Normalize(eventId, 100, "eventId")
            };

            Require(request.FirstName, "Ad zorunludur.");
            Require(request.LastName, "Soyad zorunludur.");
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
