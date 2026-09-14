param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $projectRoot "src\CrmRegistrationGateway.Functions\CrmRegistrationGateway.Api.csproj"
$testProject = Join-Path $projectRoot "tests\CrmRegistrationGateway.Functions.Tests\CrmRegistrationGateway.Functions.Tests.csproj"

dotnet build $apiProject --configuration $Configuration

if ($LASTEXITCODE -ne 0) {
    throw "Web API derlemesi başarısız oldu. Çıkış kodu: $LASTEXITCODE"
}

dotnet test $testProject --configuration $Configuration

if ($LASTEXITCODE -ne 0) {
    throw "Testler başarısız oldu. Çıkış kodu: $LASTEXITCODE"
}

Write-Host "Web API derlemesi ve testler başarılı: $Configuration"
