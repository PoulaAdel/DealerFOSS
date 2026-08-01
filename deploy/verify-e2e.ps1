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
#
# Pass -Port when something is already on 5080 — typically a development host
# left running for the frontend. Without it the script's own host cannot bind,
# and it silently measures whatever is already there instead.

param(
    [string]$HostConnection = "Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False",
    [int]$Port = 5080
)

$ErrorActionPreference = "Stop"
$port = $Port
$baseUrl = "http://localhost:$port"
$hostConn = $HostConnection

# Development accounts seeded by DevelopmentSeeder.DevUsers.
$emailOrgWide  = "gm@dev.local"       # every rooftop
$emailScoped   = "advisor@dev.local"  # first rooftop only
$emailNoAccess = "nobody@dev.local"   # no assignment
$devPassword   = "Dev@Pass1!"

# Refuse to run against somebody else's process. Without this the script's own
# host fails to bind, the readiness probe succeeds against whatever was already
# listening, and the whole run silently verifies a stale build.
if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
    throw "Port $port is already in use. Stop what is listening, or pass -Port with a free one."
}

Write-Host "Starting Host (Development, seeding enabled)..." -ForegroundColor Cyan
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $baseUrl
$env:ConnectionStrings__HostCatalog = $hostConn
$env:Seed__Enabled = "true"
# Quiet the application log: this script is meant to be read by a person, and
# EF command logging buries the result. The host log still goes to a file.
$env:Serilog__MinimumLevel__Default = "Warning"
$hostLog = Join-Path $env:TEMP "opendealer360-verify-host.log"

# --urls is passed on the command line rather than left to ASPNETCORE_URLS,
# because launchSettings.json pins 5080 and its applicationUrl would otherwise
# win over the environment variable.
$proc = Start-Process dotnet -PassThru -NoNewWindow `
    -RedirectStandardOutput $hostLog `
    -ArgumentList "run --project src/App -c Release --no-build -- --urls $baseUrl"

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

    # Signs in and returns a web session carrying the cookies. PowerShell 5.1 will
    # not accept "Cookie" as a plain header, so use a cookie container. The
    # anti-forgery token is hung off the session object as well, because every
    # write has to present it in a header — a cookie alone is exactly what this
    # protection refuses.
    function New-DealerSession([string]$tenant, [string]$email) {
        $body = @{ email = $email; password = $devPassword } | ConvertTo-Json
        $login = Invoke-WebRequest "$baseUrl/api/v1/auth/login" -Method Post `
            -Body $body -ContentType "application/json" `
            -Headers @{ "X-Tenant" = $tenant } -UseBasicParsing

        # PowerShell 5.1 sometimes hands back one joined string for repeated
        # headers and sometimes an array, so match on the text either way rather
        # than trusting the shape.
        $cookies = $login.Headers['Set-Cookie']
        if ($cookies -is [array]) { $cookies = $cookies -join ', ' }
        function Read-Cookie([string]$name) {
            $match = [regex]::Match($cookies, "(?:^|[,;]\s*)$name=([^;,]*)")
            if ($match.Success) { return $match.Groups[1].Value }
            throw "The $name cookie was not set at sign-in."
        }

        $token = Read-Cookie "odms_session"
        $csrf  = Read-Cookie "odms_csrf"

        $ws = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $ws.Cookies.Add((New-Object System.Net.Cookie("odms_session", $token, "/", "localhost")))
        $ws.Cookies.Add((New-Object System.Net.Cookie("odms_csrf", $csrf, "/", "localhost")))

        # A Set-Cookie value arrives percent-encoded. The cookie container sends
        # it back as it came and the server decodes it; a plain header is not
        # decoded, so the header form has to be unescaped here.
        $ws | Add-Member -NotePropertyName Csrf `
            -NotePropertyValue ([System.Uri]::UnescapeDataString($csrf)) -Force
        return $ws
    }

    # POSTs JSON and returns the parsed response, for the steps that need the
    # created record's id rather than just its status code.
    function Invoke-Api([string]$path, $session, $body) {
        return Invoke-RestMethod "$baseUrl$path" -Method Post `
            -Body ($body | ConvertTo-Json -Depth 5) -ContentType "application/json" `
            -Headers @{ "X-Tenant" = "northgroup"; "X-CSRF-Token" = $session.Csrf } `
            -WebSession $session
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
            if ($session) {
                $call.WebSession = $session
                if ($method -ne "Get" -and $session.Csrf) {
                    if (-not $call.Headers) { $call.Headers = @{} }
                    $call.Headers["X-CSRF-Token"] = $session.Csrf
                }
            }
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

    # Holding the permission is not enough: it must not be your own deal.
    $ownUnit = Invoke-Api "/api/v1/inventory" $orgWide @{
        vehicleId = $vehicle.id; rooftopId = $firstRooftopId; stockNumber = "W$suffix"
    }
    $null = Invoke-Api "/api/v1/inventory/$($ownUnit.id)/status" $orgWide @{ status = "Available" }

    $ownDeal = Invoke-Api "/api/v1/deals" $orgWide @{
        rooftopId = $firstRooftopId; customerId = $customer.id
        inventoryUnitId = $ownUnit.id; currency = "USD"
    }
    $null = Invoke-Api "/api/v1/deals/$($ownDeal.id)/terms" $orgWide @{
        charges = @(@{ kind = "VehiclePrice"; description = "The car"; amount = 20000 })
    }
    $null = Get-Status "/api/v1/deals/$($ownDeal.id)/status" "northgroup" $orgWide "Post" @{ status = "Submitted" }
    $selfApprove = Get-Status "/api/v1/deals/$($ownDeal.id)/status" "northgroup" $orgWide "Post" @{ status = "Approved" }
    "manager approves their OWN deal       -> HTTP $selfApprove (expect 403)"

    # The car is held by that deal, so a second deal on it must be refused.
    $doubleSell = Get-Status "/api/v1/deals" "northgroup" $orgWide "Post" @{
        rooftopId = $firstRooftopId; customerId = $customer.id
        inventoryUnitId = $unit.id; currency = "USD"
    }
    "second deal on the same car           -> HTTP $doubleSell (expect 409)"

    Write-Host "`n--- the ledger behind the sale ---" -ForegroundColor Cyan
    $null = Invoke-Api "/api/v1/deals/$($deal.id)/status" $orgWide @{ status = "Delivered" }

    $entries = Invoke-RestMethod "$baseUrl/api/v1/accounting/journal?reference=$($deal.id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $entryCount = @($entries).Count
    "delivering the car posted {0} entry(ies)  (expect exactly 1)" -f $entryCount

    $posted = Invoke-RestMethod "$baseUrl/api/v1/accounting/journal/$(@($entries)[0].id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    "entry debits {0} vs credits {1}        (expect equal)" -f $posted.totalDebits, $posted.totalCredits
    $balanced = ($posted.totalDebits -eq $posted.totalCredits)

    # A correction is a reversal; the original must survive it untouched.
    $reversal = Invoke-Api "/api/v1/accounting/journal/$($posted.id)/reverse" $orgWide @{ reason = "End-to-end check." }
    $originalAfter = Invoke-RestMethod "$baseUrl/api/v1/accounting/journal/$($posted.id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $originalIntact = ($originalAfter.totalDebits -eq $posted.totalDebits)
    "reversal posted, original intact: {0}   (expect True)" -f $originalIntact

    $reverseTwice = Get-Status "/api/v1/accounting/journal/$($posted.id)/reverse" "northgroup" $orgWide "Post" @{ reason = "Again." }
    "reversing the same entry twice        -> HTTP $reverseTwice (expect 409)"

    Write-Host "`n--- anti-forgery on writes ---" -ForegroundColor Cyan
    # The shape of a cross-site forged write: the browser's cookies ride along,
    # but nothing can set the header. It must be refused even though the session
    # is perfectly valid.
    #
    # A used WebRequestSession remembers the headers it was last given, so this
    # needs a fresh one carrying only the cookies — otherwise the check quietly
    # re-sends the very token it is supposed to be doing without.
    $cookiesOnly = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    foreach ($cookie in $orgWide.Cookies.GetCookies($baseUrl)) { $cookiesOnly.Cookies.Add($cookie) }

    try {
        $forgedBody = @{ kind = "Person"; firstName = "Forged"; lastName = "Case$suffix" } | ConvertTo-Json
        $forged = (Invoke-WebRequest "$baseUrl/api/v1/customers" -Method Post `
            -Body $forgedBody -ContentType "application/json" `
            -Headers @{ "X-Tenant" = "northgroup" } -WebSession $cookiesOnly -UseBasicParsing).StatusCode
    } catch { $forged = $_.Exception.Response.StatusCode.value__ }
    "write with the cookie but no token    -> HTTP $forged (expect 403)"

    $honest = Get-Status "/api/v1/customers" "northgroup" $orgWide "Post" @{
        kind = "Person"; firstName = "Honest"; lastName = "Case$suffix"
    }
    "the same write carrying the token     -> HTTP $honest (expect 201)"

    Write-Host "`n--- the dealership can demand a second factor ---" -ForegroundColor Cyan
    # Turned on, observed, and turned off again in a finally block below: the
    # policy is a real row, and leaving it on would break the next run.
    $roles = Invoke-RestMethod "$baseUrl/api/v1/security/second-factor-policy" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $salesRole = @($roles | Where-Object { $_.roleName -eq "Salesperson" })[0]
    "salesperson role: {0} holder(s), {1} still to enrol" -f $salesRole.usersHolding, $salesRole.usersStillToEnrol

    $advisorSetsPolicy = Get-Status "/api/v1/security/second-factor-policy" "northgroup" $scoped "Post" @{
        roleId = $salesRole.roleId; required = $true
    }
    "a one-lot user sets group policy      -> HTTP $advisorSetsPolicy (expect 403)"

    $policyOn = 0
    try {
        $null = Invoke-Api "/api/v1/security/second-factor-policy" $orgWide @{
            roleId = $salesRole.roleId; required = $true
        }
        $policyOn = 1

        # The same salesperson session as before, untouched. The policy has to
        # bite without waiting for them to sign out.
        $blocked = Get-Status "/api/v1/inventory?limit=1" "northgroup" $sales
        "salesperson mid-session, policy on    -> HTTP $blocked (expect 403)"

        $stillIn = Get-Status "/api/v1/auth/me" "northgroup" $sales
        "...but can still be told what to do   -> HTTP $stillIn (expect 200)"

        # This begins an enrolment and does not finish it, so sales@dev.local is
        # left holding an unconfirmed secret. Harmless and re-runnable: a second
        # factor is only in force once a code has confirmed it, and beginning
        # again simply replaces the secret.
        $canEnrol = Get-Status "/api/v1/auth/mfa/enrol" "northgroup" $sales "Post" @{}
        "...and the way out is open           -> HTTP $canEnrol (expect 200)"

        $managerUnaffected = Get-Status "/api/v1/inventory?limit=1" "northgroup" $orgWide
        "manager holds another role            -> HTTP $managerUnaffected (expect 200)"
    }
    finally {
        if ($policyOn -eq 1) {
            $null = Invoke-Api "/api/v1/security/second-factor-policy" $orgWide @{
                roleId = $salesRole.roleId; required = $false
            }
        }
    }

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
        -and ($selfApprove -eq 403) `
        -and ($doubleSell -eq 409) `
        -and ($entryCount -eq 1) -and $balanced -and $originalIntact -and ($reverseTwice -eq 409) `
        -and ($forged -eq 403) -and ($honest -eq 201) `
        -and ($advisorSetsPolicy -eq 403) -and ($blocked -eq 403) -and ($stillIn -eq 200) `
        -and ($canEnrol -eq 200) -and ($managerUnaffected -eq 200) `
        -and ($noSession -eq 401) -and ($crossTenant -eq 401) -and ($afterLogout -eq 401) `
        -and ($missingTenant -eq 400) -and ($unknownTenant -eq 404)

    if ($ok) {
        Write-Host "`nPASS: tenants isolated, sessions enforced, rooftop scope holds, a deal needs a manager, a forged write is refused, a second factor can be demanded, and the ledger balances." -ForegroundColor Green
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
