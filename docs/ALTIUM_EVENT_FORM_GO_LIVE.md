# Altium etkinlik formu: GitHub → Azure → CRM

Seçilen akış: **Altium etkinlik formu → Azure WebLead.aspx → CRM Lead**.
Hedef adres:
`https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net/WebLead.aspx`.
Bu belge tarayıcıdan doğrudan gönderimi anlatır. Altium sunucusundan anahtarlı
aktarım alternatifinin belgesi [EVENT_SERVER_FORWARDING.md](EVENT_SERVER_FORWARDING.md).

Bu değişiklik aynı .NET Framework 4.8 ASPX uygulamasını kullanır.
Gerçek `reg-form-app` formunun JSON'u `WebLead.aspx` sayfasına gönderilir;
mevcut CRM bağlantısı TEST Dataverse ortamında Lead oluşturur.

## GitHub'a gönderme

Proje kökünde PowerShell açın. Geçerli dal `main`, GitHub remote adı `github`;
`origin` ise Azure Repos'tur.

```powershell
git status --short
git add -- .github/workflows/main_alttr-marketingautomation.yml README.md PROJECT_CONTEXT.md docs/ASPX_DEPLOYMENT.md docs/WEB_FORM_INTEGRATION.md docs/ALTIUM_EVENT_FORM_GO_LIVE.md examples/web-form scripts/Build-AspxPackage.ps1 scripts/Test-AspxPackage.ps1 scripts/Test-EndpointContract.ps1 scripts/Test-AspxDeployment.ps1 scripts/Test-WebLeadModel.ps1 src/CrmRegistrationGateway/CrmRegistrationGateway.csproj src/CrmRegistrationGateway/Web.config src/CrmRegistrationGateway/WebLead.aspx src/CrmRegistrationGateway/WebLead.aspx.cs src/CrmRegistrationGateway/Configuration/WebLeadOptions.cs src/CrmRegistrationGateway/Infrastructure/WebLeadRateLimiter.cs src/CrmRegistrationGateway/Models/WebLeadRequest.cs src/CrmRegistrationGateway/Models/CrmRegistrationRequest.cs src/CrmRegistrationGateway/Services/TurnstileVerifier.cs src/CrmRegistrationGateway/Services/CrmRegistrationWriter.cs tests/WebLead.Tests.cs tests/web-form.test.cjs tests/altium-event-form.test.cjs
git diff --cached --stat
git commit -m "Connect Altium event form to ASPX CRM lead service"
git push github main
```

GitHub Actions'ta **Build, test and deploy ASPX** çalışmasını izleyin. Workflow
Release paketini derler, HTTP/model/JS testlerini çalıştırır, mevcut yayın secret'ıyla
Azure'a gönderir ve yayımlanan sürümü kontrol eder. Force push kullanmayın.
GitHub yayın secret'ının güncel geçerliliği bu çalışma sırasında kontrol edilmedi.

## Azure'da tamamlanacak ayarlar

App Service `ALTTR-MarketingAutomation` → Environment variables / App settings:

| Ayar | Değer |
| --- | --- |
| WEB_LEAD_ALLOWED_ORIGINS | https://altium.net; paket varsayılanında da tanımlı |
| TURNSTILE_SECRET_KEY | Formun Turnstile widget'ına ait gerçek SECRET anahtarı |
| WEB_LEAD_CONSENT_VERSION | Sitede yayımlanan onay metinlerine karşılık gelen sürüm |

Secret yalnız Azure ayarında ya da mevcut Key Vault reference'ta tutulur. Kod,
sohbet ve frontend'e yazılmaz. Mevcut CRM kimlik ayarları kullanılır.
TEST yazmaları için CRM_WRITES_ENABLED=true gerekir; ortam ayarı paket ayarından
önceliklidir. HTTPS Only açık olsun. Wildcard platform CORS eklemeyin.
CAPTCHA secret veya metin sürümü eksikse endpoint CRM'e yazmaz ve 503 döner.

## Web sitesi şablonuna uygulanacak kod

GitHub push **ASPX uygulamasını ve JS dosyasını Azure'a yayımlar**.
Altium web sitesinin mevcut form şablonunu kendiliğinden değiştirmez.

Uygulanacak Blade parçası `examples/web-form/altium-event-form-integration.blade.php`
dosyasında hazırdır. Paylaşılan gerçek form kaynağında eski listener
`document.getElementById('reg-form-app').addEventListener('submit', ...)`
ile başlıyor; içinde `fetch(storeUrl, ...)` bulunuyor.

1. `reg-form-app` için mevcut submit listener'ını kaldırıp
`examples/web-form/altium-event-form.js` ile değiştirin. Eski ve yeni listener
birlikte kalmamalı. EIO/kanal eşitleme kodu ve AltiumPhone yardımcıları kalabilir.
2. Turnstile widget'ının şablonuna `data-action="web-lead"` ekleyin; widget PUBLIC
site key'inin SECRET karşılığı Azure'da tanımlı olsun. Yüklenmiş widget'ı değiştirmek
yerine şablonu düzenleyip sayfayı yeniden açın. Hostname altium.net olmalıdır.
3. JS'yi site asset'lerine koyun veya Azure'a yayımlanan kopyayı yükleyin:

```html
<script defer src="https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net/integrations/altium-event-form.js"></script>
```

Site CSP kullanıyorsa script kaynağı ve fetch için connect-src izinleri de gerekir.
JS gizli anahtar veya CSRF token göndermez. Bu giriş CAPTCHA, origin ve limitlerle
korunur. Backend her istekte CAPTCHA success/hostname/action kontrolü yapar.

Bu sürüm doğrudan Azure'a gönderir. Sitenin eski POST endpoint'ini çağırmadığı
için onun veritabanı/e-posta/başka işlemlerini çalıştırmaz. Başarı mesajı CRM
kaydını bildirir. Hem site kayıt işlemleri hem CRM yazması istenirse site sunucusu
üzerinden anahtarlı ASPX çağrısı gerekir. Aynı Turnstile token'ı iki kez doğrulanamaz.

## Alanlar

| Gerçek form | JSON | Lead |
| --- | --- | --- |
| first_name / last_name | firstName / lastName | firstname / lastname |
| email / company | email / company | emailaddress1 / companyname |
| phone_country + phone | phone | mobilephone, ülke koduyla |
| title / city | jobTitle / city | açıklamada Unvan / Şehir |
| message | message | description |
| kvkk checkbox | consent | boolean true; açıklamada onay kaydı |
| eio ve kanallar | marketingConsent, emailConsent, smsConsent, phoneConsent | açıklamada ayrı beyanlar |
| _email honeypot | website | doluysa reddedilir |
| cf-turnstile-response | captchaToken | sunucuda doğrulanır |
| main h1 | eventTitle | subject: Etkinlik Kaydı: başlık |
| origin + pathname | eventUrl | açıklamada form adresi |

KVKK checkbox'ı gerçekten okunur; sabit `1` gönderilmez. Pazarlama onayı kayıt
için zorunlu değildir; kanallarla tutarlı olmalıdır. Beyanlar açıklamaya kaydedilir,
CRM'in donotbulkemail gibi tercih sütunları değiştirilmez. Unvan/şehir için bilinmeyen
custom column veya lookup varsayılmaz. Ayrı bir CRM etkinlik ilişkisi oluşturulmaz.
Başlık ve URL tarayıcıdan gelen kaynak bilgisi olarak saklanır.

Etkinlik mesaj limiti 1000; unvan/şehir 100; etkinlik başlığı 150; URL 400 karakter.
Açıklama toplamı 2000 karakteri aşarsa veri kesilmez, istek reddedilir. Event URL
izin verilen origin altında /tr/etkinlik-kayit/ yolunda olmalı; query/fragment yoktur.

## Gerçek test

Azure deploy, ayarlar ve site şablonu güncellemesinden sonra test etkinliğinde
bir form gönderin. Network'te OPTIONS 204, POST 201, JSON success:true kontrol
edin. TEST CRM'de Lead'in konu, mesaj, unvan, şehir, telefon, onaylar ve form URL'sini
kontrol edin. Test sırasında yeni CRM kaydı oluşur.

Network'te POST hedefi hâlâ `altium.net/tr/etkinlik-kayit/...` ise yeni kod
siteye uygulanmamış demektir. POST Azure'a gidiyor ve 503 dönüyorsa servis ayarları,
403 dönüyorsa origin/CAPTCHA, 502 dönüyorsa CRM işlemi incelenmelidir. Yeni JS
503 ve 502 cevaplarında bu ayrımı ve sunucunun takip referansını gösterir.

JS aynı anda çift gönderimi engeller; kalıcı idempotency yoktur. Yanıt kaybolursa
kayıt oluşmuş olabilir; otomatik tekrar gönderilmez. Rate limit süreç belleğindedir;
çoklu instance için ortak gateway/WAF limiti gerekir.

Önceki sürüm e5248b5 ile Azure'a başarıyla yayımlandı. Paylaşılan gerçek Altium
sayfa kaynağında gönderim hâlâ siteye yapılıyor; bu sürümün site şablonuna
uygulanması gereklidir. Yeni doğrudan gönderimden gerçek CRM kaydı henüz doğrulanmadı.
