using System;
using System.Configuration;
using System.Linq;

namespace RelatedEntegrasyonu.CrmGateway.Configuration
{
    internal sealed class WebLeadOptions
    {
        public string[] AllowedOrigins { get; private set; }
        public string CaptchaSecret { get; private set; }
        // Optional Turnstile widget action (data-action). Empty means the action is not checked.
        public string CaptchaAction { get; private set; }
        public string ConsentVersion { get; private set; }

        public static WebLeadOptions Load()
        {
            string origins = Read("WEB_LEAD_ALLOWED_ORIGINS", "WebLead.AllowedOrigins");
            var result = new WebLeadOptions
            {
                AllowedOrigins = (origins ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray(),
                CaptchaSecret = Read("TURNSTILE_SECRET_KEY", "WebLead.TurnstileSecretKey"),
                CaptchaAction = Read("WEB_LEAD_TURNSTILE_ACTION", "WebLead.TurnstileAction"),
                ConsentVersion = Read("WEB_LEAD_CONSENT_VERSION", "WebLead.ConsentVersion")
            };
            foreach (string origin in result.AllowedOrigins)
            {
                Uri uri;
                if (!Uri.TryCreate(origin, UriKind.Absolute, out uri) ||
                    !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
                    !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/" ||
                    origin != uri.GetLeftPart(UriPartial.Authority) ||
                    (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
                    throw new ConfigurationErrorsException("Invalid web form origin configuration.");
            }
            return result;
        }

        public bool Allows(string origin)
        {
            return !string.IsNullOrEmpty(origin) && AllowedOrigins.Contains(origin, StringComparer.Ordinal);
        }

        public void ValidateForSubmission()
        {
            if (AllowedOrigins.Length == 0 || string.IsNullOrWhiteSpace(CaptchaSecret) ||
                CaptchaSecret.StartsWith("<", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(ConsentVersion) || ConsentVersion.Length > 100 ||
                ConsentVersion.Any(char.IsControl))
                throw new ConfigurationErrorsException("Web form security settings are incomplete.");
            // Cloudflare test secrets must never enable the public Azure endpoint.
            if (CaptchaSecret.StartsWith("1x", StringComparison.Ordinal) ||
                CaptchaSecret.StartsWith("2x", StringComparison.Ordinal) ||
                CaptchaSecret.StartsWith("3x", StringComparison.Ordinal))
                throw new ConfigurationErrorsException("Turnstile test secrets are not permitted.");
        }

        private static string Read(string environmentName, string settingName)
        {
            string value = Environment.GetEnvironmentVariable(environmentName);
            if (string.IsNullOrWhiteSpace(value)) value = ConfigurationManager.AppSettings[settingName];
            return value == null ? null : value.Trim();
        }
    }
}
