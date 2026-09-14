param(
    [string]$BaseUrl = "http://localhost:7071",
    [Parameter(Mandatory = $true)]
    [string]$ApiKey
)

$ErrorActionPreference = "Stop"
$uri = $BaseUrl.TrimEnd('/') + "/api/crm/leads"

$unauthorizedResponse = Invoke-WebRequest `
    -Uri $uri `
    -Method Post `
    -ContentType "application/json" `
    -Body '{}' `
    -SkipHttpErrorCheck

if ($unauthorizedResponse.StatusCode -ne 401) {
    throw "Anahtarsız istek reddedilmedi. Alınan HTTP status: $($unauthorizedResponse.StatusCode)"
}

Write-Host "[OK] Anahtarsız istek reddediliyor -> HTTP 401"

function Assert-Status {
    param(
        [string]$Name,
        [int]$ExpectedStatus,
        [hashtable]$Request
    )

    $Request.Headers = @{ "X-Integration-Key" = $ApiKey }
    $response = Invoke-WebRequest @Request -SkipHttpErrorCheck
    if ($response.StatusCode -ne $ExpectedStatus) {
        throw "$Name başarısız. Beklenen: $ExpectedStatus, alınan: $($response.StatusCode). Cevap: $($response.Content)"
    }

    Write-Host "[OK] $Name -> HTTP $($response.StatusCode)"
}

Assert-Status `
    -Name "Yanlış Content-Type reddediliyor" `
    -ExpectedStatus 415 `
    -Request @{ Uri = $uri; Method = "Post"; ContentType = "text/plain"; Body = "{}" }

Assert-Status `
    -Name "Geçersiz JSON reddediliyor" `
    -ExpectedStatus 400 `
    -Request @{ Uri = $uri; Method = "Post"; ContentType = "application/json"; Body = "{" }

Assert-Status `
    -Name "Eksik zorunlu alanlar reddediliyor" `
    -ExpectedStatus 400 `
    -Request @{
        Uri = $uri
        Method = "Post"
        ContentType = "application/json"
        Body = '{"lastName":"Tarlan"}'
    }

Write-Host "Endpoint contract kontrolleri başarılı. CRM'e istek gönderilmedi."
