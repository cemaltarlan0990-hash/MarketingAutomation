param(
    [switch]$CreateTestLead,
    [string]$Email = 'test-izin-20260923151143@example.com'
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$configPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\CrmRegistrationGateway\Web.config'
[xml]$config = Get-Content -Raw -LiteralPath $configPath
$settings = @{}
$config.configuration.appSettings.add | ForEach-Object {
    $settings[$_.key] = $_.value
}

$crmUrl = ([string]$settings['Crm.Url']).TrimEnd('/')
if ($crmUrl -ne 'https://altiumtr-test.crm4.dynamics.com') {
    throw 'Hedef URL beklenen test CRM ortamı değil.'
}

foreach ($key in @('Crm.ClientId', 'Crm.TenantId', 'Crm.ClientSecret')) {
    if ([string]::IsNullOrWhiteSpace([string]$settings[$key])) {
        throw "$key ayarı boş."
    }
}

$tokenUri = "https://login.microsoftonline.com/$($settings['Crm.TenantId'])/oauth2/v2.0/token"
$tokenForm = @{
    client_id = $settings['Crm.ClientId']
    client_secret = $settings['Crm.ClientSecret']
    grant_type = 'client_credentials'
    scope = "$crmUrl/.default"
}
$token = Invoke-RestMethod -Method Post -Uri $tokenUri -ContentType 'application/x-www-form-urlencoded' -Body $tokenForm

Add-Type -AssemblyName System.Net.Http
$http = [System.Net.Http.HttpClient]::new()
$http.DefaultRequestHeaders.Authorization =
    [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token.access_token)
[void]$http.DefaultRequestHeaders.TryAddWithoutValidation('Accept', 'application/json')

try {
    $who = $http.GetAsync("$crmUrl/api/data/v9.2/WhoAmI").GetAwaiter().GetResult()
    Write-Host "CRM bağlantısı: HTTP $([int]$who.StatusCode)"
    if (-not $who.IsSuccessStatusCode) {
        throw 'CRM bağlantısı başarısız.'
    }

    $leadRead = $http.GetAsync(
        $crmUrl + '/api/data/v9.2/leads?$select=leadid&$top=1'
    ).GetAwaiter().GetResult()
    Write-Host "Lead okuma: HTTP $([int]$leadRead.StatusCode)"
    if (-not $leadRead.IsSuccessStatusCode) {
        throw 'Lead okuma başarısız.'
    }

    if (-not $CreateTestLead) {
        Write-Host 'Kayıt oluşturma denenmedi. Denemek için -CreateTestLead ekleyin.'
        return
    }

    $filter = [Uri]::EscapeDataString("emailaddress1 eq '$Email'")
    $existing = $http.GetAsync(
        $crmUrl + '/api/data/v9.2/leads?$select=leadid&$filter=' + $filter
    ).GetAwaiter().GetResult()
    if (-not $existing.IsSuccessStatusCode) {
        throw "Tekrar kontrolü başarısız: HTTP $([int]$existing.StatusCode)"
    }

    $existingJson = $existing.Content.ReadAsStringAsync().GetAwaiter().GetResult() |
        ConvertFrom-Json
    if (@($existingJson.value).Count -gt 0) {
        Write-Host "Test kaydı zaten var: $($existingJson.value[0].leadid)"
        return
    }

    $payload = @{
        firstname = 'Test'
        lastname = 'IzinKontrol'
        emailaddress1 = $Email
        companyname = 'ALTTR CRM API Test'
    } | ConvertTo-Json -Compress
    $content = [System.Net.Http.StringContent]::new(
        $payload, [Text.Encoding]::UTF8, 'application/json'
    )
    [void]$http.DefaultRequestHeaders.TryAddWithoutValidation(
        'Prefer', 'return=representation'
    )
    $created = $http.PostAsync(
        $crmUrl + '/api/data/v9.2/leads', $content
    ).GetAwaiter().GetResult()
    $responseText = $created.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $result = if ($responseText) { $responseText | ConvertFrom-Json } else { $null }

    Write-Host "Lead oluşturma: HTTP $([int]$created.StatusCode)"
    if ($created.IsSuccessStatusCode) {
        Write-Host "Kayıt ID: $($result.leadid)"
        Write-Host "Test e-postası: $Email"
        return
    }

    Write-Host "Dataverse kodu: $($result.error.code)"
    Write-Host "Dataverse hata mesajı: $($result.error.message)"
    if ($created.Headers.Contains('x-ms-service-request-id')) {
        $requestId = [string]::Join(
            ', ', $created.Headers.GetValues('x-ms-service-request-id')
        )
        Write-Host "Dataverse istek kimliği: $requestId"
    }
    $match = [regex]::Match(
        [string]$result.error.message, 'missing\s+(\w+)\s+privilege'
    )
    if ($match.Success) {
        Write-Host "Eksik yetki: $($match.Groups[1].Value)"
    }
    else {
        Write-Host 'Eksik yetki adı hata mesajında ayrıştırılamadı.'
    }
    exit 2
}
finally {
    $http.Dispose()
    $token.access_token = $null
    $tokenForm.client_secret = $null
}
