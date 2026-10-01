# Proje Bağlamı: ALTTR Marketing Automation

Son güncelleme: 2026-10-02

Bu dosya, proje üzerinde sonraki çalışmalarda mevcut durumu hızlıca hatırlamak için
tutulur. Parola, token, client secret veya API anahtarı içermez ve içermemelidir.

## Güncel durum: 2026-10-02 — Mevcut anahtarla site sunucusundan aktarım

### Son kullanıcı yönlendirmesi: doğrudan tarayıcıdan Azure'a gönderim

- Kullanıcı doğrudan Azure adresinin seçilmesini ve gerekli değişikliklerin pushlanmasını istedi. Güncel seçilen akış form → WebLead.aspx → CRM; sunucu aktarımı artık alternatiftir.
- examples/web-form/altium-event-form.js hedefi açıkça ALTTR Azure WebLead.aspx adresidir. Altium form şablonuna uygulanacak Turnstile + JS parçası examples/web-form/altium-event-form-integration.blade.php dosyasında hazırdır.
- 503 servis ayarları ve 502 CRM hataları form üzerinde farklı mesajlarla ve güvenli correlationId ile gösterilir; otomatik tekrar yoktur. 20 JS testi geçti.
- Canlı Azure kontrolü: Altium preflight OPTIONS 204 ve doğru Access-Control-Allow-Origin; geçersiz CAPTCHA ile kayıt açamayan tanılama POST'u 503 döndü. Giriş erişilebilir, ancak servis ayarlarının tamamlandığı henüz doğrulanmadı. Hangi ayarın eksik olduğu portal/günlük erişimi olmadan kesinleştirilmedi.
- Kullanıcının paylaştığı gerçek form HTML'i hâlâ fetch(storeUrl) ile Altium'a POST yapıyor. Admin kaynağında Mustafa Taylan / mustafa12314@gmail.com kaydı var; TEST CRM'de aynı e-posta bulunmadı.
- Bu depoyu Azure'a deploy etmek gerçek Altium şablonunu değiştirmez. Sitede eski submit listener kaldırılıp yeni dosya uygulanmalı; widget action web-lead olmalı ve Azure gerçek CAPTCHA secret/metin sürümü/CRM ayarları tamamlanmalıdır.

- Kullanıcı mevcut çalışan CRM bağlantısı ve INBOUND_API_KEY ile gerçek form verilerinin aktarılmasını istedi.
- CreateCrmRegistration.aspx artık X-Integration-Key doğrulamasından sonra JSON form payload'ını kabul eder; eski form-urlencoded girişi korunur.
- Authenticated JSON yolunda CAPTCHA yeniden doğrulanmaz; bunu mevcut site sunucusu çağrıdan önce yapmalıdır. WebLead.aspx'in halka açık doğrulaması değiştirilmedi.
- Siteye uygulanacak PHP/cURL yardımcı örnek examples/web-form/forward-event-to-crm.php; site kaynağı depoda olmadığı için gerçek işleyiciye kurulmuş değildir. PHP çalışma zamanı yerelde bulunmadığı için bu örnek çalıştırılmadı.
- Kurulum belgesi docs/EVENT_SERVER_FORWARDING.md. Mevcut anahtar site sunucusu yapılandırmasından alınmalı; tarayıcıya veya GitHub'a yazılmamalı.
- Release derleme, 55 model/eşleme ve 29 IIS Express HTTP kontrolü geçti. Testlerde CRM'e istek gönderilmedi. Bu yeni değişiklikler henüz commit/push/deploy edilmedi.
- Önceki çalışma 7ec0e20 commit'iyle GitHub'a gönderildi; kullanıcı önceki Azure deploy'un başarılı olduğunu bildirdi.

## Önceki çalışma: 2026-10-01 — Gerçek etkinlik formu için hazır değişiklikler

- Kullanıcı kodların bu klasörde hazırlanmasını istedi; GitHub push işlemini
  kendisi yapacak. Commit/push/deploy yapılmadı.
- Gerçek sayfa https://altium.net/tr/etkinlik-kayit/test-kayittir-silmeyin,
  form id reg-form-app, first_name/last_name, title, city, phone_country/phone,
  kvkk_consent, eio_consent ve ayrı kanal checkbox'ları kullanıyor. CAPTCHA Turnstile.
- Yeni examples/web-form/altium-event-form.js, mevcut submit listener'ın yerine
  konacak. Kayıt onayı, alan/checkbox/telefon eşlemesi, yalnız Azure'a JSON gönderimi,
  buton kilidi ve güvenli sonuç gösterimi var. Eski site POST endpoint'i çağrılmaz;
  onun mail/veritabanı işlevleri bu direct-submit sürümünde çalıştırılmaz.
- WebLeadRequest artık unvan/şehir/etkinlik başlığı/URL ve bağımsız pazarlama/kanal
  beyanlarını da kabul eder. Unvan/şehir/izinler açıklamada; başlık subject'te.
  Pazarlama tercihlerine ait CRM sütunları veya etkinlik lookup'ı değiştirilmez.
- Origin https://altium.net kaynak ve paket varsayılanında tanımlı. Secret/metin
  sürümü Azure'da ayrıca tanımlanmalı; widget'a data-action=web-lead eklenmeli.
- JS dosyası Azure paketine integrations/altium-event-form.js olarak eklenir.
  GitHub workflow model+iki JS test dosyasını çalıştırır, yayın sonrası JS'yi kontrol eder.
- Release derleme, 21 HTTP, 51 model/mapping ve 18 JS kontrolü geçti; CRM'e
  gerçek gönderim yapılmadı. Rehber docs/ALTIUM_EVENT_FORM_GO_LIVE.md.
- GitHub deploy Altium sitesinin form şablonunu otomatik değiştirmez; siteye
  yeni JS'nin uygulanması ve eski submit listener'ın kaldırılması gerekir.

## Güncel durum: 2026-09-30 — Web formu JSON girişi

- Kullanıcı yeni Node.js/Functions yerine mevcut ASPX servisinin web formundan
  veri almasını istedi. Yeni `POST /WebLead.aspx` aynı .NET Framework 4.8
  uygulamasına eklendi; mevcut `CrmConnection` / `CrmRegistrationWriter` kullanılır.
- JSON sözleşmesi: firstName, lastName, email, phone, company, message, consent
  (boolean true), captchaToken, website (honeypot boş). Subject ve description
  eşlemesi eklendi; onay metni sürümü ve sunucu zamanı açıklamaya kaydedilir.
  Form onayı pazarlama iznine dönüştürülmez.
- Origin allowlist, HTTPS, strict JSON doğrulaması, 16 KiB limit, Turnstile
  server-side success/hostname/action doğrulaması ve süreç içi rate limit var.
  Çoklu instance için ortak limit yok; gateway/WAF kısıtı rehberde açıklandı.
- Yeni ayarlar: WEB_LEAD_ALLOWED_ORIGINS, TURNSTILE_SECRET_KEY,
  WEB_LEAD_CONSENT_VERSION. Gerçek değerler yok; endpoint fail-closed çalışır.
- `examples/web-form` HTML/JS, `docs/WEB_FORM_INTEGRATION.md` bağlantı rehberidir.
- Release derleme, 21 yerel HTTP kontrolü, 41 model/CRM mapping kontrolü ve
  6 frontend testi geçti. Testlerde CRM'e istek yapılmadı.
- Kaynak/paket/workflow güncellendi. Bu değişiklik henüz commit/push/yayın
  edilmedi; gerçek CAPTCHA ve yeni form → Azure → CRM testi yapılmadı.
- Mevcut anahtarlı CreateCrmRegistration.aspx ve Power Automate EtkinlikKayit.aspx
  istek sözleşmeleri korunur. TEST CRM kısıtları korunur.

## Güncel durum: 2026-09-28

### TAMAMLANDI: ASPX GitHub üzerinden Azure'ya yayımlandı

- Kullanıcının "aspx uzantılı olup crm de kayıt açabiliyorsak tamamdır github a
  yükle" onayından sonra `a7d519583d0fbd1f1c68076c675ca6033a305baa` GitHub main'e
  gönderildi. Önceki .NET 8 API'nin yerine ASPX yayınlandı; ayrı `/aspx` yolu yok.
- GitHub Actions run **36421920561**, job **108926459375**: **success**.
  Derleme, IIS Express sözleşme testleri, Azure deploy ve canlı doğrulama
  aşamalarının tamamı başarılı.
- Yayın:
  `https://github.com/cemaltarlan0990-hash/MarketingAutomation/actions/runs/36421920561`
- Canlı kayıt endpoint'i:
  `https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net/CreateCrmRegistration.aspx`
- Canlı bağlantı tanısı: aynı host altında `POST /TestCrmConnection.aspx`.
- Canlı workflow, `deployment-version.txt` dosyasının bu commit'le eşleştiğini,
  kayıt endpoint'inin GET -> 405, yanlış Content-Type -> 415, anahtarsız form
  POST -> 401 döndüğünü ve bağlantı tanısının da anahtarsız POST'u 401 ile
  reddettiğini doğruladı. Kimliksiz yanıtların JSON sözleşmesi geçti.
- Gerçek TEST CRM kaydı oluşturma ASPX'in YEREL çalışması üzerinden aşağıda
  belgelenen testte doğrulandı. Azure'daki ASPX üzerinden anahtarlı CRM bağlantısı
  ve kayıt oluşturma henüz test edilmedi; bunun yapıldığını iddia etme.
- Yayın profili yerel bilgisayara indirilmedi; mevcut GitHub secret'ı kullanıldı.
  Kaynak gizli değerleri, Web.local.config ve yerel katılımcı/test dosyaları Git'e
  gönderilmedi. Önceki "henüz yayımlanmadı/seçim bekleniyor" notları tarihseldir.

### Kullanıcı ASPX geçişini onayladı; GitHub gönderimi tamamlandı

- Kullanıcı eski .NET 8 API ile ASPX farkı açıklandıktan sonra "aspx uzantılı olup
  crm de kayıt açabiliyorsak tamamdır github a yükle" diyerek ASPX sürümünü onayladı.
- Önceki riskler açıklandıktan sonraki bu onayla `git push github main` başarılı:
  uzak main `6893ec0` -> `a7d5195`. Kod artık GitHub'da. Yayın profili indirilmedi.
- GitHub Actions yayını izleniyor; Azure tamamlanma durumu henüz doğrulanmadı.
  Önceki bölümlerdeki "push yapılmadı / seçim bekleniyor" notları tarihseldir.

### Son kullanıcı isteği: GitHub gönderimi, yayın biçimi seçimi bekleniyor

- Başarılı ASPX CRM testi sonrası kullanıcı "o zaman azure ye atmak için github a
  atalım" dedi. Depo görünürlüğü daha önce açıklanmışken GitHub gönderimini yeniden
  istedi; genel gönderim iznini tekrar sorma.
- Ancak önceki açıklamada önerilen mevcut API + `/aspx` düzeni ile hazır commit'in
  API yerine ASPX yayınlaması arasında açık seçim yapılmamıştı. Bu iki seçenek
  kullanıcıya tek soruda sunuldu. Yanıt gelene kadar otomatik yayını tetikleme.
- Uzak main yeniden doğrulandı: `6893ec0`; yerel hazır commit `a7d5195`. Giden
  12 dosyada yerel kimlik bilgisi bulunmadığı yeniden doğrulandı. Henüz push yok.

### Doğrulandı: ASPX üzerinden gerçek TEST CRM kaydı oluşturuldu

- Kullanıcı bu kez ASPX servisiyle veri göndermeyi açıkça istedi. 28 Eylül 2026
  saat 15:13-15:15 (Türkiye) aralığında güncel kod Release derlenip yerel IIS
  Express'te çalıştırıldı. Azure/GitHub yayını yapılmadı.
- Akış: HTTP POST -> `CreateCrmRegistration.aspx` -> `CrmRegistrationWriter`
  -> CRM SDK `Create`. Kayıt oluşturmak için .NET 8 API veya doğrudan Web API
  kullanılmadı. Web API sadece ön kontrol ve oluşturulan kaydı geri okumak için
  kullanıldı.
- `TestCrmConnection.aspx`: HTTP 200, `success=true`.
- `CreateCrmRegistration.aspx`: HTTP 200, `success=true`.
- Oluşan Lead: `644aae13-36bb-f111-aaaf-7ced8d425b39`.
- Ad/soyad: `ASPX Entegrasyon Testi`; şirket: `ALTTR ASPX TEST`.
- Benzersiz test e-postası: `aspx-test-20260928151354-083b88@example.invalid`.
- TEST CRM'de geri okuma tam bir kayıt buldu; ID, ad, soyad, e-posta ve şirket
  gönderilen değerlerle eşleşti. `verified=true`. Deneme kaydı CRM'de bırakıldı.
- Kanıt: `.artifacts/aspx-crm-test/20260928-151354/result.json`.
  Deneme script'i `.artifacts/Test-AspxCrmWrite.ps1`.
- Test için başlatılan IIS Express durduruldu; geçici süreç ortam ayarları geri
  alındı. Kaynak Web.config'te yazma varsayılanı false kaldı. Gizli bilgiler
  sohbete, Git'e veya sonuç raporuna eklenmedi.
- Sonuç: Güncel ASPX'in yerelden TEST CRM'e kayıt açması artık bağımsız olarak
  doğrulandı. Azure'da ASPX yayını ve web formunun bağlanması halen yapılmadı.

### GitHub üzerinden ASPX yayın kararı ve bekleyen push

- Kullanıcı Azure portalı üzerinden işlem yerine mevcut GitHub deposuna kod
  gönderilmesini ve GitHub Actions'ın Azure'ya deploy etmesini istedi.
- GitHub deposu `cemaltarlan0990-hash/MarketingAutomation`, görünürlük **public**.
  Uzak main son kontrolünde `6893ec0`; yerel main `a7d5195` commit'inde.
- `a7d5195` güncel ASPX kodunu, güvenli Web.config'i, paketleme ve test script'lerini
  ve Windows üzerinde ASPX derleyip uygulama köküne gönderen workflow'u içerir.
  Önceki .NET API kaynak kodu korunur ancak yeni yayın onun endpoint'lerini
  (`/api/crm/leads`, `/health`) ASPX endpoint'leriyle değiştirir. `/aspx` sanal
  uygulaması oluşturulmadı ve bu yaklaşım kullanıcı portal yerine GitHub istediği
  için bırakıldı. Yeni adres `/CreateCrmRegistration.aspx` olacaktır.
- Yerel Release derleme ve paket üzerinde dört IIS Express kontrolü tekrar geçti.
  Commit'e alınan 12 dosyada yerel secret taraması geçti. Gerçek yerel ayarlar
  Git'in yok saydığı `src/CrmRegistrationGateway/Web.local.config` dosyasına taşındı;
  kaynak Web.config'te kimlik değerleri boş ve yayın paketi bu yerel dosyayı içermez.
- GitHub Actions, mevcut Azure publish-profile secret referansını kullanır;
  yayın profilini yerel bilgisayara indirmez. Manuel profil indirme izni verilmedi.
- **Push henüz yapılmadı.** Otomatik onay denetimi `git push github main` işlemini
  kamuya açık kaynak kodu yayını ve mevcut API'nin yerini alma sonuçlarına açık
  kullanıcı onayı bulunmadığı gerekçesiyle reddetti. Kullanıcıya bu iki sonucu
  kapsayan açık onay sorusu yöneltildi. Yanıt gelmeden push'u başka yolla deneme.
- Son hazırlanan paket `.artifacts/aspx-deploy/20260928-150553-447` altındadır.
  GitHub yayını `-EnableCrmWrites` ile yalnız TEST CRM'e yazmayı açar; ortam
  `CRM_WRITES_ENABLED` ayarı varsa ona öncelik verilir. Canlı CRM bağlantısı veya
  kayıt oluşturma bu çalışma kapsamında henüz doğrulanmadı.

### Son karar ve ASPX yayın hazırlığı

- Kullanıcı açıkça ASPX kullanılmasını istedi. Nihai form entegrasyonunu mevcut
  .NET 8 API'ye yönlendirme; ASPX servisinin geliştirme ve yayınını esas al.
- Web sitesi ekibi, form gönderimini işleyen sunucu kodundan alanları servise
  input olarak gönderecek. Admin panelinden veri çekme veya ayrı panel
  otomasyonu kararlaştırılmadı. KVKK/Marketing şimdilik CRM'e aktarılmayacak.
- Kullanıcı güncel ASPX kodunun Azure'a yayımlanmasını istedi. Release derlemesi
  ve kimlik bilgileri temizlenmiş yayın paketi hazırlandı. Paket üzerinde IIS
  Express kontrolleri geçti: GET 405, yanlış içerik türü 415, anahtarsız POST
  401, eksik zorunlu alan 400. Bu kontroller CRM'de kayıt oluşturmadı.
- Paket: `.artifacts/aspx-deploy/20260928-140956-374/CrmRegistrationGateway.Aspx.zip`.
  Tekrar üretim: `scripts/Build-AspxPackage.ps1`; yayın notları:
  `docs/ASPX_DEPLOYMENT.md`.
- Azure portalında `ALTTR-MarketingAutomation` Windows ve mevcut yığın Dotnet
  v10.0 olarak görüldü; son gösterilen dağıtım 18 Eylül. Kudu dosyaları mevcut
  ASP.NET Core API'ye ait. ASPX için henüz uzak dosya/yapılandırma değişikliği
  yapılmadı; ASPX yayımlandı denmemeli.
- Mevcut uygulamayı korumak için ASPX'in ayrı `/aspx` uygulama yolunda, ayrı
  fiziksel dizinde yayımlanması değerlendiriliyor; bu yol henüz oluşturulmadı.
- Yayın profilini indirme girişimi, hassas dağıtım kimlik bilgilerini alma ve
  saklama yetkisinin açık verilmediği gerekçesiyle otomatik onay denetimince
  reddedildi. Oturum tabanlı Kudu erişimi başarılı, tarayıcı üzerinden dosya
  yükleme/Cloud Shell etkileşimi tamamlanamadı. Kullanıcıdan yayın profilinin
  yalnız bu ASPX yayını için indirilip kullanılması konusunda açık izin istendi.
  İzin gelmeden bu engellenen işlemi yeniden deneme veya dolaylı yoldan yapma.

Bu bölüm önceki tarihli notlardan daha günceldir. Aşağıdaki eski hata ve
barındırma varsayımlarını güncel durum sanma. Özellikle önceki `403 /
0x80072560` ve `prvAppendToBusinessUnit` hataları artık açık sorun değildir.

### Kullanıcının doğruladığı güncel sonuç (28 Eylül 2026)

- CRM yetki sorunu giderildi ve CRM'de kayıt oluşturulabiliyor.
- Bu bilgi kullanıcının son doğrulamasına dayanır. Başarılı denemenin hangi
  servis/ortam üzerinden yapıldığı ve oluşturulan kaydın alanları bu notta henüz
  belgelenmedi; Azure -> form -> servis -> CRM akışının tamamı ayrıca
  doğrulanmış sayılmamalıdır.
- Sonraki çalışmada yetki hatasını devam eden engel olarak sunma. Odağı eksik
  alan eşlemelerine, yapılandırmaya ve hedef akışın uçtan uca doğrulanmasına ver.

### Amaç ve mimari sınırı

- Etkinlik katılımcısı bilgilerini TEST Dynamics 365/Dataverse ortamındaki
  **Müşteri Adayı** tablosuna (`lead`; Web API kümesi `leads`) aktarmak.
- Depoda iki ayrı servis vardır: Azure'daki mevcut .NET 8 API ve yerelde sınanan
  .NET Framework 4.8 ASPX/Web Forms servisi. ASPX'in Azure'a yayımlandığı veya
  Azure -> ASPX -> CRM akışının uçtan uca çalıştığı doğrulanmadı.
- ASPX Azure'da çalışmak zorunda değildir; Windows/IIS üzerinde de barınabilir.
  Nihai barındırma kararı netleşmedi. Workiom ekranları incelendi, fakat Workiom'un
  veri kaynağı olacağı ya da CRM'e bağlı olduğu doğrulanmadı.
- `scripts/Test-CrmLeadPermissions.ps1` **ASPX'i kullanmaz**; doğrudan Dataverse
  Web API'ye tanı isteği gönderir. PowerShell nihai kullanıcı arayüzü değildir.

### Önceki test sonuçları (24-25 Eylül 2026; yetki hatası çözüldü)

- TEST URL: `https://altiumtr-test.crm4.dynamics.com/`. Client ID ile eşleşen
  Application User ID: `3db1e493-44b7-f111-aaac-7ced8d4256d9`.
- Entra token ve CRM `WhoAmI` başarılı; Lead okuma HTTP 200. Yerel ASPX
  `TestCrmConnection.aspx` art arda iki kez HTTP 200 döndü. ASPX istek yöntemi,
  içerik türü, API anahtarı ve zorunlu giriş alanı kontrolleri geçti.
- Yerel ASPX `CreateCrmRegistration.aspx` kayıt denemesi genel HTTP 502 döndü.
  Doğrudan Dataverse'e yalnızca Lead alanlarıyla yapılan oluşturma denemesi
  HTTP 400 / `0x80040265` ile reddedildi: uygulama kullanıcısında
  `prvAppendToBusinessUnit` eksik. İstekte Business Unit alanı gönderilmedi;
  bu ek kontrolün sunucu tarafındaki kesin tetikleyicisi henüz belirlenmedi.
- Başarısız deneme sonrasında test e-postasıyla sorguda 0 Lead bulundu. Bu sonuç
  yalnız 24-25 Eylül'deki denemeye aittir; sonradan kayıt oluşturma başarılı oldu.
- 25 Eylül read-only yetki sorgusunda `prvCreateLead`, `prvReadLead`,
  `prvWriteLead`, `prvAppendLead`, `prvAppendToLead` Global düzeyde **vardı**;
  `prvAppendToBusinessUnit` **yoktu**. Bu, sonradan giderilen yetki sorununun
  tarihsel tanısıdır.
- Çözüm derlendi; .NET 8 test projesinin 18 testi geçti. Yerel makinede .NET 8
  runtime olmadığından testler .NET 10'a major roll-forward ile çalıştırıldı;
  bu, yerel .NET 8 runtime doğrulaması değildir.

### CRM alanları ve açık kod işi

- Kullanıcının CRM formu ekranlarında şu `lead` alan adları görüldü:
  `subject`, `companyname`, `firstname`, `lastname`, `twbs_department`,
  `twbs_isunvani`, `twbs_sehir`, `twbs_ulke`, `telephone1`,
  `emailaddress1`, `mobilephone`.
- TEST CRM metadatasında `subject`, `twbs_sehir`, `twbs_ulke` ve ayrıca
  `prioritycode` ApplicationRequired; şehir/ülke **Lookup** tipindedir ve
  mevcut ilgili kayıtlara bağlanmalıdır. `companyname`, `firstname`,
  `lastname` Recommended; manuel departman/unvan ve iletişim alanları
  metadata düzeyinde isteğe bağlıdır. Web API yalnız SystemRequired alanları
  otomatik zorlar; form/iş kuralı/eklenti ayrıca gereklilik koyabilir.
- ASPX `CrmRegistrationWriter` şu an yalnız ad, soyad, e-posta, telefon ve
  şirket gönderiyor; `subject`, manuel departman/unvan, şehir/ülke lookup'ları
  ve cep telefonu eksik. .NET 8 API `twbs_sehir` alanını string gibi gönderiyor
  ve `twbs_ulke` eşleştirmiyor; bu lookup tasarımı düzeltilmeli.
- `Web.config` içinde kalıcı ASPX yazma ayarı `false`; inbound API key ve hedef
  tablo/alan eşleştirmeleri boş. Yerel ASPX testinde bunlar yalnız test işlemi
  için geçici ortam değişkenleriyle verildi. Kod ve konfigürasyon tamamlanmadan
  üretim/uçtan uca başarı iddia edilmemeli.

### Güvenlik ve sonraki adım

- Client Secret daha önce sohbet içinde paylaşılmış ve Git tarafından izlenen
  `Web.config` dosyasına yazılmış. Değeri bu dosyaya kopyalama veya çıktıya
  dökme. Güvenli ortam ayarlarına taşıma ve secret yenileme planlanmalı.
- Kullanıcı gerçek test alan değerlerini verirse alan eşlemelerini ve lookup
  referanslarını netleştir. Yeni bir uçtan uca denemede benzersiz test e-postası
  kullan; deneme sonrasında kaydın ve beklenen alanların CRM'de oluştuğunu sorgula.

## Güncel durum: 2026-09-22

### Netleşen nihai gereksinim

- Etkinlik katılımcısı formda ad-soyad, e-posta, telefon, şirket ve ilgili diğer
  bilgileri doldurup **Gönder** düğmesine bastığında bilgiler Azure'da çalışan bir
  **ASPX / ASP.NET Web Forms servisine** gönderilecek.
- ASPX servisi bilgileri Dynamics 365 / Dataverse Lead alanlarına eşleyip kaydı
  oluşturacak ve sonucu forma döndürecek.
- PowerShell nihai kullanıcı arayüzü değildir; Azure servisinin CRM bağlantısını ve
  tek/toplu kayıt davranışını sınamak için kullanılan yardımcı araçtır.
- Şu anda Azure'da yayımlı olan .NET 8 API çalışmaya devam ediyor. Nihai ASPX
  gereksinimi için `src/CrmRegistrationGateway` projesi yeniden aktif geliştirme
  hedefidir; henüz Azure'a yayımlanmamıştır.

### CRM bağlantısında kesinleşen hata

- Azure API sağlıklı çalışıyor ve `INBOUND_API_KEY` doğrulaması geçiyor.
- Microsoft Entra client-credentials token'ı başarıyla alınıyor.
- Test hedefi `https://altiumtr-test.crm4.dynamics.com`.
- Dataverse'e yapılan ilk Lead okuma sorgusu HTTP `403` ve CRM hata kodu
  `0x80072560` ile reddedildi.
- Bu kod, uygulama kullanıcısının hedef Dataverse organizasyonunun üyesi olmadığını
  gösteriyor. Sorun kişisel CRM hesabının bağlı olmaması değil; Azure'daki
  `CLIENT_ID` ile eşleşen Application User'ın test ortamında bulunmaması, etkin
  olmaması veya farklı Client ID ile tanımlanmasıdır.
- Yöneticinin test ortamında **Ayarlar > Kullanıcılar ve izinler > Uygulama
  kullanıcıları** bölümünde Azure `CLIENT_ID` değeriyle aynı Application ID'yi
  doğrulaması gerekir. Uygulama kullanıcısına Lead için en az Read ve Create
  yetkileri atanmalıdır.
- `AuthType=ClientSecret` bağlantısında uygulama kimliğini `ClientId` belirler.
  Kişisel kullanıcı adı kullanılmaz. `crmuseraltium` / `Crm.UserKey` şu anda boş ve
  yalnız şirketin mevcut entegrasyonu gerçekten gerektiriyorsa doldurulmalıdır.

### ASPX bağlantı katmanı refaktörü

- Kullanıcının sağladığı mevcut şirket kodu referans alınarak
  `src/CrmRegistrationGateway/Services/AltiumService.cs` oluşturuldu.
- Yapı `AltiumService.GetService()` singleton modelini, `OrganizationService` ve
  `ControlID` özelliklerini koruyor.
- `CrmConnection` artık CRM işlemlerini `AltiumService` üzerinden yürütüyor.
- `crmserverurlaltium` ve `crmuseraltium` eski config adları destekleniyor; düzenli
  `Crm.*` ayar adları da korunuyor.
- Client ID, tenant ID ve secret kaynak koda gömülmüyor; config/Azure ortam
  değişkenlerinden okunuyor.
- Referans örnekteki `using` bloğu sonunda kapatılmış CRM proxy'sinin singleton'da
  tutulması problemi giderildi. Bağlantı canlı tutuluyor ve eşzamanlı ASP.NET
  isteklerindeki CRM çağrıları kilitle korunuyor.
- ASPX `.NET Framework 4.8` projesi Visual Studio MSBuild ile başarıyla derlendi.
- Bu değişiklikler henüz commit edilmedi veya Azure'a yayımlanmadı.

Değişen ASPX dosyaları:

- `src/CrmRegistrationGateway/Services/AltiumService.cs` (yeni)
- `src/CrmRegistrationGateway/Services/CrmConnection.cs`
- `src/CrmRegistrationGateway/Configuration/CrmOptions.cs`
- `src/CrmRegistrationGateway/Web.config`
- `src/CrmRegistrationGateway/CrmRegistrationGateway.csproj`

### Güvenlik notu

- Bir Client Secret sohbet içinde açık olarak paylaşıldı. Bu değer artık açığa
  çıkmış kabul edilmeli, IT tarafından iptal edilmeli ve yenisi oluşturulmalıdır.
- Eski/paylaşılan secret hiçbir dosyaya kaydedilmedi. Yeni secret da sohbete veya
  Git'e yazılmamalı; yalnız Azure ortam değişkeni/config üzerinden verilmelidir.
- `INBOUND_API_KEY` için daha önce PowerShell komut metni yanlışlıkla Azure değerine
  kaydedilmişti. Rastgele anahtar üretilerek düzeltildi; gerçek değer bu dosyada
  tutulmaz.

### Alan eşlemeleri ve kalan ASPX işi

- CRM mantıksal adları .NET 8 API'de kayıtlıdır: `firstname`, `lastname`,
  `emailaddress1`, `companyname`, `twbs_department`, `twbs_isunvani`,
  `twbs_sehir`, `telephone1`, `mobilephone`, `subject`.
- ASPX SDK tarafında tablo mantıksal adı `lead`, Web API entity-set adı `leads`tir.
- Mevcut ASPX sürümü yalnız ad, soyad, e-posta, telefon ve şirketi yazıyor.
  Departman, unvan, şehir, etkinlik/başlık ve ayrı telefon alanları ile e-posta
  bazlı tekrar kayıt kontrolü henüz ASPX sürümüne taşınmadı.

### Sonraki adımlar

1. Açığa çıkan Client Secret'ı iptal edip yenisini oluştur.
2. Azure `CLIENT_ID` ile test CRM Application User.Application ID eşleşmesini
   yöneticiyle doğrula; eksikse uygulama kullanıcısını test ortamına ekle.
3. Application User'a Lead Read ve Create içeren güvenlik rolünü ata.
4. Visual Studio/IIS Express'te önce `TestCrmConnection.aspx`, sonra tek test kaydı
   için `CreateCrmRegistration.aspx` çalıştır.
5. Eksik CRM alanlarını ve tekrar kayıt kontrolünü ASPX sürümüne ekle.
6. ASPX için Windows Azure App Service yayın hedefini netleştir ve testten sonra
   formun Gönder işlemini ASPX servisine bağla.

## Güncel durum: 2026-09-18

### Yayınlanan tanılama düzeltmesi

- Yerel commit `6893ec012669be2ed8f71b0933808172a3bdd4e2`: API yanıtına yalnızca güvenli MSAL/AADSTS kodları, token HTTP durumu ve önerilen kontrol ekleniyor. PowerShell aktarım aracı bunları doğrudan gösterip CSV raporuna kaydediyor; bilinen token hatasında uzun hata yığını yerine açıklama ve çıkış kodu 1 veriyor.
- 18 yerel test .NET 10 çalışma ortamında başarılı; Windows PowerShell 5.1 sahte yanıt kontrolü doğrudan çıktı, CSV alanları, hata yığınının olmaması ve ilk hatada durmayı doğruladı. Yeni sürümün GitHub Actions .NET 8 test aşaması da başarılı.
- Kullanıcı yeni kapsamı açıkça onayladı ("tamam gönder"). `6893ec0`, GitHub `main` dalına gönderildi; GitHub Actions çalışması `35323507138` tamamen başarılı: test, publish ve Azure deploy tamamlandı. Dağıtım sonrası `/health` yanıtı `{"status":"healthy"}`.
- Sonraki adım: kullanıcı `Import-EventRegistrations.ps1 -Send -Limit 1` komutunu gerçek INBOUND_API_KEY ile çalıştıracak. Artık PowerShell'de kimlik hata kodu, Microsoft AADSTS kodu, token HTTP durumu ve önerilen kontrol görünecek. CRM hatasının gerçek nedeni yeni yanıt alınana kadar bilinmiyor; secret/token kullanıcıdan sohbet içinde istenmemeli.

### Önceki günlük sürümü ve aktarım bilgileri

- Kullanıcının onayıyla Azure günlük düzeltmesi GitHub `main` dalına gönderildi.
- Commit: `777138ab43c83e26871a47332b652b080631a0a3`.
- GitHub Actions çalışması `35320639977` başarılı: .NET 8 test, publish ve Azure deploy tamamlandı.
- Dağıtım sonrası gerçek Azure adresinde `/health` yanıtı `{"status":"healthy"}`.
- Uygulama `AddAzureWebAppDiagnostics()` ile App Service dosya günlüklerine yazıyor.
- MSAL token hataları ErrorCode, StatusCode ve mesajda varsa yalnızca AADSTS kodu (IdentityCode) ile loglanıyor.
- Kullanıcı INBOUND_API_KEY ayarını ekledi; son canlı tek kişilik denemede anahtar kabul edildi, fakat CRM token isteği `authentication_error / CRM authentication failed` ile durdu. CRM kaydı oluşmadı.
- JSON aktarım aracı Windows PowerShell 5.1 uyumlu; önizlemede 20 satır, 19 farklı kişi, 1 dosya içi tekrar ve 0 geçersiz satır var. Toplu aktarım aracı GitHub'a gönderildi; kişisel verileri içeren JSON ve raporlar gönderilmedi.

## Amaç

Etkinlik katılımcı verilerini HTTP üzerinden alıp Microsoft Dynamics 365 /
Dataverse ortamında Lead kaydı oluşturmak. Mevcut çalışan doğrulama servisi .NET 8
ASP.NET Core Web API'dir; son kullanıcı gereksinimi Azure'da çalışan ASPX servisine
geçmektir.

- Azure App Service: `ALTTR-MarketingAutomation`
- Resource Group: `Altium_TR`
- Sağlık kontrolü: `https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net/health`
- API endpoint: `POST /api/crm/leads`

## Mevcut deployment hedefi

- API projesi: `src/CrmRegistrationGateway.Functions/CrmRegistrationGateway.Api.csproj`
- Test projesi: `tests/CrmRegistrationGateway.Functions.Tests/CrmRegistrationGateway.Functions.Tests.csproj`
- Runtime: `.NET 8`
- `src/CrmRegistrationGateway` altındaki .NET Framework 4.8 Web Forms projesi yeni
  ASPX gereksinimi için geliştirme hedefidir; henüz mevcut App Service'e deploy
  edilmemiştir.

## Git durumu

- Yerel proje: `C:\Users\AltiumTRITIntern2\Documents\ChatGPT\etkinlik`
- Portable Git: `C:\Users\AltiumTRITIntern2\Downloads\PortableGit`
- Geçerli dal: `main`
- Yerel, GitHub ve Azure'da son commit: `6893ec0`; `main`, `github/main` ile eşit. Azure yayını ve sağlık kontrolü başarılı.
- Proje bağlam dosyası ve tek kişilik yardımcı araç bilgisayarda duruyor; toplu aktarım aracı yeni yerel commit'e eklendi.

Remote bağlantıları:

- `github`: `https://github.com/cemaltarlan0990-hash/MarketingAutomation.git`
- `origin`: `https://altiumgroup@dev.azure.com/altiumgroup/ALTTR/_git/ALTTR_MarketingAutomation`

Branch durumu:

- GitHub deployment dalı: `main`
- Azure Repos üzerinde yüklenmiş dal: `codex/crm-api`
- `codex/crm-api` son doğrulanan commit: `6501a03`

GitHub repository görünürlüğünün şirket politikalarına uygun biçimde `Private`
olduğu ayrıca doğrulanmalıdır.

## GitHub Actions ve deployment

Aktif tek workflow:

`/.github/workflows/main_alttr-marketingautomation.yml`

Workflow `main` dalına her push'ta şu sırayla çalışır:

1. .NET 8 kurulumu
2. Paket restore
3. Testler
4. Doğru API projesinin Release publish işlemi
5. Azure App Service'e deployment

Azure Deployment Center'ın ürettiği publish-profile secret referansı korunmuştur.
Önceki ikinci workflow kaldırılmıştır; böylece aynı commit için çift deployment
çalışmamalıdır.

Kod GitHub'a başarıyla gönderildi ve build'in başladığı görüldü. Son build ve
deployment sonucunun GitHub Actions ile Azure Deployment Center günlüklerinden
doğrulanması hâlâ gerekir.

## App Service ayarlarına ilişkin önceki notlar (2026-09-16)

2026-09-16 itibarıyla aşağıdaki zorunlu App Service ortam değişkenlerinin Azure'da
bulunmadığı bildirildi:

- `CRM_URL`
- `TENANT_ID`
- `CLIENT_ID`
- `CLIENT_SECRET`
- `INBOUND_API_KEY` (en az 32 karakter)

İsteğe bağlı ayarlar:

- `CRM_API_VERSION` (varsayılan `v9.2`)
- `CRM_REQUEST_TIMEOUT_SECONDS` (varsayılan `30`)
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

Bu beş zorunlu değer eksikken uygulama başlangıç doğrulamasında kapanır ve
`/health` yanıt vermez. Değerler GitHub'a veya kaynak koda yazılmamalıdır;
Azure App Service > Ayarlar > Ortam değişkenleri altında tanımlanmalıdır.

Dataverse tarafında `CLIENT_ID` ile eşleşen Application User bulunmalı ve Lead
tablosunda en az Read ile Create yetkilerine sahip olmalıdır. Özel alanlar
`twbs_department`, `twbs_isunvani` ve `twbs_sehir` test ortamında doğrulanmalıdır.

## Sonraki normal GitHub güncellemesi

`main` dalındayken:

```cmd
git add .
git commit -m "Degisiklik aciklamasi"
git push github main
```

`--force` kullanılmamalıdır. Push sonrasında GitHub Actions otomatik olarak build,
test ve deployment çalıştırır.

## İlk doğrulama sırası

1. GitHub Actions çalışmasının tamamen yeşil olduğunu kontrol et.
2. Azure Deployment Center günlüklerinde deployment başarısını kontrol et.
3. App Service zorunlu ortam değişkenlerini ekle ve uygulamayı yeniden başlat.
4. `/health` endpoint'inden HTTP 200 ve `{"status":"healthy"}` yanıtını doğrula.
5. Gizli `INBOUND_API_KEY` ile test Lead isteği gönder.
