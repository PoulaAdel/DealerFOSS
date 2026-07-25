# End-to-end verification for the Phase 1 foundation (doc 07 §2 exit criteria):
# two isolated dealer organizations, one multi-rooftop, resolved by tenant.
#
# Prerequisites: the SQL dev container is healthy
#   docker compose -f deploy/docker-compose.yml up -d sql
#
# Usage (from repo root):
#   pwsh ./deploy/verify-e2e.ps1
#   pwsh ./deploy/verify-e2e.ps1 -HostConnection "Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"

param(
    [string]$HostConnection = "Server=localhost,1433;Database=OpenDealer360_Host;User Id=sa;Password=OpenDealer360_dev!;TrustServerCertificate=True;Encrypt=True"
)

$ErrorActionPreference = "Stop"
$port = 5080
$baseUrl = "http://localhost:$port"
$hostConn = $HostConnection

Write-Host "Starting Host (Development, seeding enabled)..." -ForegroundColor Cyan
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $baseUrl
$env:ConnectionStrings__HostCatalog = $hostConn
$env:Seed__Enabled = "true"

$proc = Start-Process dotnet -PassThru -NoNewWindow `
    -ArgumentList "run --project src/Host -c Release --no-build"

try {
    # Wait for readiness (seeding runs at startup, so allow generous time).
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        try {
            $r = Invoke-WebRequest "$baseUrl/health/ready" -UseBasicParsing -TimeoutSec 3
            if ($r.StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Seconds 2 }
    }
    if (-not $ready) { throw "Host did not become ready." }
    Write-Host "Host ready." -ForegroundColor Green

    function Get-Org([string]$tenant) {
        return Invoke-RestMethod "$baseUrl/api/v1/organization" -Headers @{ "X-Tenant" = $tenant }
    }

    Write-Host "`n--- northgroup (expect 2 rooftops) ---" -ForegroundColor Cyan
    $north = Get-Org "northgroup"
    $northRooftops = @($north.legalEntities.rooftops).Count
    "{0}: {1} rooftop(s)" -f $north.name, $northRooftops

    Write-Host "`n--- citymotors (expect 1 rooftop) ---" -ForegroundColor Cyan
    $city = Get-Org "citymotors"
    $cityRooftops = @($city.legalEntities.rooftops).Count
    "{0}: {1} rooftop(s)" -f $city.name, $cityRooftops

    Write-Host "`n--- isolation & error contract ---" -ForegroundColor Cyan
    $missingHeader = try { (Invoke-WebRequest "$baseUrl/api/v1/organization" -UseBasicParsing).StatusCode }
                     catch { $_.Exception.Response.StatusCode.value__ }
    $unknownTenant = try { (Invoke-WebRequest "$baseUrl/api/v1/organization" -Headers @{ "X-Tenant" = "nope" } -UseBasicParsing).StatusCode }
                     catch { $_.Exception.Response.StatusCode.value__ }
    "missing X-Tenant  -> HTTP $missingHeader (expect 400)"
    "unknown tenant    -> HTTP $unknownTenant (expect 404)"

    $ok = ($northRooftops -eq 2) -and ($cityRooftops -eq 1) -and ($north.name -ne $city.name) `
        -and ($missingHeader -eq 400) -and ($unknownTenant -eq 404)
    if ($ok) { Write-Host "`nPASS: two isolated organizations, multi-rooftop resolved, error contract holds." -ForegroundColor Green }
    else { Write-Host "`nFAIL: expectations not met." -ForegroundColor Red; exit 1 }
}
finally {
    Write-Host "Stopping Host..." -ForegroundColor DarkGray
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like "*OpenDealer360.Host*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}
