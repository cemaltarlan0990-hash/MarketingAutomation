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
    -Request @{ Uri = $uri; Method = "Post"; ContentType = "application/json"; Body = "{}" }

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

Write-Host "Tüm endpoint kontrolleri başarılı. CRM'e istek gönderilmedi."
