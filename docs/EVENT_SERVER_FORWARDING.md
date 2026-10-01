# Mevcut web formunu mevcut ASPX servisine bağlama

Akış: kullanıcı Gönder'e basar → altium.net üzerindeki mevcut form işleyicisi veriyi doğrular → işleyici Azure'daki `CreateCrmRegistration.aspx` adresine JSON gönderir → mevcut Dataverse bağlantısı Lead açar.

## Hazırlanan dosyalar

- `CreateCrmRegistration.aspx` artık anahtarlı JSON kabul eder. Önceki form-urlencoded çağrıları çalışmaya devam eder.
- `examples/web-form/forward-event-to-crm.php` formdaki gerçek alanları CRM isteğine çeviren **site sunucusu için entegrasyon örneğidir**. Sitenin kaynak kodu bu depoda bulunmadığı için mevcut form işleyicisine otomatik kurulmuş değildir. PHP dışındaki bir site sunucusu aynı JSON sözleşmesini kullanabilir.

## Site tarafında uygulanacak işlem

Mevcut form işleyicisinde sunucu doğrulaması, CSRF ve CAPTCHA başarılı olduktan sonra yardımcı fonksiyonu çağırın. Etkinlik adı ve adresini sunucudaki etkinlik kaydından alın. KVKK onayını gerçekten doğrulayın; tarayıcının her istekte `kvkk_consent: 1` göndermesi tek başına doğrulama değildir.

```php
require_once '/sunucudaki-gercek-dizin/forward-event-to-crm.php';

// Bu değişkenler mevcut site işleyicisinden sağlanır.
// $integrationKey: site sunucusu yapılandırmasındaki mevcut INBOUND_API_KEY.
// Ortam değişkeni kullanılıyorsa (framework kullanıyorsanız kendi config okuyucunuzla):
$integrationKey = getenv('INBOUND_API_KEY') ?: '';
// $validatedForm: doğrulanmış first_name, last_name, email, phone, company,
// title, city, message, kvkk_consent, eio_consent ve channel_* alanları.
$crmResult = forwardEventToCrm(
    $validatedForm,
    $integrationKey,
    $eventTitleFromDatabase,
    $eventUrlFromDatabase
);
// CRM aktarımı da tamamlandıktan sonra mevcut başarı/teşekkür cevabını verin.
```

Test etkinliği için sunucu kaynaklı değerler:

```text
eventTitle: TEST Kayıttır Silmeyin
eventUrl: https://altium.net/tr/etkinlik-kayit/test-kayittir-silmeyin
```

Telefon değeri mevcut doğrulanmış `phone` alanından aktarılır. Site uluslararası telefon biçimi üretiyorsa o değeri kullanın. Ülke bilgisi bu örnekte telefon numarasına otomatik eklenmez.

## Anahtar ve Azure ayarları

ASPX kodu `CRM_INBOUND_API_KEY`, ardından `INBOUND_API_KEY`, ardından `Crm.InboundApiKey` ayarını okur. İlk dolu değer kullanılır. Site sunucusunun `X-Integration-Key` başlığında gönderdiği değer, Azure'un okuduğu değerle aynı olmalıdır. Anahtar JavaScript'e veya GitHub'a yazılmaz; site sunucusu yapılandırmasına eklenir.

Çalışan CRM bağlantı ayarları korunur. Bu sunucudan sunucuya akış için Azure'da `TURNSTILE_SECRET_KEY` veya CORS ayarı eklemek gerekmez; CAPTCHA mevcut site tarafından doğrulanır. `WEB_LEAD_CONSENT_VERSION` isteğe bağlıdır: yoksa CRM açıklamasında metin sürümü yazılmaz. Tarayıcıdan doğrudan çağrılan `WebLead.aspx` için önceki CAPTCHA gereksinimi devam eder.

## Kayıt ve hata davranışı

Ad, soyad, e-posta, telefon ve şirket Lead alanlarına; etkinlik adı konu alanına; mesaj, unvan, şehir, etkinlik adresi ve onaylar açıklamaya aktarılır. Pazarlama onayları ayrı kaydedilir; CRM'in pazarlama izin alanları bu değişiklikte değiştirilmez. CRM cevabı HTTP 200, `success: true` ve `crmId` içerdiğinde kayıt doğrulanır.

Otomatik tekrar gönderim yoktur. Zaman aşımı veya site kaydı ile CRM kaydı arasındaki kısmi hata durumunda ikinci gönderimden önce CRM'i kontrol edin; bu sürüm yinelenen kaydı önleyen bir işlem kimliği veya kalıcı kuyruk içermiyor.

## İlk gerçek test

1. ASPX değişikliğini Azure'a deploy edin.
2. Yardımcı çağrıyı altium.net'in gerçek form işleyicisine ekleyin ve mevcut anahtarı site sunucusuna tanımlayın.
3. Test etkinliğine ayırt edilebilir test adı/e-posta ile tek form gönderin.
4. Site isteğinin CRM'den başarı cevabı aldığını ve `altiumtr-test.crm4.dynamics.com` ortamında `Etkinlik Kaydı: TEST Kayıttır Silmeyin` konulu Lead oluştuğunu doğrulayın.

Yalnızca bu depoyu Azure'a deploy etmek, altium.net'in form işleyicisini değiştirmez.
