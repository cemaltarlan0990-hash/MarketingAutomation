#requires -Version 7.0
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,
    [int]$Port = 8098
)

$ErrorActionPreference = 'Stop'
$iisPath = Join-Path $env:ProgramFiles 'IIS Express\iisexpress.exe'
if (-not (Test-Path -LiteralPath $iisPath)) { throw 'IIS Express is required for the ASPX contract checks.' }
$PackageRoot = (Resolve-Path -LiteralPath $PackageRoot).Path
$logDirectory = Split-Path -Parent $PackageRoot
$apiKey = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$previousApiKey = $env:CRM_INBOUND_API_KEY
$previousWriteSetting = $env:CRM_WRITES_ENABLED
$iisProcess = $null
try {
    $env:CRM_INBOUND_API_KEY = $apiKey
    $env:CRM_WRITES_ENABLED = 'false'
    $iisProcess = Start-Process -FilePath $iisPath -ArgumentList @("/path:`"$PackageRoot`"", "/port:$Port", '/systray:false') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logDirectory 'iis.stdout.log') -RedirectStandardError (Join-Path $logDirectory 'iis.stderr.log')
    $baseUrl = "http://localhost:$Port"
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($iisProcess.HasExited) { throw 'IIS Express exited before the ASPX endpoint was ready.' }
        try {
            $response = Invoke-WebRequest "$baseUrl/CreateCrmRegistration.aspx" -SkipHttpErrorCheck -TimeoutSec 5
            if ($response.StatusCode -eq 405) { $ready = $true; break }
        } catch { }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw 'ASPX endpoint did not start in IIS Express.' }
    & (Join-Path $PSScriptRoot 'Test-EndpointContract.ps1') -BaseUrl $baseUrl -ApiKey $apiKey
} finally {
    if ($iisProcess -and -not $iisProcess.HasExited) { Stop-Process -Id $iisProcess.Id }
    $env:CRM_INBOUND_API_KEY = $previousApiKey
    $env:CRM_WRITES_ENABLED = $previousWriteSetting
}
