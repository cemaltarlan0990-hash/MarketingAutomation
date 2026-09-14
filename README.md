# ALTTR Marketing Automation CRM API

Bu proje, etkinlik katılımcı verilerini HTTP üzerinden alıp Microsoft Dynamics
365 / Dataverse test ortamında **Müşteri Adayı (Lead)** kaydı açan `.NET 8`
ASP.NET Core Web API uygulamasıdır.

Hedef Azure kaynağı:

```text
Web App: ALTTR-MarketingAutomation
Resource Group: Altium_TR
```

`src/CrmRegistrationGateway` altındaki eski `.NET Framework 4.8` Web Forms
uygulaması geçmiş çalışma olarak korunmuştur. Yeni deployment hedefi
`src/CrmRegistrationGateway.Functions/CrmRegistrationGateway.Api.csproj`
projesidir. Klasörün eski `Functions` adı korunmuş olsa da proje artık Azure
Functions değil, App Service uyumlu ASP.NET Core Web API'dir.

## Çalışma akışı

```text
POST /api/crm/leads
        |
        v
X-Integration-Key kontrolü
        |
        v
JSON ve alan doğrulaması
        |
        v
Entra ID client credentials ile Dataverse token'ı
        |
        v
emailaddress1 ile mevcut Lead sorgusu
        |
        +-- bulundu  -> HTTP 200 / already_exists
        |
        +-- bulunmadı -> Lead oluştur / HTTP 201 / created
```

- `GET /health` uygulamanın ayakta olduğunu kontrol eder ve API anahtarı istemez.
- `POST /api/crm/leads` en az 32 karakterli `X-Integration-Key` header'ı ister.
- MSAL application token cache gereksiz token çağrılarını önler.
- `IHttpClientFactory` Dataverse HTTP bağlantılarını güvenli şekilde yönetir.
- CRM credential'ları, API anahtarı ve token'lar loglanmaz veya yanıta yazılmaz.
- Application Insights bağlantı dizesi varsa telemetri otomatik etkinleşir.

## İstek alanları ve CRM mapping

Logical name'ler CRM Müşteri Adayı ekranındaki doğrulanmış alanlardan alınmıştır.

| JSON alanı | Lead logical name | Açıklama |
| --- | --- | --- |
| `eventName` | `subject` | Başlık |
| `company` | `companyname` | Şirket Adı |
| `firstName` | `firstname` | Ad; zorunlu |
| `lastName` | `lastname` | Soyadı; zorunlu |
| `email` | `emailaddress1` | E-posta; zorunlu ve duplicate anahtarı |
| `department` | `twbs_department` | Departman Manuel |
| `jobTitle` | `twbs_isunvani` | İş Ünvanı Manuel |
| `city` | `twbs_sehir` | Şehir |
| `workPhone` | `telephone1` | İş Telefonu |
| `mobilePhone` | `mobilephone` | Cep Telefonu |
| `phone` | `mobilephone` | Eski istemciler için geriye uyumlu alan |

`mobilePhone` ve `phone` birlikte gelirse `mobilePhone` önceliklidir. Mapping tek
noktada `Configuration/CrmFieldMapping.cs` dosyasında bulunur.

## Örnek istek

```bash
curl --request POST \
  "https://alttr-marketingautomation.azurewebsites.net/api/crm/leads" \
  --header "Content-Type: application/json" \
  --header "X-Integration-Key: <INBOUND_API_KEY>" \
  --data '{
    "firstName": "Cemal",
    "lastName": "Tarlan",
    "email": "cemal@example.com",
    "company": "ABC Teknoloji",
    "eventName": "Altium Day 2026",
    "department": "Ar-Ge",
    "jobTitle": "Computer Engineer",
    "city": "İstanbul",
    "workPhone": "+902120000000",
    "mobilePhone": "+905551234567"
  }'
```

Yeni kayıt cevabı:

```json
{
  "success": true,
  "status": "created",
  "crmId": "00000000-0000-0000-0000-000000000000",
  "message": "Lead successfully created.",
  "correlationId": "..."
}
```

Aynı e-posta varsa yeni kayıt açılmaz; `HTTP 200`, `already_exists` ve mevcut
Lead ID'si döner. Validation hataları `400`, API anahtarı hatası `401`, CRM
authentication/API hataları `502`, timeout `504`, beklenmeyen hatalar `500`
döndürür.

## Azure Web App ayarları

Azure Portal'da:

```text
ALTTR-MarketingAutomation
→ Settings
→ Environment variables
→ App settings
```

Aşağıdaki değerleri ekleyin:

| Ayar | Zorunlu | Açıklama |
| --- | --- | --- |
| `CRM_URL` | Evet | Test Dataverse HTTPS URL'si |
| `TENANT_ID` | Evet | Entra tenant GUID |
| `CLIENT_ID` | Evet | App Registration client GUID |
| `CLIENT_SECRET` | Evet | App Registration secret value |
| `INBOUND_API_KEY` | Evet | En az 32 karakterli rastgele API anahtarı |
| `CRM_API_VERSION` | Hayır | Varsayılan `v9.2` |
| `CRM_REQUEST_TIMEOUT_SECONDS` | Hayır | Varsayılan `30` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Önerilir | Application Insights bağlantısı |

App Service üzerinde ayrıca:

- Runtime stack `.NET 8` olmalı.
- `HTTPS Only` açık olmalı.
- Application Insights bağlı olmalı.
- Credential değerleri GitHub workflow dosyasına veya kaynak koda yazılmamalı.
- Production'da `CLIENT_SECRET` için Key Vault reference tercih edilmelidir.

## Dataverse hazırlığı

1. App Registration'ın client ID'siyle test Dataverse ortamında Application User
   oluşturun.
2. Application User'a Lead tablosu üzerinde en az `Read` ve `Create` yetkili özel
   Security Role verin.
3. Eşzamanlı duplicate garantisi gerekiyorsa CRM yöneticisiyle `emailaddress1`
   Alternate Key veya duplicate rule kararı alın.
4. `twbs_department`, `twbs_isunvani` ve `twbs_sehir` alanlarının metin değerini
   kabul ettiğini test ortamı metadata'sından doğrulayın.

## GitHub Actions deployment

Hazır workflow:

```text
.github/workflows/deploy-alttr-marketing-automation.yml
```

Workflow her `main` veya `master` push'unda:

1. .NET 8'i kurar.
2. Paketleri restore eder.
3. Unit testleri çalıştırır.
4. Web API'yi Release modunda publish eder.
5. Azure'a OpenID Connect ile giriş yapar.
6. Çıktıyı `ALTTR-MarketingAutomation` Web App'e deploy eder.

GitHub repository içinde aşağıdaki Actions secrets bulunmalıdır:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
```

`AZURE_CLIENT_ID`, GitHub OIDC için kullanılan managed identity veya service
principal kimliğidir; CRM için kullanılan `CLIENT_ID` ile aynı olmak zorunda
değildir. Deployment kimliğine `ALTTR-MarketingAutomation` üzerinde uygun
deployment rolü verilmeli ve GitHub repository/branch için federated credential
tanımlanmalıdır. Azure Deployment Center, gerekli yetki varsa OIDC bağlantısını
oluşturabilir.

Portal yolu:

```text
ALTTR-MarketingAutomation
→ Deployment Center
→ Source: GitHub
→ Organization / Repository / Branch
→ Authentication: User-assigned identity (OpenID Connect)
```

## Yerel geliştirme

Örnek ayarı kopyalayın:

```powershell
Copy-Item `
  .\src\CrmRegistrationGateway.Functions\appsettings.Development.json.example `
  .\src\CrmRegistrationGateway.Functions\appsettings.Development.json
```

Gerçek credential'ları yalnız ignore edilen
`appsettings.Development.json` dosyasına girin.

Derleme ve test:

```powershell
.\scripts\Build.ps1
```

Yerel API:

```powershell
.\scripts\Start-ApiLocal.ps1
```

CRM'e bağlanmadan HTTP sözleşme testi:

```powershell
.\scripts\Test-ApiContract.ps1 `
  -ApiKey "<YEREL_INBOUND_API_KEY>"
```

## NuGet paketleri

Uygulama:

- `Microsoft.ApplicationInsights.AspNetCore`
- `Microsoft.Identity.Client`

Test:

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `NSubstitute`

## Kapsam

Bu sürüm yalnızca Lead oluşturur. Contact, Account, Event ve diğer Dataverse
tabloları implemente edilmemiştir. Canlı Dataverse testi gerçek test credential'ları
ve yetkili Application User sağlandıktan sonra yapılmalıdır.
