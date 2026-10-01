param(
    [string]$BaseUrl = "http://localhost:8080",
    [Parameter(Mandatory = $true)]
    [string]$ApiKey,
    [string]$EventApiKey
)

$ErrorActionPreference = "Stop"
$uri = $BaseUrl.TrimEnd('/') + "/CreateCrmRegistration.aspx"

function Invoke-Check {
    param(
        [string]$Name,
        [int]$ExpectedStatus,
        [hashtable]$Request
    )

    $response = Invoke-WebRequest @Request -SkipHttpErrorCheck
    if ($response.StatusCode -ne $ExpectedStatus) {
        throw "$Name başarısız. Beklenen: $ExpectedStatus, alınan: $($response.StatusCode). Cevap: $($response.Content)"
    }

    $json = $response.Content | ConvertFrom-Json
    if ($json.success -ne $false) {
        throw "$Name başarısız. Hata cevabında success=false bekleniyordu."
    }

    Write-Host "[OK] $Name -> HTTP $($response.StatusCode)"
}

Invoke-Check `
    -Name "GET reddediliyor" `
    -ExpectedStatus 405 `
    -Request @{ Uri = $uri; Method = "Get" }

Invoke-Check `
    -Name "Yanlış Content-Type reddediliyor" `
    -ExpectedStatus 415 `
    -Request @{ Uri = $uri; Method = "Post"; ContentType = "text/plain"; Body = "{}" }

Invoke-Check `
    -Name "Anahtarsız POST reddediliyor" `
    -ExpectedStatus 401 `
    -Request @{
        Uri = $uri
        Method = "Post"
        ContentType = "application/x-www-form-urlencoded"
        Body = "firstname=Test"
    }

Invoke-Check `
    -Name "Eksik zorunlu alan reddediliyor" `
    -ExpectedStatus 400 `
    -Request @{
        Uri = $uri
        Method = "Post"
        ContentType = "application/x-www-form-urlencoded"
        Headers = @{ "X-Integration-Key" = $ApiKey }
        Body = @{
            lastname = "Katilimci"
            email = "contract-test@example.invalid"
        }
    }

foreach ($case in @(
    @{ Name = 'JSON: Anahtarsız istek'; Status = 401; Headers = @{}; Body = '{}' },
    @{ Name = 'JSON: Yanlış anahtar'; Status = 401; Headers = @{ 'X-Integration-Key' = 'wrong-key' }; Body = '{}' },
    @{ Name = 'JSON: Bozuk gövde'; Status = 400; Headers = @{ 'X-Integration-Key' = $ApiKey }; Body = '{' },
    @{ Name = 'JSON: Onay eksik'; Status = 400; Headers = @{ 'X-Integration-Key' = $ApiKey }; Body = '{"firstName":"Test"}' },
    @{ Name = 'JSON: Honeypot'; Status = 403; Headers = @{ 'X-Integration-Key' = $ApiKey }; Body = '{"website":"spam"}' },
    @{ Name = 'JSON: Büyük gövde'; Status = 413; Headers = @{ 'X-Integration-Key' = $ApiKey }; Body = ('x' * 17000) },
    @{ Name = 'JSON: Yanlış etkinlik adresi'; Status = 400; Headers = @{ 'X-Integration-Key' = $ApiKey }; Body = '{"consent":true,"firstName":"Test","lastName":"Katilimci","email":"test@example.invalid","eventTitle":"Test","eventUrl":"https://other.example/tr/etkinlik-kayit/test"}' },
    @{ Name = 'JSON: CAPTCHA olmadan geçerli alanlar CRM aşamasına ulaşır'; Status = 503; Headers = @{ 'X-Integration-Key' = $ApiKey }; Body = '{"consent":true,"firstName":"Test","lastName":"Katilimci","email":"test@example.invalid","eventTitle":"TEST Kayıttır Silmeyin","eventUrl":"https://altium.net/tr/etkinlik-kayit/test-kayittir-silmeyin"}' }
)) {
    Invoke-Check -Name $case.Name -ExpectedStatus $case.Status -Request @{
        Uri = $uri; Method = 'Post'; ContentType = 'application/json'; Headers = $case.Headers; Body = $case.Body
    }
}

# EtkinlikKayit.aspx: Power Automate etkinlik kaydı endpoint'i (JSON + x-api-key)
$eventUri = $BaseUrl.TrimEnd('/') + "/EtkinlikKayit.aspx"

Invoke-Check `
    -Name "EtkinlikKayit: GET reddediliyor" `
    -ExpectedStatus 405 `
    -Request @{ Uri = $eventUri; Method = "Get" }

Invoke-Check `
    -Name "EtkinlikKayit: Yanlış Content-Type reddediliyor" `
    -ExpectedStatus 415 `
    -Request @{ Uri = $eventUri; Method = "Post"; ContentType = "application/x-www-form-urlencoded"; Body = "adSoyad=Test" }

Invoke-Check `
    -Name "EtkinlikKayit: Anahtarsız POST reddediliyor" `
    -ExpectedStatus 401 `
    -Request @{ Uri = $eventUri; Method = "Post"; ContentType = "application/json"; Body = '{"adSoyad":"Test Katilimci"}' }

if ($EventApiKey) {
    Invoke-Check `
        -Name "EtkinlikKayit: Eksik zorunlu alan reddediliyor" `
        -ExpectedStatus 400 `
        -Request @{
            Uri = $eventUri
            Method = "Post"
            ContentType = "application/json"
            Headers = @{ "x-api-key" = $EventApiKey }
            Body = '{"adSoyad":"","eposta":"contract-test@example.invalid"}'
        }

    Invoke-Check `
        -Name "EtkinlikKayit: Geçersiz JSON reddediliyor" `
        -ExpectedStatus 400 `
        -Request @{
            Uri = $eventUri
            Method = "Post"
            ContentType = "application/json"
            Headers = @{ "x-api-key" = $EventApiKey }
            Body = '{"adSoyad":'
        }
}

$webUri = $BaseUrl.TrimEnd('/') + '/WebLead.aspx'
Invoke-Check -Name 'WebLead: GET reddediliyor' -ExpectedStatus 405 -Request @{ Uri = $webUri; Method = 'Get' }
Invoke-Check -Name 'WebLead: İzin verilmeyen origin' -ExpectedStatus 403 -Request @{
    Uri = $webUri; Method = 'Post'; ContentType = 'application/json'; Headers = @{ Origin = 'https://attacker.example' }; Body = '{}'
}
Invoke-Check -Name 'WebLead: Origin olmadan gönderim' -ExpectedStatus 403 -Request @{
    Uri = $webUri; Method = 'Post'; ContentType = 'application/json'; Body = '{}'
}
$preflight = Invoke-WebRequest -Uri $webUri -Method Options -SkipHttpErrorCheck -Headers @{
    Origin = 'http://localhost:3000'; 'Access-Control-Request-Method' = 'POST'; 'Access-Control-Request-Headers' = 'content-type'
}
if ($preflight.StatusCode -ne 204 -or $preflight.Headers['Access-Control-Allow-Origin'] -ne 'http://localhost:3000') {
    throw ("WebLead preflight failed. Status={0}; Headers={1}; Body={2}" -f $preflight.StatusCode, ($preflight.Headers | ConvertTo-Json -Compress), $preflight.Content)
}
Write-Host '[OK] WebLead: OPTIONS preflight -> HTTP 204'
Invoke-Check -Name 'WebLead: Büyük gövde reddediliyor' -ExpectedStatus 413 -Request @{
    Uri = $webUri; Method = 'Post'; ContentType = 'application/json'; Headers = @{ Origin = 'http://localhost:3000' }; Body = ('x' * 17000)
}
Invoke-Check -Name 'WebLead: Yanlış Content-Type' -ExpectedStatus 415 -Request @{
    Uri = $webUri; Method = 'Post'; ContentType = 'text/plain'; Headers = @{ Origin = 'http://localhost:3000' }; Body = '{}'
}
foreach ($case in @(
    @{ Name = 'Bozuk JSON'; Status = 400; Body = '{"firstName":' },
    @{ Name = 'String onay reddediliyor'; Status = 400; Body = '{"consent":"true"}' },
    @{ Name = 'Honeypot reddediliyor'; Status = 403; Body = '{"consent":true,"website":"spam"}' },
    @{ Name = 'CAPTCHA eksik'; Status = 403; Body = '{"consent":true,"firstName":"Test","lastName":"Katilimci","email":"test@example.invalid"}' },
    @{ Name = 'Eksik güvenlik ayarı güvenli kapanıyor'; Status = 503; Body = '{"consent":true,"firstName":"Test","lastName":"Katilimci","email":"test@example.invalid","message":"Talep","captchaToken":"test-token","website":""}' }
)) {
    Invoke-Check -Name ('WebLead: ' + $case.Name) -ExpectedStatus $case.Status -Request @{
        Uri = $webUri; Method = 'Post'; ContentType = 'application/json'; Headers = @{ Origin = 'http://localhost:3000' }; Body = $case.Body
    }
}
Invoke-Check -Name 'WebLead: Gönderim limiti' -ExpectedStatus 429 -Request @{
    Uri = $webUri; Method = 'Post'; ContentType = 'application/json'; Headers = @{ Origin = 'http://localhost:3000' }; Body = '{}'
}
Write-Host "Tüm endpoint kontrolleri başarılı. CRM'e istek gönderilmedi."
