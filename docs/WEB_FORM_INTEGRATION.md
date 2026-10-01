# Web formu → ASPX → CRM Lead

Mevcut `INBOUND_API_KEY` ile site sunucusundan gönderim için
[EVENT_SERVER_FORWARDING.md](EVENT_SERVER_FORWARDING.md) belgesini uygulayın.
Aşağıdaki bölüm doğrudan tarayıcıdan gönderim seçeneğini anlatır.

Yeni giriş aynı .NET Framework 4.8 uygulamasında `POST /WebLead.aspx` adresidir.
Tarayıcı JSON gönderir. Bu sayfa doğrulamadan sonra mevcut `CrmConnection` ve
`CrmRegistrationWriter` koduyla Lead oluşturur. Yeni Node.js servisi gerekmez.

Gerçek Altium etkinlik formu, yeni alanlar ve GitHub push adımları için
[devreye alma rehberini](ALTIUM_EVENT_FORM_GO_LIVE.md) kullanın.
`altium-event-form.js` gerçek `reg-form-app` içindir; `contactForm` aşağıdaki
genel örneğin kimliğidir.

## Endpoint ve gönderilecek veri

Güncel paket Azure'a yayımlandıktan sonra kullanılacak adres:

```text
https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net/WebLead.aspx
```

```http
POST /WebLead.aspx
Content-Type: application/json
Origin: https://www.SITENIZ.com
```

```json
{
  "firstName": "Ahmet",
  "lastName": "Yılmaz",
  "email": "ahmet@example.com",
  "phone": "+905551112233",
  "company": "ABC",
  "message": "Ürün hakkında bilgi almak istiyorum.",
  "consent": true,
  "captchaToken": "GERCEK_TURNSTILE_TOKEN",
  "website": ""
}
```

Telefon isteğe bağlıdır; mevcut JS'de yoksa boş gönderilebilir. Ad/soyad/e-posta
zorunludur. `consent` gerçek JSON boolean `true` olmalıdır; `"true"`, `1`, `false`
ve eksik değerler kabul edilmez. Alanlar metin olmalıdır; otomatik tip dönüşümü yoktur.

Limitler: ad 50, soyad 50, e-posta 100, telefon 50, firma 100, mesaj 1500,
CAPTCHA token 2048 karakter; istek gövdesi en fazla 16 KiB UTF-8.

CRM eşlemesi mevcut ayarları kullanır:

| JSON | Varsayılan Lead alanı |
| --- | --- |
| firstName | firstname |
| lastName | lastname |
| email | emailaddress1 |
| phone | mobilephone |
| company | companyname |
| message | description |
| Sabit konu | subject = Web sitesi form talebi |

Form onayı, sunucuda tanımlı metin sürümü ve alınma zamanı UTC olarak açıklamanın
sonuna eklenir. Bu, sunucunun gelen onay beyanını kaydetmesidir; pazarlama izni
olarak yorumlanmaz. `donotbulkemail` değiştirilmez. CAPTCHA token, honeypot ve
IP adresi CRM kaydına yazılmaz. Lead Source için doğrulanmamış `8` değeri kullanılmaz.

## Azure'da gerekli yeni ayarlar

Azure Portal → App Services → ALTTR-MarketingAutomation → Settings →
Environment variables / App settings bölümüne aşağıdakileri ekleyip kaydedin:

| Ortam değişkeni | Açıklama |
| --- | --- |
| WEB_LEAD_ALLOWED_ORIGINS | Gerçek frontend origin'i; ör. https://www.sirketiniz.com |
| TURNSTILE_SECRET_KEY | CAPTCHA sağlayıcısının SECRET anahtarı; yalnız sunucuda tutulur |
| WEB_LEAD_CONSENT_VERSION | Formdaki yayınlanmış onay metninin sürümü; ör. contact-2026-09-v1 |

Birden fazla origin virgülle ayrılır. Origin sonunda `/` bulunmaz; path ve wildcard
kabul edilmez. `https://sirketiniz.com` ile `https://www.sirketiniz.com` farklıdır.
Geliştirme origin'i gerekiyorsa ayrıca `http://localhost:3000` eklenir; canlıda
gereksiz geliştirme origin'lerini kaldırın. Başlıkta `Origin` yoksa istek reddedilir.
Tarayıcı bu başlığı kendisi ekler; JS'de elle ayarlamayın.

Mevcut CRM ayarları kullanılmaya devam eder: CRM_URL, CRM_CLIENT_ID/CLIENT_ID,
CRM_CLIENT_SECRET/CLIENT_SECRET, CRM_TENANT_ID/TENANT_ID,
CRM_INBOUND_API_KEY/INBOUND_API_KEY ve CRM_WRITES_ENABLED. Mevcut bağlantı
doğrulaması inbound anahtarın sunucuda tanımlı olmasını da bekler. Bu anahtar
`WebLead.aspx` isteğinde gönderilmez. CRM_ENVIRONMENT Test ve CRM_ALLOWED_HOST
altiumtr-test.crm4.dynamics.com kısıtları korunmuştur. Canlı CRM'e geçiş bu
değişikliğin kapsamında yapılmamıştır.

Secret'ı sohbet, HTML, JS, Git veya yayın ZIP'ine koymayın. Sunucuda App Settings
ya da mevcut Key Vault reference kullanın. Kaynak Web.config ve paket izinli
origin olarak https://altium.net içerir; CAPTCHA secret ve metin sürümü boştur.
Eksik güvenlik ayarları isteği güvenli şekilde durdurur.

## CAPTCHA

Bu uygulama Cloudflare Turnstile kullanır. Cloudflare hesabında Turnstile widget
oluşturup gerçek sitenin hostname'ini izinli olarak tanımlayın. PUBLIC site key'i
HTML'deki `data-sitekey` alanına; SECRET key'i Azure ayarına yerleştirin.
HTML widget'ında `data-action="web-lead"` bulunmalıdır.

Backend Siteverify API'sini çağırır ve `success`, `hostname`, `action` değerlerini
kontrol eder. Hostname gönderimin izin verilen origin'iyle, action `web-lead` ile
eşleşmelidir. Token tek kullanımlıdır ve 5 dakika geçerlidir; her gönderim sonrası
widget yenilenir. Sağlayıcıya erişilemiyorsa CRM kaydı oluşturulmaz.
Resmi kaynak: [Turnstile server-side validation](https://developers.cloudflare.com/turnstile/get-started/server-side-validation/).

Sağlayıcının dummy test secret'ları bu herkese açık endpoint'te reddedilir.
Otomatik testlerde CAPTCHA yanıt kontrolü ve CRM writer'ı bağımsız olarak sınanır;
üretimde CAPTCHA atlama anahtarı bulunmaz.

Mevcut `getCaptchaToken()` reCAPTCHA token üretiyorsa bunu Turnstile token'ıyla
değiştirin; sağlayıcılar birbirinin token'ını doğrulamaz. JS örneği mevcut
`getCaptchaToken()` fonksiyonu varsa çağırır; yoksa widget'ın oluşturduğu
`cf-turnstile-response` alanını okur.

## Mevcut formu bağlama

1. `examples/web-form/contact-form.js` dosyasını web sitesine yerleştirin.
2. Önceki submit listener'ını kaldırın; iki farklı gönderim kodunu birlikte bırakmayın.
3. Formun id'si `contactForm`; alan adları yukarıdaki JSON anahtarları olsun.
4. Form etiketine aşağıdaki attribute'u ekleyin:

```html
data-api-url="https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net/WebLead.aspx"
```

5. Turnstile script/widget'ını ve onaylı form metnini ekleyin.
6. JS dosyasını `defer` ile yükleyin. Sayfanın CSP'si bu API'ye connect-src,
Turnstile script/frame bağlantılarına da gerekli izinleri vermelidir.

Tam HTML örneği `examples/web-form/contact-form.html` içindedir. Onay metni örnektir;
kullanacağınız gerçek metni yerleştirin. HTML'deki PUBLIC site key placeholder'ı
değiştirilmeden CAPTCHA çalışmaz. HTML dosyasını `file://` ile açmak yerine
izin verilen web origin'inden sunun. Bu frontend örnekleri ASPX paketine dahil
edilmez; web sitesine yerleştirilir.

Buton işlem boyunca kilitlenir. CAPTCHA hatası dahil tüm yollarda yeniden açılır.
Form yalnız HTTP başarı ve JSON `success:true` birlikte geldiğinde temizlenir.
Ham sunucu hatası veya CRM kimliği kullanıcıya gösterilmez.

## Güvenlik, CORS ve gönderim limiti

CORS kod tarafından yalnız izin verilen origin'e verilir. IIS'in OPTIONS isteğini
ASPX'e yönlendirmesi için Web.config'de yalnız WebLead.aspx'e özel handler vardır.
Azure App Service CORS'unda wildcard kullanmayın; ikinci bir CORS katmanı
çakışması oluşturmayın. App Service'te HTTPS Only açık olsun. Endpoint yalnız
yerel IIS Express bağlantıları için HTTP'ye izin verir.

CORS kimlik doğrulama değildir: tarayıcı dışı araçlar Origin başlığını taklit
edebilir. Gerçek spam koruması CAPTCHA, honeypot, doğrulamalar ve limitlerden gelir.

Kodda uygulama süreci başına dakikada 30 istek ve bağlantının uzak adresi başına
dakikada 5 istek sınırı bulunur. Sayaç bellektedir; süreç yeniden başlatıldığında
sıfırlanır, birden fazla worker için ortak değildir. App Service önündeki proxy
nedeniyle uzak adres proxy olabilir; bu durumda kullanıcılar adres limitini paylaşır.
İstemcinin gönderdiği X-Forwarded-For başlığına güvenilmez. Çoklu instance veya
yoğun trafik için sitenin güvenilir gateway/WAF katmanında ortak IP/gönderim
limiti uygulayın. Bu yerel limit dağıtık rate limiting garantisi vermez.

Her kabul edilen yeni form gönderimi yeni Lead oluşturur. JS aynı anda iki
gönderimi önler; kalıcı idempotency yoktur. Bağlantı yanıtı kaybolursa CRM kaydı
oluşmuş olabilir; otomatik tekrar denenmez. Süreçler arasında mükerrer kaydı kesin
önlemek gerekiyorsa ayrı submission ID + CRM alternate key tasarımı gerekir.

Yeni endpoint logları correlationId, aşama ve exception tipini içerir; payload,
mesaj, secret, token ve tam exception loglanmaz. Eski anahtarlı endpoint'lerin
mevcut loglama davranışı bu değişiklikte yeniden tasarlanmamıştır.

## Test ve yayın

```powershell
./scripts/Build-AspxPackage.ps1
./scripts/Test-AspxPackage.ps1 -PackageRoot '<paket>/wwwroot'
./scripts/Test-WebLeadModel.ps1 -PackageRoot '<paket>/wwwroot'
node --test tests/web-form.test.cjs
node --test tests/altium-event-form.test.cjs
```

Bu testler CRM'e kayıt göndermez. Paket testi CRM yazmalarını kapatır ve yerel
origin tanımlar. Model testi fake IOrganizationService ile alan eşlemesini kontrol
eder. GitHub workflow aynı testleri yayından önce çalıştırır. Test edilmiş kodun
main dalına gönderilmesi mevcut workflow üzerinden Azure yayınını tetikler.

Yayından sonra:

1. Azure'da mevcut CRM ayarlarını ve üç yeni web form ayarını doğrulayın.
2. POST TestCrmConnection.aspx ile mevcut anahtarlı bağlantı kontrolünü çalıştırın.
3. Gerçek sitede PUBLIC site key ve yeni JS'yi yerleştirin.
4. Tarayıcı Network'te OPTIONS 204 ve POST 201 / success:true kontrol edin.
5. TEST CRM'de yeni Lead'i, konu/mesaj/alanları ve onay kaydını kontrol edin.

HTTP sonuçları: 201 başarılı kayıt; 400 hatalı veri; 403 origin veya güvenlik
doğrulaması; 405 yanlış yöntem; 413 çok büyük gövde; 415 yanlış Content-Type;
429 limit; 503 eksik ayar veya CAPTCHA sağlayıcı hatası; 502 CRM işlem hatası.
Correlation ID üzerinden sunucu loglarıyla ilişkilendirin.

## Doğrulama durumu

30 Eylül 2026: Release paket derlemesi, 21 IIS Express endpoint kontrolü,
41 model/güvenlik/CRM eşleme kontrolü ve 6 JS testi geçti. Gerçek CAPTCHA
doğrulaması, Azure yayını ve bu yeni endpoint'ten gerçek CRM kaydı bu geliştirme
sırasında yapılmadı. Gerçek site origin'i ve CAPTCHA ayarları henüz sağlanmadı.
