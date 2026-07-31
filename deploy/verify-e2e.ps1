# End-to-end verification of the tenancy, sign-in, and authorization foundation:
# two isolated dealer organizations, real sessions, and rooftop-scoped access
# enforced server-side — over both the organization structure and the stock on
# each lot.
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

# Development accounts seeded by DevelopmentSeeder.DevUsers.
$emailOrgWide  = "gm@dev.local"       # every rooftop
$emailScoped   = "advisor@dev.local"  # first rooftop only
$emailNoAccess = "nobody@dev.local"   # no assignment
$devPassword   = "Dev@Pass1!"

Write-Host "Starting Host (Development, seeding enabled)..." -ForegroundColor Cyan
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $baseUrl
$env:ConnectionStrings__HostCatalog = $hostConn
$env:Seed__Enabled = "true"
# Quiet the application log: this script is meant to be read by a person, and
# EF command logging buries the result. The host log still goes to a file.
$env:Serilog__MinimumLevel__Default = "Warning"
$hostLog = Join-Path $env:TEMP "opendealer360-verify-host.log"

$proc = Start-Process dotnet -PassThru -NoNewWindow `
    -RedirectStandardOutput $hostLog `
    -ArgumentList "run --project src/App -c Release --no-build"

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

    # Signs in and returns a web session carrying the cookie. PowerShell 5.1 will
    # not accept "Cookie" as a plain header, so use a cookie container.
    function New-DealerSession([string]$tenant, [string]$email) {
        $body = @{ email = $email; password = $devPassword } | ConvertTo-Json
        $login = Invoke-WebRequest "$baseUrl/api/v1/auth/login" -Method Post `
            -Body $body -ContentType "application/json" `
            -Headers @{ "X-Tenant" = $tenant } -UseBasicParsing

        $raw = $login.Headers['Set-Cookie']
        if ($raw -is [array]) { $raw = $raw | Where-Object { $_ -like 'odms_session=*' } | Select-Object -First 1 }
        $token = ($raw -split ';')[0] -replace '^odms_session=', ''

        $ws = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $ws.Cookies.Add((New-Object System.Net.Cookie("odms_session", $token, "/", "localhost")))
        return $ws
    }

    # POSTs JSON and returns the parsed response, for the steps that need the
    # created record's id rather than just its status code.
    function Invoke-Api([string]$path, $session, $body) {
        return Invoke-RestMethod "$baseUrl$path" -Method Post `
            -Body ($body | ConvertTo-Json -Depth 5) -ContentType "application/json" `
            -Headers @{ "X-Tenant" = "northgroup" } -WebSession $session
    }

    function Get-Org([string]$tenant, $session) {
        return Invoke-RestMethod "$baseUrl/api/v1/organization" `
            -Headers @{ "X-Tenant" = $tenant } -WebSession $session
    }

    # A POST to an endpoint that expects a body must carry one, or model binding
    # answers 415 before authorization is ever consulted — which would make a
    # permission check look like it passed when it never ran.
    function Get-Status([string]$path, [string]$tenant, $session, [string]$method = "Get", $body = $null) {
        try {
            $call = @{ Uri = "$baseUrl$path"; UseBasicParsing = $true; Method = $method }
            if ($tenant)  { $call.Headers = @{ "X-Tenant" = $tenant } }
            if ($session) { $call.WebSession = $session }
            if ($body) {
                $call.Body = ($body | ConvertTo-Json)
                $call.ContentType = "application/json"
            }
            return (Invoke-WebRequest @call).StatusCode
        } catch { return $_.Exception.Response.StatusCode.value__ }
    }

    Write-Host "`n--- signing in ---" -ForegroundColor Cyan
    $orgWide  = New-DealerSession "northgroup" $emailOrgWide
    $scoped   = New-DealerSession "northgroup" $emailScoped
    $noAccess = New-DealerSession "northgroup" $emailNoAccess
    $cityWide = New-DealerSession "citymotors" $emailOrgWide
    "signed in three northgroup users and one citymotors user"

    Write-Host "`n--- tenant isolation ---" -ForegroundColor Cyan
    $north = Get-Org "northgroup" $orgWide
    $northRooftops = @($north.legalEntities.rooftops).Count
    "{0}: {1} rooftop(s)  (expect 2)" -f $north.name, $northRooftops

    $city = Get-Org "citymotors" $cityWide
    $cityRooftops = @($city.legalEntities.rooftops).Count
    "{0}: {1} rooftop(s)  (expect 1)" -f $city.name, $cityRooftops

    Write-Host "`n--- rooftop authorization (R05) ---" -ForegroundColor Cyan
    $scopedOrg = Get-Org "northgroup" $scoped
    $scopedCodes = @($scopedOrg.legalEntities.rooftops.code)
    "scoped user sees: {0}  (expect NAG-01 only)" -f ($scopedCodes -join ", ")

    $siblingId = ($north.legalEntities.rooftops | Where-Object { $_.code -eq "NAG-02" }).id
    $siblingStatus = Get-Status "/api/v1/organization/rooftops/$siblingId" "northgroup" $scoped
    "scoped user -> sibling rooftop by id  -> HTTP $siblingStatus (expect 403)"

    $noAccessStatus = Get-Status "/api/v1/organization" "northgroup" $noAccess
    "unassigned user                       -> HTTP $noAccessStatus (expect 403)"

    Write-Host "`n--- inventory rooftop scope ---" -ForegroundColor Cyan
    # Counts are not asserted: the integration suite runs against the same
    # databases and adds units of its own. What must hold is that none of the
    # sibling rooftop's units ever reaches a scoped caller.
    $managerUnits = Invoke-RestMethod "$baseUrl/api/v1/inventory?limit=200" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $scopedUnits = Invoke-RestMethod "$baseUrl/api/v1/inventory?limit=200" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $scoped

    $siblingUnits = @($managerUnits | Where-Object { $_.rooftopId -eq $siblingId })
    $leaked = @($scopedUnits | Where-Object { $_.rooftopId -eq $siblingId }).Count
    "manager sees {0} unit(s) at NAG-02        (expect 1 or more)" -f $siblingUnits.Count
    "scoped user sees {0} of them              (expect 0)" -f $leaked

    $siblingUnitId = $siblingUnits[0].id
    $siblingUnitStatus = Get-Status "/api/v1/inventory/$siblingUnitId" "northgroup" $scoped
    "scoped user -> sibling unit by id     -> HTTP $siblingUnitStatus (expect 403)"

    $siblingFilterStatus = Get-Status "/api/v1/inventory?rooftopId=$siblingId" "northgroup" $scoped
    "scoped user -> list filtered to NAG-02 -> HTTP $siblingFilterStatus (expect 403)"

    # Vehicles are organization-shared, so the same user may read them all.
    $vehicles = Invoke-RestMethod "$baseUrl/api/v1/vehicles?limit=200" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $scoped
    $vehicleCount = @($vehicles).Count
    "scoped user sees {0} vehicle(s)           (expect 1 or more: vehicles are shared)" -f $vehicleCount

    Write-Host "`n--- lead rooftop scope ---" -ForegroundColor Cyan
    $managerLeads = Invoke-RestMethod "$baseUrl/api/v1/leads?limit=200" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $scopedLeads = Invoke-RestMethod "$baseUrl/api/v1/leads?limit=200" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $scoped

    $siblingLeads = @($managerLeads | Where-Object { $_.rooftopId -eq $siblingId })
    $leakedLeads = @($scopedLeads | Where-Object { $_.rooftopId -eq $siblingId }).Count
    "manager sees {0} enquiry(ies) at NAG-02   (expect 1 or more)" -f $siblingLeads.Count
    "scoped user sees {0} of them              (expect 0)" -f $leakedLeads

    $siblingLeadId = $siblingLeads[0].id
    $siblingLeadStatus = Get-Status "/api/v1/leads/$siblingLeadId" "northgroup" $scoped
    "scoped user -> sibling enquiry by id  -> HTTP $siblingLeadStatus (expect 403)"

    # Read is granted to the scoped user, manage is not.
    $workStatus = Get-Status "/api/v1/leads/$($scopedLeads[0].id)/status" "northgroup" $scoped "Post" @{ status = "Working" }
    "scoped user -> work own enquiry       -> HTTP $workStatus (expect 403: read-only role)"

    Write-Host "`n--- deals: approval is a separate right ---" -ForegroundColor Cyan
    # Built fresh rather than read from the seed, so the check is repeatable and
    # does not depend on what an earlier test run left behind.
    $sales = New-DealerSession "northgroup" "sales@dev.local"
    $firstRooftopId = ($north.legalEntities.rooftops | Where-Object { $_.code -eq "NAG-01" }).id
    $suffix = (New-Guid).ToString("N").Substring(0, 8).ToUpper()

    $customer = Invoke-Api "/api/v1/customers" $orgWide @{
        kind = "Person"; firstName = "Verify"; lastName = "Case$suffix"
    }
    $vehicle = Invoke-Api "/api/v1/vehicles" $orgWide @{
        vin = "VERIFY$suffix"; modelYear = 2021; make = "Toyota"; model = "RAV4"
        vinExceptionReason = "Synthetic VIN for the end-to-end check."
    }
    $unit = Invoke-Api "/api/v1/inventory" $orgWide @{
        vehicleId = $vehicle.id; rooftopId = $firstRooftopId; stockNumber = "V$suffix"
    }
    $null = Invoke-Api "/api/v1/inventory/$($unit.id)/status" $orgWide @{ status = "Available" }

    $deal = Invoke-Api "/api/v1/deals" $sales @{
        rooftopId = $firstRooftopId; customerId = $customer.id
        inventoryUnitId = $unit.id; currency = "USD"
    }
    $null = Invoke-Api "/api/v1/deals/$($deal.id)/terms" $sales @{
        charges = @(@{ kind = "VehiclePrice"; description = "The car"; amount = 24000 })
    }
    $submitStatus = Get-Status "/api/v1/deals/$($deal.id)/status" "northgroup" $sales "Post" @{ status = "Submitted" }
    "salesperson submits own deal          -> HTTP $submitStatus (expect 200)"

    $salesApprove = Get-Status "/api/v1/deals/$($deal.id)/status" "northgroup" $sales "Post" @{ status = "Approved" }
    "salesperson approves own deal         -> HTTP $salesApprove (expect 403)"

    $managerApprove = Get-Status "/api/v1/deals/$($deal.id)/status" "northgroup" $orgWide "Post" @{ status = "Approved" }
    "manager approves the same deal        -> HTTP $managerApprove (expect 200)"

    # The car is held by that deal, so a second deal on it must be refused.
    $doubleSell = Get-Status "/api/v1/deals" "northgroup" $orgWide "Post" @{
        rooftopId = $firstRooftopId; customerId = $customer.id
        inventoryUnitId = $unit.id; currency = "USD"
    }
    "second deal on the same car           -> HTTP $doubleSell (expect 409)"

    Write-Host "`n--- sessions ---" -ForegroundColor Cyan
    $noSession = Get-Status "/api/v1/organization" "northgroup" $null
    "no session                            -> HTTP $noSession (expect 401)"

    $crossTenant = Get-Status "/api/v1/organization" "citymotors" $scoped
    "northgroup session at citymotors      -> HTTP $crossTenant (expect 401)"

    # Revoke, then prove the very same session stops working at once.
    $null = Get-Status "/api/v1/auth/logout" "northgroup" $orgWide "Post"
    $afterLogout = Get-Status "/api/v1/organization" "northgroup" $orgWide
    "after sign-out, same session          -> HTTP $afterLogout (expect 401)"

    Write-Host "`n--- error contract ---" -ForegroundColor Cyan
    # No session here on purpose: a WebRequestSession remembers headers between
    # calls, which would silently re-send the previous tenant. The tenant is
    # resolved before the caller anyway, so none is needed to prove this.
    $missingTenant = Get-Status "/api/v1/organization" $null $null
    $unknownTenant = Get-Status "/api/v1/organization" "nope" $null
    "missing X-Tenant  -> HTTP $missingTenant (expect 400)"
    "unknown tenant    -> HTTP $unknownTenant (expect 404)"

    $ok = ($northRooftops -eq 2) -and ($cityRooftops -eq 1) -and ($north.name -ne $city.name) `
        -and ($scopedCodes.Count -eq 1) -and ($scopedCodes[0] -eq "NAG-01") `
        -and ($siblingStatus -eq 403) -and ($noAccessStatus -eq 403) `
        -and ($siblingUnits.Count -ge 1) -and ($leaked -eq 0) `
        -and ($siblingUnitStatus -eq 403) -and ($siblingFilterStatus -eq 403) `
        -and ($vehicleCount -ge 1) `
        -and ($siblingLeads.Count -ge 1) -and ($leakedLeads -eq 0) `
        -and ($siblingLeadStatus -eq 403) -and ($workStatus -eq 403) `
        -and ($submitStatus -eq 200) -and ($salesApprove -eq 403) -and ($managerApprove -eq 200) `
        -and ($doubleSell -eq 409) `
        -and ($noSession -eq 401) -and ($crossTenant -eq 401) -and ($afterLogout -eq 401) `
        -and ($missingTenant -eq 400) -and ($unknownTenant -eq 404)

    if ($ok) {
        Write-Host "`nPASS: tenants isolated, sessions enforced and revocable, rooftop scope holds, and a deal needs a manager." -ForegroundColor Green
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
