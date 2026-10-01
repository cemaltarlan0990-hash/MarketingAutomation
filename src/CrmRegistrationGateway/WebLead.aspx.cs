using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using RelatedEntegrasyonu.CrmGateway.Configuration;
using RelatedEntegrasyonu.CrmGateway.Infrastructure;
using RelatedEntegrasyonu.CrmGateway.Models;
using RelatedEntegrasyonu.CrmGateway.Services;

namespace RelatedEntegrasyonu.CrmGateway
{
    public partial class WebLead : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RegisterAsyncTask(new PageAsyncTask(HandleAsync));
        }

        private async Task HandleAsync()
        {
            string correlationId = Guid.NewGuid().ToString("N");
            Response.Clear();
            Response.ContentType = "application/json";
            Response.ContentEncoding = Encoding.UTF8;
            Response.TrySkipIisCustomErrors = true;
            Response.SuppressFormsAuthenticationRedirect = true;
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.AddHeader("X-Correlation-ID", correlationId);
            Response.AddHeader("Vary", "Origin");
            string stage = "request";
            try
            {
                if (Request.HttpMethod != "POST" && Request.HttpMethod != "OPTIONS")
                {
                    Response.AddHeader("Allow", "POST, OPTIONS");
                    Reply(405, false, "Yalnızca POST isteği desteklenmektedir.", correlationId);
                    return;
                }
                // Plain HTTP is allowed only for a local IIS Express test.
                if (!Request.IsSecureConnection && !(Request.IsLocal && Request.Url.IsLoopback))
                { Reply(403, false, "Güvenli bağlantı gereklidir.", correlationId); return; }

                WebLeadOptions web = WebLeadOptions.Load();
                string origin = Request.Headers["Origin"];
                if (!web.Allows(origin))
                { Reply(403, false, "Bu kaynaktan form gönderimi kabul edilmiyor.", correlationId); return; }
                Response.AddHeader("Access-Control-Allow-Origin", origin);
                Response.AddHeader("Access-Control-Expose-Headers", "X-Correlation-ID, Retry-After");
                if (Request.HttpMethod == "OPTIONS")
                {
                    string requestedHeaders = Request.Headers["Access-Control-Request-Headers"] ?? "";
                    foreach (string header in requestedHeaders.Split(','))
                        if (header.Trim().Length > 0 && !header.Trim().Equals("content-type", StringComparison.OrdinalIgnoreCase))
                        { Reply(403, false, "İstek başlığı desteklenmiyor.", correlationId); return; }
                    if (Request.Headers["Access-Control-Request-Method"] != "POST")
                    { Reply(403, false, "İstek yöntemi desteklenmiyor.", correlationId); return; }
                    Response.AddHeader("Access-Control-Allow-Methods", "POST");
                    Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                    Response.StatusCode = 204;
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
                string mediaType = (Request.ContentType ?? "").Split(';')[0].Trim();
                if (!mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                { Reply(415, false, "Content-Type application/json olmalıdır.", correlationId); return; }
                if (Request.ContentLength > 16384)
                { Reply(413, false, "Form verisi çok büyük.", correlationId); return; }
                int retryAfter;
                // Do not trust user-supplied forwarding headers for per-address limits.
                if (!WebLeadRateLimiter.TryAcquire(Request.UserHostAddress, out retryAfter))
                {
                    Response.AddHeader("Retry-After", retryAfter.ToString());
                    Reply(429, false, "Çok fazla gönderim yapıldı. Bir süre sonra tekrar deneyin.", correlationId);
                    return;
                }
                string json = ReadBody();
                WebLeadRequest data = WebLeadRequest.FromJson(json);
                data.ValidateEventOrigin(origin);
                stage = "configuration";
                web.ValidateForSubmission();
                CrmOptions crm = CrmOptions.Load();
                crm.ValidateForWrite();
                // Validate total CRM field lengths before consuming the single-use CAPTCHA.
                data.PrepareCrmRecord(web.ConsentVersion, DateTime.UtcNow);
                stage = "captcha";
                if (!await TurnstileVerifier.VerifyAsync(web.CaptchaSecret, data.CaptchaToken, new Uri(origin).Host))
                { Reply(403, false, "Güvenlik doğrulaması başarısız. Doğrulamayı yenileyin.", correlationId); return; }
                stage = "crm";
                CrmConnection.Execute(crm, service => CrmRegistrationWriter.Write(service, crm, data.Registration));
                // Public callers do not need internal CRM record identifiers.
                System.Diagnostics.Trace.TraceInformation("Web lead created. CorrelationId={0}", correlationId);
                Reply(201, true, "Talebiniz alındı, teşekkürler.", correlationId);
            }
            catch (WebLeadBodyTooLargeException) { Reply(413, false, "Form verisi çok büyük.", correlationId); }
            catch (RequestValidationException ex) { Reply(400, false, ex.Message, correlationId); }
            catch (WebLeadRejectedException) { Reply(403, false, "Form güvenlik doğrulaması başarısız.", correlationId); }
            catch (ConfigurationErrorsException ex)
            {
                LogFailure(correlationId, stage, ex);
                Reply(503, false, "Form servisi şu anda kullanılamıyor.", correlationId);
            }
            catch (Exception ex)
            {
                LogFailure(correlationId, stage, ex);
                Reply(stage == "crm" ? 502 : 503, false, "Talebiniz tamamlanamadı. Lütfen daha sonra tekrar deneyin.", correlationId);
            }
        }

        private string ReadBody()
        {
            using (var buffer = new MemoryStream())
            {
                byte[] chunk = new byte[1024];
                int count;
                while ((count = Request.InputStream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (buffer.Length + count > 16384)
                        throw new WebLeadBodyTooLargeException();
                    buffer.Write(chunk, 0, count);
                }
                try { return new UTF8Encoding(false, true).GetString(buffer.ToArray()); }
                catch (DecoderFallbackException) { throw new RequestValidationException("Gövde UTF-8 olmalıdır."); }
            }
        }

        private static void LogFailure(string id, string stage, Exception ex)
        {
            System.Diagnostics.Trace.TraceError("Web lead failed. CorrelationId={0}; Stage={1}; Type={2}", id, stage, ex.GetType().Name);
        }

        private void Reply(int status, bool success, string message, string id)
        {
            Response.StatusCode = status;
            Response.Write(new JavaScriptSerializer().Serialize(new { success, message, correlationId = id }));
            Context.ApplicationInstance.CompleteRequest();
        }
    }

    internal sealed class WebLeadBodyTooLargeException : Exception { }
}
