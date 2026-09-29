#requires -Version 7.0
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedCommit
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
# Deployment may recycle IIS. Retry only read-only/unauthenticated checks;
# these requests cannot create a CRM record and need no CRM secrets.
for ($attempt = 1; $attempt -le 18; $attempt++) {
    try {
        $version = Invoke-WebRequest "$BaseUrl/deployment-version.txt" -TimeoutSec 15
        if ($version.Content.Trim() -ne $ExpectedCommit) { throw 'The deployed ASPX version does not match this commit.' }
        $endpoint = "$BaseUrl/CreateCrmRegistration.aspx"
        $checks = @(
            @{ Method = 'Get'; Expected = 405 },
            @{ Method = 'Post'; Expected = 415; ContentType = 'application/json'; Body = '{}' },
            @{ Method = 'Post'; Expected = 401; ContentType = 'application/x-www-form-urlencoded'; Body = 'firstname=Test' }
        )
        foreach ($check in $checks) {
            $request = @{ Uri = $endpoint; Method = $check.Method; SkipHttpErrorCheck = $true; TimeoutSec = 15 }
            if ($check.ContainsKey('ContentType')) { $request.ContentType = $check.ContentType; $request.Body = $check.Body }
            $response = Invoke-WebRequest @request
            if ($response.StatusCode -ne $check.Expected) { throw "ASPX HTTP check expected $($check.Expected), received $($response.StatusCode)." }
            $body = $response.Content | ConvertFrom-Json
            if ($body.success -ne $false -or -not $body.correlationId) { throw 'The ASPX JSON response was not valid.' }
        }
        $eventEndpoint = "$BaseUrl/EtkinlikKayit.aspx"
        $eventChecks = @(
            @{ Method = 'Get'; Expected = 405 },
            @{ Method = 'Post'; Expected = 401; ContentType = 'application/json'; Body = '{"adSoyad":"Test"}' }
        )
        foreach ($check in $eventChecks) {
            $request = @{ Uri = $eventEndpoint; Method = $check.Method; SkipHttpErrorCheck = $true; TimeoutSec = 15 }
            if ($check.ContainsKey('ContentType')) { $request.ContentType = $check.ContentType; $request.Body = $check.Body }
            $response = Invoke-WebRequest @request
            if ($response.StatusCode -ne $check.Expected) { throw "EtkinlikKayit HTTP check expected $($check.Expected), received $($response.StatusCode)." }
            $body = $response.Content | ConvertFrom-Json
            if ($body.success -ne $false -or -not $body.correlationId) { throw 'The EtkinlikKayit JSON response was not valid.' }
        }
        $connectionTest = Invoke-WebRequest "$BaseUrl/TestCrmConnection.aspx" -Method Post -SkipHttpErrorCheck -TimeoutSec 15
        if ($connectionTest.StatusCode -ne 401 -or ($connectionTest.Content | ConvertFrom-Json).success -ne $false) {
            throw 'The deployed connection-test endpoint must require an API key.'
        }
        Write-Host "ASPX deployment checks passed for $ExpectedCommit."
        exit 0
    } catch {
        if ($attempt -eq 18) { throw }
        Write-Host "Waiting for deployed application ($attempt/18): $($_.Exception.Message)"
        Start-Sleep -Seconds 10
    }
}
