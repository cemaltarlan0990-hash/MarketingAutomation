param(
    [string]$BaseUrl = "http://localhost:8080",
    [Parameter(Mandatory = $true)]
    [string]$ApiKey
)

$uri = $BaseUrl.TrimEnd('/') + "/TestCrmConnection.aspx"

Invoke-RestMethod `
    -Uri $uri `
    -Method Post `
    -Headers @{ "X-Integration-Key" = $ApiKey }
