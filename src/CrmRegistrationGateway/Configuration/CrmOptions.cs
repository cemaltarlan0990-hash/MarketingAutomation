using System;
using System.Configuration;
using System.Data.Common;
using System.Globalization;

namespace RelatedEntegrasyonu.CrmGateway.Configuration
{
    internal sealed class CrmOptions
    {
        public string Url { get; private set; }
        public string ClientId { get; private set; }
        public string ClientSecret { get; private set; }
        public string TenantId { get; private set; }
        public string UserKey { get; private set; }
        public string EnvironmentName { get; private set; }
        public string AllowedHost { get; private set; }
        public bool WritesEnabled { get; private set; }
        public string InboundApiKey { get; private set; }

        // EtkinlikKayit.aspx (Power Automate) için ayrı anahtar.
        public string EventApiKey { get; private set; }

        public string TargetEntity { get; private set; }
        public string FirstNameAttribute { get; private set; }
        public string LastNameAttribute { get; private set; }
        public string EmailAttribute { get; private set; }
        public string PhoneAttribute { get; private set; }
        public string CompanyAttribute { get; private set; }

        // İsteğe bağlı eşlemeler (EtkinlikKayit.aspx). Tanımlı değillerse bu bilgiler
        // Lead açıklamasına yazılır.
        public string JobTitleAttribute { get; private set; }
        public string CityLookupAttribute { get; private set; }
        public string CityEntity { get; private set; }
        public string CityNameAttribute { get; private set; }

        // İsteğe bağlı: web formundaki KVKK onayının yazılacağı Evet/Hayır alanı.
        public string KvkkConsentAttribute { get; private set; }

        // İsteğe bağlı: CRM'deki İYS eklentilerinin okuduğu telefon alanı (iş telefonu).
        public string IysPhoneAttribute { get; private set; }

        // İsteğe bağlı: Türkiye sabit hatlarının (05 dışı) yazılacağı iş telefonu alanı.
        // Tanımlıysa cep ve yabancı numaralar yalnız PhoneAttribute'a yazılır.
        public string BusinessPhoneAttribute { get; private set; }

        // İsteğe bağlı: Müşteri Adayı Kaynağı seçenek alanı ve etiket gelmezse kullanılacak varsayılan.
        public string LeadSourceAttribute { get; private set; }
        public string DefaultLeadSource { get; private set; }

        // İsteğe bağlı: Kaynak Kampanya bağlantısı (kampanya adıyla eşleştirilir).
        public string CampaignLookupAttribute { get; private set; }
        public string CampaignEntity { get; private set; }
        public string CampaignNameAttribute { get; private set; }
        // Doluysa web formu kayıtları formdan gelen ad yerine her zaman bu kampanyaya bağlanır.
        public string WebFormCampaign { get; private set; }

        // İsteğe bağlı: formdaki kanal kutucuklarının Evet/Hayır olarak yazılacağı Lead alanları.
        public string EmailPermissionAttribute { get; private set; }
        public string SmsPermissionAttribute { get; private set; }
        public string CallPermissionAttribute { get; private set; }

        // Kanal onaylarının CRM'deki mevcut İYS eklentileri üzerinden işlenmesi.
        public bool IysConsentEnabled { get; private set; }

        // İsteğe bağlı: etkinlik formu adresinin yazılacağı Lead alanı.
        public string EventUrlAttribute { get; private set; }

        // Sunucudan gelen etkinlik adresi için kabul edilen kökler (virgülle ayrılmış).
        // Tanımlı değilse yalnızca https://altium.net kabul edilir.
        public string[] EventUrlOrigins { get; private set; }

        public bool HasCampaignLookup
        {
            get
            {
                return !string.IsNullOrWhiteSpace(CampaignLookupAttribute) &&
                       !string.IsNullOrWhiteSpace(CampaignEntity) &&
                       !string.IsNullOrWhiteSpace(CampaignNameAttribute);
            }
        }

        public bool HasCityLookup
        {
            get
            {
                return !string.IsNullOrWhiteSpace(CityLookupAttribute) &&
                       !string.IsNullOrWhiteSpace(CityEntity) &&
                       !string.IsNullOrWhiteSpace(CityNameAttribute);
            }
        }

        public static CrmOptions Load()
        {
            return new CrmOptions
            {
                // The first app-setting names preserve the naming convention used by
                // the existing Related/Altium code. The Crm.* names remain supported.
                Url = Read(new[] { "CRM_URL" }, "crmserverurlaltium", "Crm.Url"),
                UserKey = Read(new[] { "CRM_USER_KEY" }, "crmuseraltium", "Crm.UserKey"),
                ClientId = Read(new[] { "CRM_CLIENT_ID", "CLIENT_ID" }, "Crm.ClientId"),
                ClientSecret = Read(new[] { "CRM_CLIENT_SECRET", "CLIENT_SECRET" }, "Crm.ClientSecret"),
                TenantId = Read(new[] { "CRM_TENANT_ID", "TENANT_ID" }, "Crm.TenantId"),
                EnvironmentName = Read(new[] { "CRM_ENVIRONMENT" }, "Crm.EnvironmentName"),
                AllowedHost = Read(new[] { "CRM_ALLOWED_HOST" }, "Crm.AllowedHost"),
                WritesEnabled = ReadBoolean(new[] { "CRM_WRITES_ENABLED" }, "Crm.WritesEnabled"),
                InboundApiKey = Read(new[] { "CRM_INBOUND_API_KEY", "INBOUND_API_KEY" }, "Crm.InboundApiKey"),
                EventApiKey = Read(new[] { "CRM_EVENT_API_KEY", "EtkinlikApiKey" }, "Crm.EventApiKey", "EtkinlikApiKey"),

                TargetEntity = Read(new[] { "CRM_TARGET_ENTITY" }, "Crm.TargetEntityLogicalName"),
                FirstNameAttribute = Read(new[] { "CRM_FIRSTNAME_ATTRIBUTE" }, "Crm.FirstNameAttribute"),
                LastNameAttribute = Read(new[] { "CRM_LASTNAME_ATTRIBUTE" }, "Crm.LastNameAttribute"),
                EmailAttribute = Read(new[] { "CRM_EMAIL_ATTRIBUTE" }, "Crm.EmailAttribute"),
                PhoneAttribute = Read(new[] { "CRM_PHONE_ATTRIBUTE" }, "Crm.PhoneAttribute"),
                CompanyAttribute = Read(new[] { "CRM_COMPANY_ATTRIBUTE" }, "Crm.CompanyAttribute"),

                JobTitleAttribute = Read(new[] { "CRM_JOBTITLE_ATTRIBUTE" }, "Crm.JobTitleAttribute"),
                CityLookupAttribute = Read(new[] { "CRM_CITY_LOOKUP_ATTRIBUTE" }, "Crm.CityLookupAttribute"),
                CityEntity = Read(new[] { "CRM_CITY_ENTITY" }, "Crm.CityEntityLogicalName"),
                CityNameAttribute = Read(new[] { "CRM_CITY_NAME_ATTRIBUTE" }, "Crm.CityNameAttribute"),
                KvkkConsentAttribute = Read(new[] { "CRM_KVKK_ATTRIBUTE" }, "Crm.KvkkConsentAttribute"),
                IysPhoneAttribute = Read(new[] { "CRM_IYS_PHONE_ATTRIBUTE" }, "Crm.IysPhoneAttribute"),
                BusinessPhoneAttribute = Read(new[] { "CRM_BUSINESS_PHONE_ATTRIBUTE" }, "Crm.BusinessPhoneAttribute"),
                LeadSourceAttribute = Read(new[] { "CRM_LEAD_SOURCE_ATTRIBUTE" }, "Crm.LeadSourceAttribute"),
                DefaultLeadSource = Read(new[] { "CRM_DEFAULT_LEAD_SOURCE" }, "Crm.DefaultLeadSource"),
                CampaignLookupAttribute = Read(new[] { "CRM_CAMPAIGN_LOOKUP_ATTRIBUTE" }, "Crm.CampaignLookupAttribute"),
                CampaignEntity = Read(new[] { "CRM_CAMPAIGN_ENTITY" }, "Crm.CampaignEntityLogicalName"),
                CampaignNameAttribute = Read(new[] { "CRM_CAMPAIGN_NAME_ATTRIBUTE" }, "Crm.CampaignNameAttribute"),
                WebFormCampaign = Read(new[] { "CRM_WEB_FORM_CAMPAIGN" }, "Crm.WebFormCampaign"),
                EmailPermissionAttribute = Read(new[] { "CRM_EMAIL_PERMISSION_ATTRIBUTE" }, "Crm.EmailPermissionAttribute"),
                SmsPermissionAttribute = Read(new[] { "CRM_SMS_PERMISSION_ATTRIBUTE" }, "Crm.SmsPermissionAttribute"),
                CallPermissionAttribute = Read(new[] { "CRM_CALL_PERMISSION_ATTRIBUTE" }, "Crm.CallPermissionAttribute"),
                IysConsentEnabled = ReadBoolean(new[] { "CRM_IYS_CONSENT_ENABLED" }, "Crm.IysConsentEnabled"),
                EventUrlAttribute = Read(new[] { "CRM_EVENT_URL_ATTRIBUTE" }, "Crm.EventUrlAttribute"),
                EventUrlOrigins = ReadList(new[] { "CRM_EVENT_URL_ORIGINS" }, "Crm.EventUrlOrigins", "https://altium.net")
            };
        }

        private static string[] ReadList(string[] environmentVariables, string appSetting, string fallback)
        {
            string value = Read(environmentVariables, appSetting);
            string[] items = (string.IsNullOrWhiteSpace(value) ? fallback : value)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < items.Length; index++)
                items[index] = items[index].Trim().TrimEnd('/');
            return items;
        }

        public void ValidateForConnection()
        {
            Require(Url, "CRM URL");
            Require(ClientId, "CRM Client ID");
            Require(ClientSecret, "CRM Client Secret");
            Require(EnvironmentName, "CRM environment name");
            Require(AllowedHost, "CRM allowed host");
            Require(InboundApiKey, "inbound API key");

            if (!string.Equals(EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase))
                throw new ConfigurationErrorsException("CRM environment must be explicitly set to Test.");

            Uri crmUri;
            if (!Uri.TryCreate(Url, UriKind.Absolute, out crmUri) ||
                !string.Equals(crmUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new ConfigurationErrorsException("CRM URL must be a valid HTTPS address.");

            if (!string.Equals(crmUri.Host, AllowedHost, StringComparison.OrdinalIgnoreCase))
                throw new ConfigurationErrorsException("CRM URL is not the configured test host.");

            Guid parsedClientId;
            if (!Guid.TryParse(ClientId, out parsedClientId))
                throw new ConfigurationErrorsException("CRM Client ID is invalid.");

            if (!string.IsNullOrWhiteSpace(TenantId))
            {
                Guid parsedTenantId;
                if (!Guid.TryParse(TenantId, out parsedTenantId))
                    throw new ConfigurationErrorsException("CRM Tenant ID is invalid.");
            }

        }

        public void ValidateForWrite()
        {
            ValidateForConnection();

            if (!WritesEnabled)
                throw new ConfigurationErrorsException("CRM writes are disabled.");

            Require(TargetEntity, "target entity logical name");
            Require(FirstNameAttribute, "first-name attribute logical name");
            Require(LastNameAttribute, "last-name attribute logical name");
            Require(EmailAttribute, "email attribute logical name");

        }

        public string BuildConnectionString()
        {
            var builder = new DbConnectionStringBuilder();
            builder["AuthType"] = "ClientSecret";
            builder["Url"] = Url;
            builder["ClientId"] = ClientId;
            builder["ClientSecret"] = ClientSecret;
            builder["SkipDiscovery"] = true;
            builder["RequireNewInstance"] = true;
            builder["LoginPrompt"] = "Never";

            // Kept for compatibility with the reference Altium connection config.
            // ClientId, not UserName, determines the Dataverse application user.
            if (!string.IsNullOrWhiteSpace(UserKey))
                builder["UserName"] = UserKey;

            if (!string.IsNullOrWhiteSpace(TenantId))
                builder["TenantId"] = TenantId;

            return builder.ConnectionString;
        }

        private static string Read(string[] environmentVariables, params string[] appSettings)
        {
            string value = null;
            foreach (string environmentVariable in environmentVariables)
            {
                value = Environment.GetEnvironmentVariable(environmentVariable);
                if (!string.IsNullOrWhiteSpace(value))
                    break;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                foreach (string appSetting in appSettings)
                {
                    value = ConfigurationManager.AppSettings[appSetting];
                    if (!string.IsNullOrWhiteSpace(value))
                        break;
                }
            }

            return value == null ? null : value.Trim();
        }

        private static bool ReadBoolean(string[] environmentVariables, params string[] appSettings)
        {
            string value = Read(environmentVariables, appSettings);
            bool parsed;
            return bool.TryParse(value, out parsed) && parsed;
        }

        private static void Require(string value, string settingName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ConfigurationErrorsException(
                    string.Format(CultureInfo.InvariantCulture, "Missing {0} configuration.", settingName));
            }
        }
    }
}
