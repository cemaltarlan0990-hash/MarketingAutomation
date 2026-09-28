# ALTTR Marketing Automation ASPX servisi

Etkinlik formu bilgilerini alıp Dynamics 365 / Dataverse TEST ortamında Lead
oluşturan **ASP.NET Web Forms / .NET Framework 4.8** servisidir.

## Aktif proje ve yayın

- Proje: `src/CrmRegistrationGateway/CrmRegistrationGateway.csproj`
- Azure App Service: `ALTTR-MarketingAutomation` (Windows)
- `main` dalına push, GitHub Actions üzerinden derleme, IIS Express testleri,
  Azure yayını ve canlı endpoint doğrulamasını çalıştırır.
- GitHub'daki mevcut Azure yayın secret'ı kullanılır. Gizli bilgiler Git'e yazılmaz.
- ASPX yayını önceki .NET 8 API'nin yerini alır. Önceki API kodu
  `src/CrmRegistrationGateway.Functions` altında korunur; eski dokümantasyon
  [docs/LEGACY_NET8_API.md](docs/LEGACY_NET8_API.md) içindedir.

## Servisi çağırma

`POST /CreateCrmRegistration.aspx`

- `Content-Type: application/x-www-form-urlencoded`
- `X-Integration-Key`: web sitesi sunucusunda saklanan entegrasyon anahtarı
- Zorunlu alanlar: `firstname`, `lastname`, `email`
- İsteğe bağlı alanlar: `phone`, `company`
- Başarılı yanıt: HTTP 200, `success: true` ve oluşturulan kaydın `crmId` değeri

Örnek gövde (test verisi):

```text
firstname=Test&lastname=Participant&email=test%40example.invalid&company=Example
```

Web sitesi ekibi formun gönderimini işleyen sunucu kodundan bu isteği yapar.
Anahtar tarayıcı JavaScript'ine konulmamalıdır. Form entegrasyonu bu depodaki
servisin yayımlanmasından ayrı bir adımdır.

`eventId` alınır ama bu sürümde CRM'e yazılmaz. Unvan, şehir, not ve mükerrer
kayıt denetimi henüz ASPX sürümünde uygulanmamıştır. KVKK/Marketing aktarılmaz.

## Bağlantı kontrolü

`POST /TestCrmConnection.aspx`, aynı `X-Integration-Key` başlığıyla CRM WhoAmI
isteği yapar. Yeni CRM kaydı oluşturmaz. ASPX servisinde `/health` veya
`/api/crm/leads` endpoint'i bulunmaz.

## Yapılandırma ve yerel çalışma

Azure'da `CRM_URL`, `CLIENT_ID`, `CLIENT_SECRET`, `TENANT_ID` ve
`INBOUND_API_KEY` ortam ayarları kullanılır. `CRM_` önekli kimlik ayarı adları da
kodda desteklenir. Hedef yalnız `altiumtr-test.crm4.dynamics.com` ortamıdır.

Yerel gizli değerler `.gitignore` kapsamındaki `Web.local.config` dosyasında
veya ortam değişkenlerinde tutulur. `Web.config` kaynak dosyasında kimlik
bilgileri boş bırakılır. Yayın paketi yerel ayarları içermez.

PowerShell 7 ve Visual Studio MSBuild ile:

```powershell
./scripts/Build-AspxPackage.ps1
./scripts/Test-AspxPackage.ps1 -PackageRoot '<paket-dizini>/wwwroot'
```

Ayrıntılar: [ASPX yayın ve doğrulama notları](docs/ASPX_DEPLOYMENT.md).
