using System;
using System.Configuration;
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
    public partial class CreateCrmRegistration : Page
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

            string mediaType = (Request.ContentType ?? "").Split(';')[0].Trim();
            bool isJson = string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase);
            if (!isJson && !string.Equals(mediaType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(415, false, "Content-Type application/json veya application/x-www-form-urlencoded olmalıdır.", null, correlationId);
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

                CrmRegistrationRequest registration;
                if (isJson)
                {
                    const int maxBytes = 16384;
                    byte[] bytes = new byte[maxBytes + 1];
                    int count = 0;
                    int read;
                    while (count < bytes.Length && (read = Request.InputStream.Read(bytes, count, bytes.Length - count)) > 0)
                        count += read;
                    if (count > maxBytes)
                    {
                        WriteJson(413, false, "İstek gövdesi çok büyük.", null, correlationId);
                        return;
                    }
                    string json;
                    try { json = new UTF8Encoding(false, true).GetString(bytes, 0, count); }
                    catch (DecoderFallbackException) { throw new RequestValidationException("İstek UTF-8 olmalıdır."); }
                    WebLeadRequest form = WebLeadRequest.FromAuthenticatedJson(json);
                    form.ValidateEventOrigin(options.EventUrlOrigins);
                    // A missing version is omitted; no consent text version is invented.
                    form.PrepareCrmRecord(WebLeadOptions.Load().ConsentVersion, DateTime.UtcNow);
                    registration = form.Registration;
                }
                else
                {
                    registration = CrmRegistrationRequest.Create(
                        Request.Form["firstname"], Request.Form["lastname"], Request.Form["email"],
                        Request.Form["phone"], Request.Form["company"], Request.Form["eventId"]);
                }

                Guid crmId = CrmConnection.Execute(
                    options,
                    service => CrmRegistrationWriter.Write(service, options, registration));

                WriteJson(200, true, "CRM kaydı oluşturuldu.", crmId.ToString(), correlationId);
            }
            catch (WebLeadRejectedException)
            {
                WriteJson(403, false, "Gönderim kabul edilmedi.", null, correlationId);
            }
            catch (RequestValidationException ex)
            {
                WriteJson(400, false, ex.Message, null, correlationId);
            }
            catch (ConfigurationErrorsException ex)
            {
                System.Diagnostics.Trace.TraceError("CRM configuration error. CorrelationId={0}; ErrorType={1}", correlationId, ex.GetType().Name);
                WriteJson(503, false, "CRM test servisi henüz kullanıma hazır değil.", null, correlationId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("CRM operation failed. CorrelationId={0}; ErrorType={1}", correlationId, ex.GetType().Name);
                WriteJson(502, false, "CRM kaydı oluşturulurken bir hata oluştu.", null, correlationId);
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
