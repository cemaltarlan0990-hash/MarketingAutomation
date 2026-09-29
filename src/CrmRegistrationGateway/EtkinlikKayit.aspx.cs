using System;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using RelatedEntegrasyonu.CrmGateway.Configuration;
using RelatedEntegrasyonu.CrmGateway.Infrastructure;
using RelatedEntegrasyonu.CrmGateway.Models;
using RelatedEntegrasyonu.CrmGateway.Services;

namespace RelatedEntegrasyonu.CrmGateway
{
    /// <summary>
    /// Power Automate'in etkinlik kayıt mailinden ürettiği JSON paketini alır ve
    /// TEST CRM'de Lead oluşturur.
    ///
    /// POST /EtkinlikKayit.aspx
    ///   Content-Type: application/json
    ///   x-api-key:    CRM_EVENT_API_KEY / EtkinlikApiKey ayarındaki anahtar
    /// </summary>
    public partial class EtkinlikKayit : Page
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

            if (string.IsNullOrWhiteSpace(Request.ContentType) ||
                !Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(415, false, "Content-Type application/json olmalıdır.", null, correlationId);
                return;
            }

            try
            {
                CrmOptions options = CrmOptions.Load();
                if (!HasValidApiKey(options.EventApiKey, Request.Headers["x-api-key"]))
                {
                    WriteJson(401, false, "Yetkisiz istek.", null, correlationId);
                    return;
                }

                EtkinlikKaydiRequest registration = EtkinlikKaydiRequest.FromJson(ReadBody());

                EtkinlikKaydiResult result = CrmConnection.Execute(
                    options,
                    service => EtkinlikKaydiWriter.Write(service, options, registration));

                string message = result.Created
                    ? "CRM kaydı oluşturuldu."
                    : "Bu kişi bu etkinliğe zaten kayıtlı; yeni kayıt oluşturulmadı.";
                WriteJson(200, true, message, result.CrmId.ToString(), correlationId);
            }
            catch (RequestValidationException ex)
            {
                WriteJson(400, false, ex.Message, null, correlationId);
            }
            catch (ConfigurationErrorsException ex)
            {
                System.Diagnostics.Trace.TraceError("Event registration configuration error. CorrelationId={0}; Error={1}", correlationId, ex);
                WriteJson(503, false, "CRM test servisi henüz kullanıma hazır değil.", null, correlationId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Event registration CRM operation failed. CorrelationId={0}; Error={1}", correlationId, ex);
                WriteJson(502, false, "CRM kaydı oluşturulurken bir hata oluştu.", null, correlationId);
            }
        }

        private string ReadBody()
        {
            Stream input = Request.InputStream;
            if (input.CanSeek)
                input.Position = 0;

            using (var reader = new StreamReader(input, Encoding.UTF8, true, 4096, true))
                return reader.ReadToEnd();
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
            string crmId,
            string correlationId)
        {
            Response.StatusCode = statusCode;
            var payload = new
            {
                success = success,
                message = message,
                crmId = crmId,
                correlationId = correlationId
            };

            Response.Write(new JavaScriptSerializer().Serialize(payload));
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
