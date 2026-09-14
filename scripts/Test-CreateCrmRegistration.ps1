param(
    [string]$BaseUrl = "http://localhost:8080",
    [Parameter(Mandatory = $true)]
    [string]$ApiKey,
    [string]$EventId = "phase-1-test-event",
    [string]$Email = ("crm-gateway-test-{0}@example.invalid" -f [DateTimeOffset]::UtcNow.ToUnixTimeSeconds())
)

$headers = @{
    "X-Integration-Key" = $ApiKey
}

$body = @{
    firstname = "Test"
    lastname  = "Katilimci"
    email     = $Email
    phone     = "+90 555 000 00 00"
    company   = "Test Sirketi"
    eventId   = $EventId
}

$uri = $BaseUrl.TrimEnd('/') + "/CreateCrmRegistration.aspx"

Invoke-RestMethod `
    -Uri $uri `
    -Method Post `
    -ContentType "application/x-www-form-urlencoded" `
    -Headers $headers `
    -Body $body
