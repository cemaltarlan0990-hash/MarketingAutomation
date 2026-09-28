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

        public string TargetEntity { get; private set; }
        public string FirstNameAttribute { get; private set; }
        public string LastNameAttribute { get; private set; }
        public string EmailAttribute { get; private set; }
        public string PhoneAttribute { get; private set; }
        public string CompanyAttribute { get; private set; }

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

                TargetEntity = Read(new[] { "CRM_TARGET_ENTITY" }, "Crm.TargetEntityLogicalName"),
                FirstNameAttribute = Read(new[] { "CRM_FIRSTNAME_ATTRIBUTE" }, "Crm.FirstNameAttribute"),
                LastNameAttribute = Read(new[] { "CRM_LASTNAME_ATTRIBUTE" }, "Crm.LastNameAttribute"),
                EmailAttribute = Read(new[] { "CRM_EMAIL_ATTRIBUTE" }, "Crm.EmailAttribute"),
                PhoneAttribute = Read(new[] { "CRM_PHONE_ATTRIBUTE" }, "Crm.PhoneAttribute"),
                CompanyAttribute = Read(new[] { "CRM_COMPANY_ATTRIBUTE" }, "Crm.CompanyAttribute")
            };
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
