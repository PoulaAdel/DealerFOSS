# End-to-end verification of the tenancy and authorization foundation:
# two isolated dealer organizations, one multi-rooftop, and rooftop-scoped
# access enforced server-side.
#
# The same assertions run in CI via tests/Integration. This script exists for a
# manual check against a real running Host.
#
# Prerequisites: a reachable SQL engine. On Windows, LocalDB is the verified
# option (see CLAUDE.md):
#   sqllocaldb start MSSQLLocalDB
#
# Usage (from repo root, Windows PowerShell 5.1):
#   & .\deploy\verify-e2e.ps1
#   & .\deploy\verify-e2e.ps1 -HostConnection "Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"

param(
    [string]$HostConnection = "Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
)

$ErrorActionPreference = "Stop"
$port = 5080
$baseUrl = "http://localhost:$port"
$hostConn = $HostConnection

# Well-known development users seeded by DevelopmentSeeder.DevUsers.
$userOrgWide  = "11111111-1111-1111-1111-111111111111"   # every rooftop
$userScoped   = "22222222-2222-2222-2222-222222222222"   # first rooftop only
$userNoAccess = "33333333-3333-3333-3333-333333333333"   # no assignment

Write-Host "Starting Host (Development, seeding enabled)..." -ForegroundColor Cyan
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $baseUrl
$env:ConnectionStrings__HostCatalog = $hostConn
$env:Seed__Enabled = "true"

$proc = Start-Process dotnet -PassThru -NoNewWindow `
    -ArgumentList "run --project src/Host -c Release --no-build"

try {
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        try {
            $r = Invoke-WebRequest "$baseUrl/health/ready" -UseBasicParsing -TimeoutSec 3
            if ($r.StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Seconds 2 }
    }
    if (-not $ready) { throw "Host did not become ready." }
    Write-Host "Host ready." -ForegroundColor Green

    function Get-Org([string]$tenant, [string]$user) {
        return Invoke-RestMethod "$baseUrl/api/v1/organization" `
            -Headers @{ "X-Tenant" = $tenant; "X-User" = $user }
    }

    function Get-Status([string]$path, [hashtable]$headers) {
        try { return (Invoke-WebRequest "$baseUrl$path" -Headers $headers -UseBasicParsing).StatusCode }
        catch { return $_.Exception.Response.StatusCode.value__ }
    }

    Write-Host "`n--- tenant isolation ---" -ForegroundColor Cyan
    $north = Get-Org "northgroup" $userOrgWide
    $northRooftops = @($north.legalEntities.rooftops).Count
    "{0}: {1} rooftop(s)  (expect 2)" -f $north.name, $northRooftops

    $city = Get-Org "citymotors" $userOrgWide
    $cityRooftops = @($city.legalEntities.rooftops).Count
    "{0}: {1} rooftop(s)  (expect 1)" -f $city.name, $cityRooftops

    Write-Host "`n--- rooftop authorization (R05) ---" -ForegroundColor Cyan
    $scoped = Get-Org "northgroup" $userScoped
    $scopedCodes = @($scoped.legalEntities.rooftops.code)
    "scoped user sees: {0}  (expect NAG-01 only)" -f ($scopedCodes -join ", ")

    $siblingId = ($north.legalEntities.rooftops | Where-Object { $_.code -eq "NAG-02" }).id
    $siblingStatus = Get-Status "/api/v1/organization/rooftops/$siblingId" `
        @{ "X-Tenant" = "northgroup"; "X-User" = $userScoped }
    "scoped user -> sibling rooftop by id  -> HTTP $siblingStatus (expect 403)"

    $noAccessStatus = Get-Status "/api/v1/organization" `
        @{ "X-Tenant" = "northgroup"; "X-User" = $userNoAccess }
    "unassigned user                       -> HTTP $noAccessStatus (expect 403)"

    Write-Host "`n--- error contract ---" -ForegroundColor Cyan
    $missingTenant = Get-Status "/api/v1/organization" @{ "X-User" = $userOrgWide }
    $unknownTenant = Get-Status "/api/v1/organization" @{ "X-Tenant" = "nope"; "X-User" = $userOrgWide }
    $missingUser   = Get-Status "/api/v1/organization" @{ "X-Tenant" = "northgroup" }
    "missing X-Tenant  -> HTTP $missingTenant (expect 400)"
    "unknown tenant    -> HTTP $unknownTenant (expect 404)"
    "missing X-User    -> HTTP $missingUser (expect 401)"

    $ok = ($northRooftops -eq 2) -and ($cityRooftops -eq 1) -and ($north.name -ne $city.name) `
        -and ($scopedCodes.Count -eq 1) -and ($scopedCodes[0] -eq "NAG-01") `
        -and ($siblingStatus -eq 403) -and ($noAccessStatus -eq 403) `
        -and ($missingTenant -eq 400) -and ($unknownTenant -eq 404) -and ($missingUser -eq 401)

    if ($ok) {
        Write-Host "`nPASS: tenants isolated, rooftop scope enforced, error contract holds." -ForegroundColor Green
    } else {
        Write-Host "`nFAIL: expectations not met." -ForegroundColor Red
        exit 1
    }
}
finally {
    Write-Host "Stopping Host..." -ForegroundColor DarkGray
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like "*OpenDealer360.Host*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}
