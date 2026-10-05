#requires -Version 7.0
param([Parameter(Mandatory = $true)][string]$PackageRoot)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $PackageRoot).Path) 'model-tests'
$null = New-Item -ItemType Directory -Path $testRoot -Force
$testExe = Join-Path $testRoot 'WebLead.Tests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sdk = Join-Path $PackageRoot 'bin\Microsoft.Xrm.Sdk.dll'
Copy-Item -LiteralPath $sdk -Destination $testRoot
$sources = @(
    'tests/WebLead.Tests.cs',
    'src/CrmRegistrationGateway/Configuration/CrmOptions.cs',
    'src/CrmRegistrationGateway/Configuration/WebLeadOptions.cs',
    'src/CrmRegistrationGateway/Infrastructure/RequestValidationException.cs',
    'src/CrmRegistrationGateway/Infrastructure/WebLeadRateLimiter.cs',
    'src/CrmRegistrationGateway/Models/CrmRegistrationRequest.cs',
    'src/CrmRegistrationGateway/Models/WebLeadRequest.cs',
    'src/CrmRegistrationGateway/Services/CrmRegistrationWriter.cs',
    'src/CrmRegistrationGateway/Services/IysConsentWriter.cs',
    'src/CrmRegistrationGateway/Services/TurnstileVerifier.cs'
) | ForEach-Object { Join-Path $projectRoot $_ }
& $compiler /nologo /target:exe "/out:$testExe" "/reference:$sdk" /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll /reference:System.Runtime.Serialization.dll /reference:System.ServiceModel.dll @sources
if ($LASTEXITCODE -ne 0) { throw 'WebLead model tests failed to compile.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'WebLead model tests failed.' }
