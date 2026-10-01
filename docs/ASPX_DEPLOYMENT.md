# ASPX Azure yayını

Bu yayın `src/CrmRegistrationGateway` altındaki .NET Framework 4.8 Web Forms
servisi içindir. `.github/workflows/main_alttr-marketingautomation.yml`, `main`
dalına push yapıldığında ASPX'i Windows üzerinde derler, IIS Express'te sınar ve
GitHub'da zaten tanımlı Azure yayın secret'ıyla `ALTTR-MarketingAutomation`
uygulamasının köküne yayımlar. Yayın profilini yerel bilgisayara indirmez.

Bu yayın önceki .NET 8 API'nin yerini alır. Yeni kayıt adresi
`POST /CreateCrmRegistration.aspx`, bağlantı tanısı `POST /TestCrmConnection.aspx`.
Önceki `/api/crm/leads` ve `/health` bu ASPX servisinin endpoint'leri değildir.

## Paket oluşturma

PowerShell 7 ve Visual Studio MSBuild ile:

```powershell
./scripts/Build-AspxPackage.ps1
# TEST CRM yazmaları açık bir yayın paketi için:
./scripts/Build-AspxPackage.ps1 -EnableCrmWrites
```

Çıktı `.artifacts/aspx-deploy/<tarih>/CrmRegistrationGateway.Aspx.zip` dosyasıdır.
Paketin kökünde dört ASPX dosyası, Web.config ve bin dizini vardır. Kaynak kod,
katılımcı verisi ve kimlik bilgileri pakete alınmaz. Paket üretimi tek başına
Azure'a yayın yapmaz. Yerel Web.config değiştirilmez. Yerel kimlik bilgileri
Git tarafından yok sayılan `Web.local.config` dosyasında tutulabilir; bu dosya
ve ona ait config referansı yayın paketine alınmaz.

## Azure hedefi ve ayarları

Windows App Service, .NET Framework 4.8 / IIS v4.0 uygulama havuzu gerekir.
Hedef uygulamanın Windows olduğu doğrulandı. Ayrı `/aspx` sanal uygulaması
gerekmiyor; GitHub yayını ASPX'i doğrudan `site\\wwwroot` içine yerleştirir.

Aşağıdaki ayarlar Azure uygulama ortamında bulunmalıdır; gizli değerleri
kaynak koda, yayın ZIP'ine veya sohbete yazmayın:

| Ayar | Değer |
| --- | --- |
| CRM_URL | https://altiumtr-test.crm4.dynamics.com/ |
| CRM_CLIENT_ID | Mevcut TEST CRM uygulama kimliği |
| CRM_CLIENT_SECRET | TEST CRM uygulamasının geçerli gizli değeri |
| CRM_TENANT_ID | Mevcut tenant kimliği |
| CRM_INBOUND_API_KEY | Servisi çağıracak sunucunun kullanacağı gizli anahtar |
| CRM_ENVIRONMENT | Test |
| CRM_ALLOWED_HOST | altiumtr-test.crm4.dynamics.com |
| CRM_WRITES_ENABLED | Test kaydı oluşturulacaksa true |

CLIENT_ID, CLIENT_SECRET, TENANT_ID ve INBOUND_API_KEY adları da kodda desteklenir.
Paket, lead/firstname/lastname/emailaddress1/mobilephone/companyname eşlemelerini
içerir. Yerel paket varsayılan olarak yazmayı kapalı tutar. GitHub workflow'u
`-EnableCrmWrites` ile yalnız izin verilen TEST CRM host'una yazmayı açar;
Azure'daki `CRM_WRITES_ENABLED` değeri varsa paket ayarına üstün gelir.

## Yayın sonrası doğrulama

Workflow, canlı `deployment-version.txt` değerinin commit SHA ile eşleştiğini
ve kimliksiz endpoint kontrollerini doğrular. Bu doğrulama CRM kaydı oluşturmaz
ve tek başına gerçek CRM bağlantısının başarılı olduğu anlamına gelmez.

- GET CreateCrmRegistration.aspx: JSON gövdeli HTTP 405 dönmeli.
- Anahtarsız form POST: HTTP 401 dönmeli.
- Geçersiz Content-Type: HTTP 415 dönmeli.
- Yetkili fakat eksik alanlı POST: HTTP 400 dönmeli.
- Yetkili POST TestCrmConnection.aspx: CRM bağlantısını kayıt oluşturmadan sınar.
- Yetkili POST CreateCrmRegistration.aspx: TEST CRM'de kayıt oluşturur; dönen
  crmId ile kayıt ayrıca doğrulanmalıdır.

CreateCrmRegistration.aspx, application/x-www-form-urlencoded gövdesi ve
X-Integration-Key başlığı bekler. firstname, lastname, email zorunludur;
phone ve company isteğe bağlıdır. eventId alınır fakat mevcut sürümde CRM'e
yazılmaz. Unvan, şehir, kalıcı tekrar gönderim takibi ve form entegrasyonu
bu yayın paketine eklenmiş özellikler değildir.

## Web formu JSON girişi

Yeni `/WebLead.aspx` aynı uygulamada doğrudan tarayıcı JSON'unu alır. Pakete
dahildir. Genel HTML/contactForm örnekleri paket dışında kalır; gerçek etkinlik
JS'si `/integrations/altium-event-form.js` olarak pakete eklenir.
`WEB_LEAD_ALLOWED_ORIGINS`, `TURNSTILE_SECRET_KEY`, `WEB_LEAD_CONSENT_VERSION`
Azure ayarlarında tanımlanmalıdır. IIS OPTIONS handler'ı Web.config'de bu sayfa
için ayrıca eşlenmiştir. Ayrıntılar: [Web formu entegrasyonu](WEB_FORM_INTEGRATION.md).

Gerçek `reg-form-app` alan eşlemesi, GitHub push ve siteye uygulama için
[Altium etkinlik formunu devreye alma](ALTIUM_EVENT_FORM_GO_LIVE.md).
