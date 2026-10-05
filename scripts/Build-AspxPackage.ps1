#requires -Version 7.0
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory,
    [switch]$EnableCrmWrites
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$webRoot = Join-Path $projectRoot 'src\CrmRegistrationGateway'
$projectPath = Join-Path $webRoot 'CrmRegistrationGateway.csproj'
$vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswherePath)) {
    throw 'Visual Studio MSBuild is required to build the .NET Framework ASPX application.'
}
$msbuildPath = & $vswherePath -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuildPath) { throw 'MSBuild could not be located.' }

# Normalize environment names: Windows Framework tools can fail when a parent
# process supplies both Path and PATH. This affects only the child build process.
$buildStart = [System.Diagnostics.ProcessStartInfo]::new()
$buildStart.FileName = $msbuildPath
$buildStart.WorkingDirectory = $projectRoot
$buildStart.UseShellExecute = $false
$buildStart.Environment.Clear()
$buildEnvironment = [Environment]::GetEnvironmentVariables('Process')
foreach ($environmentName in $buildEnvironment.Keys) {
    $buildStart.Environment[$environmentName.ToString().ToUpperInvariant()] = $buildEnvironment[$environmentName]
}
foreach ($argument in @($projectPath, '/restore', '/t:Build', "/p:Configuration=$Configuration", '/p:UseSharedCompilation=false', '/verbosity:minimal', '/nologo')) {
    $buildStart.ArgumentList.Add($argument)
}
$buildProcess = [System.Diagnostics.Process]::Start($buildStart)
$buildProcess.WaitForExit()
if ($buildProcess.ExitCode -ne 0) { throw "ASPX build failed: $($buildProcess.ExitCode)" }

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $projectRoot ('.artifacts\aspx-deploy\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
}
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Package output directory must be new to avoid stale deployment files.' }
$outputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$packageRoot = Join-Path $outputDirectory 'wwwroot'
$packageBin = Join-Path $packageRoot 'bin'
$null = New-Item -ItemType Directory -Path $packageBin -Force

# Package compiled runtime files only. Do not copy source, local settings,
# generated DLL config files, publish credentials, or participant data.
foreach ($page in @('CreateCrmRegistration.aspx', 'TestCrmConnection.aspx', 'EtkinlikKayit.aspx', 'WebLead.aspx')) {
    Copy-Item -LiteralPath (Join-Path $webRoot $page) -Destination $packageRoot
}
$integrationRoot = Join-Path $packageRoot 'integrations'
$null = New-Item -ItemType Directory -Path $integrationRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'examples\web-form\altium-event-form.js') -Destination $integrationRoot
Get-ChildItem -LiteralPath (Join-Path $webRoot 'bin') -File -Filter '*.dll' |
    Copy-Item -Destination $packageBin

[xml]$webConfig = Get-Content -LiteralPath (Join-Path $webRoot 'Web.config') -Raw
$webConfig.configuration.appSettings.RemoveAttribute('file')
$deploymentDefaults = @{
    'Crm.EnvironmentName' = 'Test'
    'Crm.AllowedHost' = 'altiumtr-test.crm4.dynamics.com'
    'Crm.WritesEnabled' = $EnableCrmWrites.IsPresent.ToString().ToLowerInvariant()
    'Crm.TargetEntityLogicalName' = 'lead'
    'Crm.FirstNameAttribute' = 'firstname'
    'Crm.LastNameAttribute' = 'lastname'
    'Crm.EmailAttribute' = 'emailaddress1'
    'Crm.PhoneAttribute' = 'mobilephone'
    'Crm.CompanyAttribute' = 'companyname'
    'Crm.JobTitleAttribute' = 'twbs_isunvani'
    'Crm.CityLookupAttribute' = 'twbs_sehir'
    'Crm.CityEntityLogicalName' = 'twbs_sehir'
    'Crm.CityNameAttribute' = 'twbs_sehiradi'
    'Crm.KvkkConsentAttribute' = 'twbs_kvkkonayi'
    'WebLead.AllowedOrigins' = 'https://altium.net'
}
foreach ($setting in $webConfig.configuration.appSettings.add) {
    $setting.value = if ($deploymentDefaults.ContainsKey($setting.key)) {
        $deploymentDefaults[$setting.key]
    } else {
        ''
    }
}
$webConfig.configuration.'system.web'.compilation.debug = 'false'
$webConfig.Save((Join-Path $packageRoot 'Web.config'))

$applicationAssembly = Join-Path $packageBin 'RelatedEntegrasyonu.CrmGateway.dll'
if (-not (Test-Path -LiteralPath $applicationAssembly)) { throw 'Compiled ASPX assembly is missing.' }
foreach ($setting in $webConfig.configuration.appSettings.add) {
    if (-not $deploymentDefaults.ContainsKey($setting.key) -and $setting.value) {
        throw "Unexpected configuration value in deployment package: $($setting.key)"
    }
}

$archivePath = Join-Path $outputDirectory 'CrmRegistrationGateway.Aspx.zip'
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $archivePath
$manifest = [ordered]@{
    builtAtUtc = [DateTime]::UtcNow.ToString('o')
    framework = '.NET Framework 4.8'
    configuration = $Configuration
    requiredHost = 'Azure App Service (Windows)'
    archivePath = $archivePath
    packageRoot = $packageRoot
    sha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    fileCount = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File).Count
    containsCredentials = $false
    deployed = $false
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDirectory 'manifest.json') -Encoding utf8
$manifest | ConvertTo-Json
