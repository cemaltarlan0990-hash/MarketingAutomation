param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 8080,
    [Parameter(Mandatory = $true)]
    [string]$ApiKey
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$webRoot = Join-Path $projectRoot "src\CrmRegistrationGateway"
$iisExpressPath = Join-Path $env:ProgramFiles "IIS Express\iisexpress.exe"

if (-not (Test-Path -LiteralPath $iisExpressPath)) {
    throw "IIS Express bulunamadı: $iisExpressPath"
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "Yerel entegrasyon anahtarı boş olamaz."
}

$env:CRM_INBOUND_API_KEY = $ApiKey

Write-Host "Servis başlatılıyor: http://localhost:$Port"
Write-Host "Durdurmak için IIS Express ekranında Q tuşuna basın."

& $iisExpressPath "/path:$webRoot" "/port:$Port"
