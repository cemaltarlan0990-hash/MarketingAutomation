param(
    [Parameter(Mandatory = $true)]
    [string]$JsonPath,
    [string]$EventName = '',
    [switch]$Send,
    [ValidateRange(0, 100000)]
    [int]$Limit = 0
)

$ErrorActionPreference = 'Stop'
$serviceUrl = 'https://alttr-marketingautomation-f9dmgackdme4gueu.westeurope-01.azurewebsites.net'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $projectRoot 'artifacts\crm-import'
$null = New-Item -ItemType Directory -Path $outputDir -Force
$outputPrefix = Join-Path $outputDir ('import-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$previewPath = $outputPrefix + '-preview.csv'
$reportPath = $outputPrefix + '-results.csv'
$mappedPath = $outputPrefix + '-payloads.json'

# Read the source as UTF-8 without modifying it. Preview is the default mode.
$sourceText = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $JsonPath).Path, [System.Text.Encoding]::UTF8)
if (-not $sourceText.TrimStart().StartsWith('[')) {
    throw 'JSON dosyasinin en ust seviyesinde bir kayit listesi (dizi) olmali.'
}
# Windows PowerShell 5.1 emits a JSON array as one pipeline object.
# Assign first so the array expression below contains its individual records.
$parsedRows = ConvertFrom-Json -InputObject $sourceText
$rows = @($parsedRows)
if ($rows.Count -eq 0) { throw 'JSON dosyasinda kayit bulunamadi.' }
$seenEmails = @{}
$preview = @()
$ready = @()
$limits = @{ firstName = 50; lastName = 50; email = 254; mobilePhone = 50; company = 160; jobTitle = 100; city = 100; eventName = 200 }
$rowIndex = 0

foreach ($row in $rows) {
    $rowIndex++
    $name = ([string]$row.nameSurname).Trim()
    $parts = @($name -split '\s+' | Where-Object { $_ })
    $firstName = ''
    $lastName = ''
    if ($parts.Count -ge 2) {
        $firstName = $parts[0..($parts.Count - 2)] -join ' '
        $lastName = $parts[-1]
    }
    $payload = [ordered]@{
        firstName = $firstName
        lastName = $lastName
        email = ([string]$row.email).Trim()
    }
    $optional = [ordered]@{
        mobilePhone = ([string]$row.phone).Trim()
        company = ([string]$row.company).Trim()
        jobTitle = ([string]$row.title).Trim()
        city = ([string]$row.city).Trim()
        eventName = $EventName.Trim()
    }
    foreach ($field in $optional.Keys) {
        if ($optional[$field]) { $payload[$field] = $optional[$field] }
    }
    $issues = @()
    if (-not $firstName -or -not $lastName) { $issues += 'Ad ve soyad ayristirilamadi.' }
    try {
        $parsedEmail = [System.Net.Mail.MailAddress]::new($payload.email)
        if ($parsedEmail.Address -ne $payload.email) { throw 'Invalid email' }
    } catch { $issues += 'E-posta gecersiz veya bos.' }
    foreach ($field in $payload.Keys) {
        if ($payload[$field].Length -gt $limits[$field]) {
            $issues += "$field en fazla $($limits[$field]) karakter olmali."
        }
    }
    $state = 'ready'
    $detail = 'Soyad, nameSurname alaninin son kelimesi olarak ayrildi.'
    $emailKey = $payload.email.ToLowerInvariant()
    if ($issues.Count -gt 0) {
        $state = 'invalid'
        $detail = $issues -join ' '
    } elseif ($seenEmails.ContainsKey($emailKey)) {
        $state = 'duplicate_in_file'
        $detail = "Ayni e-posta icin ilk gecerli satir kullanilacak: $($seenEmails[$emailKey])."
    } else {
        $seenEmails[$emailKey] = $rowIndex
        $ready += [pscustomobject]@{ row = $rowIndex; payload = $payload }
    }
    $preview += [pscustomobject][ordered]@{
        row = $rowIndex
        sourceEventId = [string]$row.eventId
        sourceName = $name
        firstName = $firstName
        lastName = $lastName
        email = $payload.email
        mobilePhone = $optional.mobilePhone
        company = $optional.company
        jobTitle = $optional.jobTitle
        city = $optional.city
        eventName = $optional.eventName
        status = $state
        detail = $detail
    }
}

$preview | Export-Csv -LiteralPath $previewPath -NoTypeInformation -Encoding UTF8
$payloads = @($ready | ForEach-Object { $_.payload })
ConvertTo-Json -InputObject $payloads -Depth 5 | Set-Content -LiteralPath $mappedPath -Encoding UTF8
$invalidCount = @($preview | Where-Object { $_.status -eq 'invalid' }).Count
$duplicateCount = @($preview | Where-Object { $_.status -eq 'duplicate_in_file' }).Count
Write-Host "Kaynak: $($rows.Count); hazir: $($ready.Count); dosyada tekrar: $duplicateCount; hatali: $invalidCount"
Write-Host "Onizleme: $previewPath"
Write-Host "Servise gonderilecek bilgiler: $mappedPath"
Write-Host 'Izinler (kvkk/marketing), note, dateRaw, registrationDate ve eventId CRM alanlarina aktarilmaz; kaynak dosya korunur.'
if (-not $EventName.Trim()) { Write-Host 'Etkinlik adi verilmedi; CRM baslik alanina etkinlik adi gonderilmeyecek.' }
if (-not $Send) {
    Write-Host 'Yalnizca onizleme olusturuldu. CRM kaydi gonderilmedi.'
    return
}
if ($invalidCount -gt 0) { throw 'Hatali satirlari kaynak dosyanin bir kopyasinda duzeltip tekrar onizleme alin.' }
if ($ready.Count -eq 0) { throw 'Gonderilecek kayit yok.' }

$selected = @($ready)
if ($Limit -gt 0) { $selected = @($ready | Select-Object -First $Limit) }
$headers = @{}
$secureKey = $null
$plainKey = $null
$results = @()
try {
    $health = Invoke-RestMethod -Uri "$serviceUrl/health" -TimeoutSec 60
    if ($health.status -ne 'healthy') { throw 'Azure servisi hazir degil.' }
    $secureKey = Read-Host 'Azure INBOUND_API_KEY degerini girin (ekranda gorunmez)' -AsSecureString
    $plainKey = [System.Net.NetworkCredential]::new('', $secureKey).Password
    if ($plainKey.Length -lt 32) { throw 'Giris anahtari en az 32 karakter olmali.' }
    $headers['X-Integration-Key'] = $plainKey
    Write-Host "$($selected.Count) kisi CRM servisine sirayla gonderiliyor..."
    foreach ($item in $selected) {
        $outcome = [pscustomobject][ordered]@{
            row = $item.row
            email = $item.payload.email
            status = ''
            httpStatus = ''
            apiStatus = ''
            authenticationErrorCode = ''
            identityCode = ''
            tokenHttpStatus = ''
            recommendedAction = ''
            crmId = ''
            message = ''
            correlationId = ''
        }
        $stopBatch = $false
        try {
            $body = [System.Text.Encoding]::UTF8.GetBytes(($item.payload | ConvertTo-Json -Depth 5))
            $response = Invoke-RestMethod -Method Post -Uri "$serviceUrl/api/crm/leads" `
                -Headers $headers -ContentType 'application/json; charset=utf-8' -Body $body -TimeoutSec 90
            if (-not $response.success -or $response.status -notin @('created', 'already_exists')) {
                throw 'Servis beklenen basari yanitini vermedi.'
            }
            $outcome.status = $response.status
            $outcome.crmId = $response.crmId
            $outcome.message = $response.message
            $outcome.correlationId = $response.correlationId
        } catch {
            $outcome.status = 'failed_or_unconfirmed'
            $outcome.message = $_.Exception.Message
            $failure = $_
            $errorBody = [string]$failure.ErrorDetails.Message
            $errorResponse = $failure.Exception.Response
            if ($null -ne $errorResponse) {
                $outcome.httpStatus = [int]$errorResponse.StatusCode
                # PowerShell 5.1 may omit ErrorDetails even when the API returns JSON.
                if (-not $errorBody -and $errorResponse -is [System.Net.HttpWebResponse]) {
                    try {
                        $errorStream = $errorResponse.GetResponseStream()
                        if ($null -ne $errorStream) {
                            $errorReader = [System.IO.StreamReader]::new($errorStream)
                            try { $errorBody = $errorReader.ReadToEnd() } finally { $errorReader.Dispose() }
                        }
                    } catch { }
                }
            }
            if ($errorBody) {
                $outcome.message = $errorBody
                try {
                    $apiError = ConvertFrom-Json -InputObject $errorBody
                    if ($apiError.message) { $outcome.message = [string]$apiError.message }
                    if ($apiError.status) { $outcome.apiStatus = [string]$apiError.status }
                    if ($apiError.correlationId) { $outcome.correlationId = [string]$apiError.correlationId }
                    if ($apiError.authentication) {
                        $outcome.authenticationErrorCode = [string]$apiError.authentication.errorCode
                        $outcome.identityCode = [string]$apiError.authentication.identityCode
                        $outcome.tokenHttpStatus = [string]$apiError.authentication.tokenHttpStatus
                        $outcome.recommendedAction = [string]$apiError.authentication.recommendedAction
                    }
                } catch { }
            }
            # A timeout may happen after CRM creates the record. Do not retry automatically.
            if ($outcome.apiStatus -eq 'authentication_error' -and $outcome.message -eq 'CRM authentication failed.') {
                $outcome.status = 'failed'
            }
            $stopBatch = $true
        }
        $results += $outcome
        # Save each completed outcome so progress survives an interrupted import.
        $results | Export-Csv -LiteralPath $reportPath -NoTypeInformation -Encoding UTF8
        Write-Host "Satir $($outcome.row): $($outcome.status)"
        if ($stopBatch) {
            Write-Host "HTTP: $($outcome.httpStatus); API: $($outcome.apiStatus)"
            Write-Host "Hata: $($outcome.message)"
            if ($outcome.authenticationErrorCode) { Write-Host "Kimlik dogrulama hata kodu: $($outcome.authenticationErrorCode)" }
            if ($outcome.identityCode) { Write-Host "Microsoft hata kodu: $($outcome.identityCode)" }
            if ($outcome.tokenHttpStatus) { Write-Host "Token istegi HTTP: $($outcome.tokenHttpStatus)" }
            if ($outcome.recommendedAction) { Write-Host "Yapilacak kontrol: $($outcome.recommendedAction)" }
            if ($outcome.apiStatus -eq 'authentication_error' -and $outcome.message -eq 'CRM authentication failed.') {
                Write-Host 'CRM token alinamadi; bu istekte CRM kaydi olusturulmadi.'
            }
            if ($outcome.correlationId) { Write-Host "Takip kimligi: $($outcome.correlationId)" }
            if ($outcome.status -eq 'failed') {
                Write-Host 'Aktarim CRM kimlik dogrulama hatasi nedeniyle durdu.'
            } else {
                Write-Host 'Aktarim hata nedeniyle durdu. Sonuc raporunu inceleyin; son istegin CRM sonucu belirsiz olabilir.'
            }
            break
        }
    }
    $createdCount = @($results | Where-Object { $_.status -eq 'created' }).Count
    $existingCount = @($results | Where-Object { $_.status -eq 'already_exists' }).Count
    $failedCount = @($results | Where-Object { $_.status -in @('failed', 'failed_or_unconfirmed') }).Count
    Write-Host "Olusturulan: $createdCount; CRM'de mevcut: $existingCount; hata: $failedCount; gonderilmeyen: $($selected.Count - $results.Count)"
    Write-Host "Sonuc raporu: $reportPath"
    if ($failedCount -gt 0) {
        Write-Host 'Toplu aktarim tamamlanamadi. Yukaridaki hata ayrintisini inceleyin; rapor kaydedildi.' -ForegroundColor Red
        exit 1
    }
} finally {
    $headers.Clear()
    $plainKey = $null
    if ($null -ne $secureKey) { $secureKey.Dispose() }
}
