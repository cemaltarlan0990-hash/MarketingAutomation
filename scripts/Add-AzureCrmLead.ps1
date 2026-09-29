$ErrorActionPreference = 'Stop'
$serviceUrl = 'https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net'

function Read-RequiredValue([string]$Prompt) {
    do {
        $value = (Read-Host $Prompt).Trim()
    } while ([string]::IsNullOrWhiteSpace($value))
    return $value
}

$requestHeaders = @{}
$plainKey = $null
$secureKey = $null

try {
    Write-Host 'Azure servisinin calisip calismadigi kontrol ediliyor...'
    $health = Invoke-RestMethod -Uri "$serviceUrl/health" -TimeoutSec 60
    if ($health.status -ne 'healthy') {
        throw 'Servis saglik kontrolunden gecemedi.'
    }

    Write-Host 'Girdiginiz bilgiler CRM test ortaminda bir Musteri Adayi kaydi olusturacak.'
    $registration = [ordered]@{
        firstName = Read-RequiredValue 'Ad'
        lastName = Read-RequiredValue 'Soyad'
        email = Read-RequiredValue 'E-posta'
    }
    try {
        $parsedEmail = [System.Net.Mail.MailAddress]::new($registration.email)
        if ($parsedEmail.Address -ne $registration.email) { throw 'Invalid email' }
    } catch {
        throw 'Gecerli bir e-posta adresi girin.'
    }
    $company = (Read-Host 'Sirket (istege bagli, bos birakabilirsiniz)').Trim()
    $eventName = (Read-Host 'Etkinlik adi (istege bagli, bos birakabilirsiniz)').Trim()
    if ($company) { $registration.company = $company }
    if ($eventName) { $registration.eventName = $eventName }

    $secureKey = Read-Host 'Azure INBOUND_API_KEY degerini girin (ekranda gorunmez)' -AsSecureString
    $plainKey = [System.Net.NetworkCredential]::new('', $secureKey).Password
    if ($plainKey.Length -lt 32) {
        throw 'Anahtar en az 32 karakter olmali; Azure ile ayni degeri girin.'
    }
    $requestHeaders['X-Integration-Key'] = $plainKey
    $body = [System.Text.Encoding]::UTF8.GetBytes(($registration | ConvertTo-Json))
    $result = Invoke-RestMethod -Method Post -Uri "$serviceUrl/api/crm/leads" `
        -Headers $requestHeaders -ContentType 'application/json; charset=utf-8' `
        -Body $body -TimeoutSec 90

    if (-not $result.success) { throw 'Servis kayit islemini tamamlayamadi.' }
    switch ($result.status) {
        'created' { Write-Host 'CRM Musteri Adayi kaydi olusturuldu.' -ForegroundColor Green }
        'already_exists' { Write-Host 'Bu e-posta ile CRM kaydi zaten var; tekrar olusturulmadi.' }
        default { Write-Host "Servis sonucu: $($result.status)" }
    }
    Write-Host "CRM kayit kimligi: $($result.crmId)"
} catch {
    Write-Host 'Kayit islemi tamamlanamadi:' -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host $_.ErrorDetails.Message
    } else {
        Write-Host $_.Exception.Message
    }
    exit 1
} finally {
    $requestHeaders.Clear()
    $plainKey = $null
    if ($null -ne $secureKey) { $secureKey.Dispose() }
}
