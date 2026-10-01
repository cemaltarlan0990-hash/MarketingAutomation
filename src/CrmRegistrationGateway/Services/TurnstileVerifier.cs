using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RelatedEntegrasyonu.CrmGateway.Services
{
    internal static class TurnstileVerifier
    {
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 16384 };

        public static async Task<bool> VerifyAsync(string secret, string token, string expectedHostname)
        {
            using (var body = new FormUrlEncodedContent(new Dictionary<string, string>
            { { "secret", secret }, { "response", token } }))
            using (var response = await Client.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", body))
            {
                if (!response.IsSuccessStatusCode) throw new HttpRequestException("CAPTCHA provider unavailable.");
                string json = await response.Content.ReadAsStringAsync();
                return IsValidResponse(json, expectedHostname);
            }
        }

        internal static bool IsValidResponse(string json, string expectedHostname)
        {
            var data = new JavaScriptSerializer { MaxJsonLength = 16384, RecursionLimit = 8 }
                .Deserialize<Dictionary<string, object>>(json);
            object success, hostname, action;
            return data != null && data.TryGetValue("success", out success) && success is bool && (bool)success &&
                data.TryGetValue("hostname", out hostname) && hostname is string &&
                string.Equals((string)hostname, expectedHostname, StringComparison.OrdinalIgnoreCase) &&
                data.TryGetValue("action", out action) && action is string && (string)action == "web-lead";
        }
    }
}
