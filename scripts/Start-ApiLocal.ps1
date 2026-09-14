param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 7071
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $projectRoot "src\CrmRegistrationGateway.Functions\CrmRegistrationGateway.Api.csproj"
$developmentSettings = Join-Path $projectRoot "src\CrmRegistrationGateway.Functions\appsettings.Development.json"

if (-not (Test-Path -LiteralPath $developmentSettings)) {
    throw "appsettings.Development.json bulunamadı. Example dosyasını kopyalayıp yerel değerleri doldurun."
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project $apiProject --urls "http://localhost:$Port"
