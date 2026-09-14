using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using Microsoft.Crm.Sdk.Messages;
using RelatedEntegrasyonu.CrmGateway.Configuration;
using RelatedEntegrasyonu.CrmGateway.Services;

namespace RelatedEntegrasyonu.CrmGateway
{
    public partial class TestCrmConnection : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string correlationId = Guid.NewGuid().ToString("N");
            PrepareResponse(correlationId);

            if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                Response.AddHeader("Allow", "POST");
                WriteJson(405, false, "Yalnızca POST isteği desteklenmektedir.", null, correlationId);
                return;
            }

            try
            {
                CrmOptions options = CrmOptions.Load();
                if (!HasValidApiKey(options.InboundApiKey, Request.Headers["X-Integration-Key"]))
                {
                    WriteJson(401, false, "Yetkisiz istek.", null, correlationId);
                    return;
                }

                WhoAmIResponse whoAmI = CrmConnection.ExecuteReadOnly(
                    options,
                    service => (WhoAmIResponse)service.Execute(new WhoAmIRequest()));

                var details = new
                {
                    userId = whoAmI.UserId,
                    businessUnitId = whoAmI.BusinessUnitId,
                    organizationId = whoAmI.OrganizationId
                };

                WriteJson(200, true, "CRM test bağlantısı başarılı.", details, correlationId);
            }
            catch (ConfigurationErrorsException ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "CRM connection-test configuration error. CorrelationId={0}; Error={1}",
                    correlationId,
                    ex);
                WriteJson(503, false, "CRM test bağlantısı henüz yapılandırılmadı.", null, correlationId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "CRM connection test failed. CorrelationId={0}; Error={1}",
                    correlationId,
                    ex);
                WriteJson(502, false, "CRM test bağlantısı kurulamadı.", null, correlationId);
            }
        }

        private void PrepareResponse(string correlationId)
        {
            Response.Clear();
            Response.BufferOutput = true;
            Response.ContentType = "application/json";
            Response.ContentEncoding = Encoding.UTF8;
            Response.Charset = "utf-8";
            Response.TrySkipIisCustomErrors = true;
            Response.SuppressFormsAuthenticationRedirect = true;
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.AddHeader("X-Correlation-ID", correlationId);
        }

        private static bool HasValidApiKey(string expected, string submitted)
        {
            if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(submitted))
                return false;

            byte[] expectedHash;
            byte[] submittedHash;
            using (SHA256 sha = SHA256.Create())
            {
                expectedHash = sha.ComputeHash(Encoding.UTF8.GetBytes(expected));
                submittedHash = sha.ComputeHash(Encoding.UTF8.GetBytes(submitted));
            }

            int difference = expectedHash.Length ^ submittedHash.Length;
            int length = Math.Min(expectedHash.Length, submittedHash.Length);
            for (int index = 0; index < length; index++)
                difference |= expectedHash[index] ^ submittedHash[index];

            return difference == 0;
        }

        private void WriteJson(
            int statusCode,
            bool success,
            string message,
            object details,
            string correlationId)
        {
            Response.StatusCode = statusCode;
            var payload = new
            {
                success = success,
                message = message,
                details = details,
                correlationId = correlationId
            };

            Response.Write(new JavaScriptSerializer().Serialize(payload));
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
