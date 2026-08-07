# End-to-end verification of the tenancy, sign-in, and authorization foundation:
# two isolated dealer organizations, real sessions, and rooftop-scoped access
# enforced server-side — over both the organization structure and the stock on
# each lot.
#
# The same assertions run in CI via tests/Integration. This script exists for a
# manual check against a real running Host.
#
# Prerequisites: a reachable SQL engine. On Windows, LocalDB is the verified
# option (see docs/LOCAL-DEVELOPMENT.md):
#   sqllocaldb start MSSQLLocalDB
#
# Usage (from repo root, Windows PowerShell 5.1):
#   & .\deploy\verify-e2e.ps1
#   & .\deploy\verify-e2e.ps1 -HostConnection "Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
#
# Pass -Port when something is already on 5080 — typically a development host
# left running for the frontend. Without it the script's own host cannot bind,
# and it silently measures whatever is already there instead.

param(
    [string]$HostConnection = "Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False",
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

# The control plane. Deliberately not a @dev.local address: it is a different
# kind of record, in a different database, and belongs to nobody's dealership.
# The reserved second operator is used rather than root@control.local, because
# enrolling a second factor is one-way and this script resets it below to stay
# re-runnable.
$adminEmail = "newop@control.local"

# --- helpers that need no running host ---

# One value from a database, for the checks that must look at what was actually
# written rather than at what an endpoint said.
function Invoke-Sql-Scalar([string]$sql, [string]$connectionString) {
    $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $sql
        $value = $command.ExecuteScalar()
        if ($value -is [DBNull]) { return $null }
        return $value
    } finally { $connection.Close() }
}

function Invoke-Sql([string]$sql, [string]$connectionString) {
    $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $sql
        $null = $command.ExecuteNonQuery()
    } finally { $connection.Close() }
}

# Mirrors DevelopmentSeeder.TenantDatabaseName: a tenant database is named from
# the host catalog's own name, so an installation called something else keeps its
# databases together.
function Get-TenantConnection([string]$slug) {
    # Indexed rather than by property name: PowerShell routes a property set on a
    # DbConnectionStringBuilder through its IDictionary indexer, which only knows
    # the spaced keyword form and throws on "InitialCatalog".
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $hostConn
    $catalog = $builder["Initial Catalog"]
    if ($catalog.EndsWith("_Host")) { $catalog = $catalog.Substring(0, $catalog.Length - 5) }
    $builder["Initial Catalog"] = "${catalog}_Tenant_$slug"
    return $builder.ConnectionString
}

# RFC 6238, the same six digits an authenticator app produces. Written out here
# rather than called from the application, so the check proves the server agrees
# with an independent implementation instead of with itself.
function ConvertFrom-Base32([string]$value) {
    $alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"
    $bits = New-Object System.Text.StringBuilder
    foreach ($character in $value.ToUpper().ToCharArray()) {
        $index = $alphabet.IndexOf($character)
        if ($index -ge 0) { $null = $bits.Append([Convert]::ToString($index, 2).PadLeft(5, '0')) }
    }
    $text = $bits.ToString()
    $bytes = New-Object System.Collections.Generic.List[byte]
    for ($i = 0; ($i + 8) -le $text.Length; $i += 8) {
        $bytes.Add([Convert]::ToByte($text.Substring($i, 8), 2))
    }
    return $bytes.ToArray()
}

function Get-TotpCode([string]$secret) {
    $counter = [long][Math]::Floor([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() / 30)
    $counterBytes = [BitConverter]::GetBytes($counter)
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($counterBytes) }

    $hmac = New-Object System.Security.Cryptography.HMACSHA1
    $hmac.Key = (ConvertFrom-Base32 $secret)
    $hash = $hmac.ComputeHash($counterBytes)

    $offset = $hash[$hash.Length - 1] -band 0x0F
    $binary = ((($hash[$offset] -band 0x7F) -shl 24) -bor `
               (($hash[$offset + 1] -band 0xFF) -shl 16) -bor `
               (($hash[$offset + 2] -band 0xFF) -shl 8) -bor `
                ($hash[$offset + 3] -band 0xFF))

    return ($binary % 1000000).ToString("D6")
}

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
$hostLog = Join-Path $env:TEMP "dealerfoss-verify-host.log"

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

        $token = Read-Cookie "dfoss_session"
        $csrf  = Read-Cookie "dfoss_csrf"

        $ws = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $ws.Cookies.Add((New-Object System.Net.Cookie("dfoss_session", $token, "/", "localhost")))
        $ws.Cookies.Add((New-Object System.Net.Cookie("dfoss_csrf", $csrf, "/", "localhost")))

        # A Set-Cookie value arrives percent-encoded. The cookie container sends
        # it back as it came and the server decodes it; a plain header is not
        # decoded, so the header form has to be unescaped here.
        $ws | Add-Member -NotePropertyName Csrf `
            -NotePropertyValue ([System.Uri]::UnescapeDataString($csrf)) -Force
        $ws | Add-Member -NotePropertyName CsrfHeader -NotePropertyValue "X-CSRF-Token" -Force
        return $ws
    }

    # Signs an administrator in. Separate cookies from a dealership session on
    # purpose: after support access is granted a browser holds both, and a shared
    # name would mean one silently overwriting the other.
    function New-AdminSession([string]$email) {
        $body = @{ email = $email; password = $devPassword } | ConvertTo-Json
        $login = Invoke-WebRequest "$baseUrl/api/v1/admin/login" -Method Post `
            -Body $body -ContentType "application/json" -UseBasicParsing

        $cookies = $login.Headers['Set-Cookie']
        if ($cookies -is [array]) { $cookies = $cookies -join ', ' }
        function Read-AdminCookie([string]$name) {
            $match = [regex]::Match($cookies, "(?:^|[,;]\s*)$name=([^;,]*)")
            if ($match.Success) { return $match.Groups[1].Value }
            throw "The $name cookie was not set at administrator sign-in."
        }

        $ws = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $ws.Cookies.Add((New-Object System.Net.Cookie("dfoss_admin", (Read-AdminCookie "dfoss_admin"), "/", "localhost")))
        $csrf = Read-AdminCookie "dfoss_admin_csrf"
        $ws.Cookies.Add((New-Object System.Net.Cookie("dfoss_admin_csrf", $csrf, "/", "localhost")))
        $ws | Add-Member -NotePropertyName Csrf `
            -NotePropertyValue ([System.Uri]::UnescapeDataString($csrf)) -Force
        $ws | Add-Member -NotePropertyName CsrfHeader `
            -NotePropertyValue "X-Admin-CSRF-Token" -Force
        return $ws
    }

    # Builds a dealership session from the cookies a response set — used for
    # support access, where the tenant session arrives from a control-plane call
    # rather than from signing in.
    function New-TenantSession($response) {
        $cookies = $response.Headers['Set-Cookie']
        if ($cookies -is [array]) { $cookies = $cookies -join ', ' }

        function Read-TenantCookie([string]$name) {
            $match = [regex]::Match($cookies, "(?:^|[,;]\s*)$name=([^;,]*)")
            if ($match.Success) { return $match.Groups[1].Value }
            throw "The $name cookie was not set by the support-access grant."
        }

        $ws = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $ws.Cookies.Add((New-Object System.Net.Cookie("dfoss_session", (Read-TenantCookie "dfoss_session"), "/", "localhost")))
        $csrf = Read-TenantCookie "dfoss_csrf"
        $ws.Cookies.Add((New-Object System.Net.Cookie("dfoss_csrf", $csrf, "/", "localhost")))
        $ws | Add-Member -NotePropertyName Csrf `
            -NotePropertyValue ([System.Uri]::UnescapeDataString($csrf)) -Force
        $ws | Add-Member -NotePropertyName CsrfHeader -NotePropertyValue "X-CSRF-Token" -Force
        return $ws
    }

    # POSTs JSON and returns the parsed response, for the steps that need the
    # created record's id rather than just its status code.
    function Invoke-Api([string]$path, $session, $body) {
        try {
            return Invoke-RestMethod "$baseUrl$path" -Method Post `
                -Body ($body | ConvertTo-Json -Depth 5) -ContentType "application/json" `
                -Headers @{ "X-Tenant" = "northgroup"; "X-CSRF-Token" = $session.Csrf } `
                -WebSession $session
        }
        catch {
            # Without the path, a failure here reads as "something POSTed
            # somewhere returned 405", which is no help at all in a script this
            # long.
            $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { "no response" }
            throw "POST $path failed with $code. $($_.Exception.Message)"
        }
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
                    # Which header depends on which world the session belongs to.
                    # A dealership token must not satisfy a control-plane write.
                    $header = if ($session.CsrfHeader) { $session.CsrfHeader } else { "X-CSRF-Token" }
                    $call.Headers[$header] = $session.Csrf
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

    # Read before the car leaves, so what follows is this sale's contribution and
    # not whatever the seeded month already held.
    $monthBefore = Invoke-RestMethod "$baseUrl/api/v1/reporting/month" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $grossBefore = ($monthBefore.trading.departments | Where-Object { $_.name -eq "Vehicles" }).gross
    $unitsBefore = $monthBefore.trading.vehiclesDelivered

    $null = Invoke-Api "/api/v1/deals/$($deal.id)/status" $orgWide @{ status = "Delivered" }

    $entries = Invoke-RestMethod "$baseUrl/api/v1/accounting/journal?reference=$($deal.id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $entryCount = @($entries).Count
    "delivering the car posted {0} entry(ies)  (expect exactly 1)" -f $entryCount

    $posted = Invoke-RestMethod "$baseUrl/api/v1/accounting/journal/$(@($entries)[0].id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    "entry debits {0} vs credits {1}        (expect equal)" -f $posted.totalDebits, $posted.totalCredits
    $balanced = ($posted.totalDebits -eq $posted.totalCredits)

    # The dashboard is derived from these same journal lines rather than from the
    # deal, which is the only reason the two can never disagree. The unit was
    # taken into stock with no cost, so the whole 24,000 is front gross.
    $monthAfter = Invoke-RestMethod "$baseUrl/api/v1/reporting/month" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $grossAfter = ($monthAfter.trading.departments | Where-Object { $_.name -eq "Vehicles" }).gross
    "the month now reports {0} more front gross (expect 24000)" -f ($grossAfter - $grossBefore)
    "...on {0} more car(s) delivered           (expect 1)" -f ($monthAfter.trading.vehiclesDelivered - $unitsBefore)

    # A correction is a reversal; the original must survive it untouched.
    $reversal = Invoke-Api "/api/v1/accounting/journal/$($posted.id)/reverse" $orgWide @{ reason = "End-to-end check." }
    $originalAfter = Invoke-RestMethod "$baseUrl/api/v1/accounting/journal/$($posted.id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $originalIntact = ($originalAfter.totalDebits -eq $posted.totalDebits)
    "reversal posted, original intact: {0}   (expect True)" -f $originalIntact

    # And the dashboard follows the correction. The count and the money have to
    # move together, or somebody divides one by the other and gets a nonsense
    # average per car.
    $monthUndone = Invoke-RestMethod "$baseUrl/api/v1/reporting/month" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $grossUndone = ($monthUndone.trading.departments | Where-Object { $_.name -eq "Vehicles" }).gross
    "after the reversal the month is back to {0} (expect {1})" -f ($grossUndone - $grossBefore), 0
    "...and {0} more car(s) than before        (expect 0)" -f ($monthUndone.trading.vehiclesDelivered - $unitsBefore)

    $withheld = Invoke-RestMethod "$baseUrl/api/v1/reporting/month" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $sales
    "a salesperson reads the same month     -> {0} withheld (expect 0)" -f @($withheld.withheld).Count

    # A technician holds neither Accounting.Read nor Inventory.Read. An empty
    # dashboard would read as "the dealership sold nothing", which is a different
    # and much worse statement than "this is not yours to see".
    $noFigures = New-DealerSession "northgroup" "tech@dev.local"
    $technicianDashboard = Get-Status "/api/v1/reporting/month" "northgroup" $noFigures "Get"
    "somebody entitled to none of it        -> HTTP $technicianDashboard (expect 403)"

    $reverseTwice = Get-Status "/api/v1/accounting/journal/$($posted.id)/reverse" "northgroup" $orgWide "Post" @{ reason = "Again." }
    "reversing the same entry twice        -> HTTP $reverseTwice (expect 409)"

    # Posting an entry and undoing one are different rights. The salesperson just
    # posted this one by delivering the car, which is exactly what makes the
    # refusal meaningful rather than an accident of them holding nothing.
    $salesReverse = Get-Status "/api/v1/accounting/journal/$($posted.id)/reverse" "northgroup" $sales "Post" @{ reason = "Undo my own posting." }
    "salesperson reverses their own entry  -> HTTP $salesReverse (expect 403)"

    Write-Host "`n--- the workshop: work nobody agreed to is not billed ---" -ForegroundColor Cyan
    $technician = New-DealerSession "northgroup" "tech@dev.local"

    # A car booked in for a service, with a second fault found once it was on the
    # ramp. The found work cannot reach an invoice until somebody has actually
    # asked the customer — which is the control the capability exists to hold.
    $job = Invoke-Api "/api/v1/repair-orders" $orgWide @{
        rooftopId = $firstRooftopId; customerId = $customer.id; vehicleId = $vehicle.id
        complaint = "Squealing from the front when braking."; currency = "USD"; odometerReading = 48210
    }
    "booked in as {0}                  (expect RO-something)" -f $job.number

    $null = Invoke-Api "/api/v1/repair-orders/$($job.id)/lines" $orgWide @{
        kind = "Labour"; description = "Full service"; hours = 1.5; rate = 120
    }
    $null = Invoke-Api "/api/v1/repair-orders/$($job.id)/status" $orgWide @{ status = "InProgress" }

    # Found on the ramp: nobody has rung the customer about this one.
    $withFound = Invoke-Api "/api/v1/repair-orders/$($job.id)/lines" $orgWide @{
        kind = "Part"; description = "Front discs and pads"; unitAmount = 284
    }
    $pending = @($withFound.lines | Where-Object { $_.authorization -eq "Pending" })
    "work found on the ramp: {0} line(s)      (expect 1)" -f $pending.Count

    $null = Invoke-Api "/api/v1/repair-orders/$($job.id)/status" $orgWide @{ status = "Completed" }

    $billTooSoon = Get-Status "/api/v1/repair-orders/$($job.id)/status" "northgroup" $orgWide "Post" @{ status = "Invoiced" }
    "invoicing with work unanswered        -> HTTP $billTooSoon (expect 409)"

    # A technician writes work up; saying the customer agreed to pay is not theirs.
    $techAuthorises = Get-Status "/api/v1/repair-orders/$($job.id)/lines/$($pending[0].id)/answer" "northgroup" $technician "Post" @{ approved = $true }
    "technician says customer agreed       -> HTTP $techAuthorises (expect 403)"

    $null = Invoke-Api "/api/v1/repair-orders/$($job.id)/lines/$($pending[0].id)/answer" $orgWide @{
        approved = $true; note = "Phoned 10:40, agreed."
    }
    $invoiced = Invoke-Api "/api/v1/repair-orders/$($job.id)/status" $orgWide @{ status = "Invoiced" }
    "once answered, the job bills {0}     (expect 464)" -f $invoiced.amountDue

    $serviceEntries = Invoke-RestMethod "$baseUrl/api/v1/accounting/journal?reference=$($job.id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $serviceEntry = Invoke-RestMethod "$baseUrl/api/v1/accounting/journal/$(@($serviceEntries)[0].id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide

    # Labour and parts land on their own accounts: "we sold 464 of service" is
    # useless to a workshop manager, and the split is what they run on.
    $labour = (@($serviceEntry.lines | Where-Object { $_.accountCode -eq "4200" }) | Measure-Object -Property credit -Sum).Sum
    $parts = (@($serviceEntry.lines | Where-Object { $_.accountCode -eq "4300" }) | Measure-Object -Property credit -Sum).Sum
    "posted as labour {0} and parts {1}   (expect 180 and 284)" -f $labour, $parts
    $serviceBalanced = ($serviceEntry.totalDebits -eq $serviceEntry.totalCredits)
    "service entry debits {0} vs credits {1} (expect equal)" -f $serviceEntry.totalDebits, $serviceEntry.totalCredits

    Write-Host "`n--- parts: the workshop knows what the job cost, not just what it billed ---" -ForegroundColor Cyan

    # Two deliveries at different prices, so the costing methods genuinely
    # disagree and the setting is doing something.
    $partNumber = "VE" + [Guid]::NewGuid().ToString("N").Substring(0, 10)
    $part = Invoke-Api "/api/v1/parts" $orgWide @{ partNumber = $partNumber; description = "Front brake pad set" }
    $null = Invoke-Api "/api/v1/parts/$($part.id)/receipts" $orgWide @{
        quantity = 10; unitCost = 5; rooftopId = $firstRooftopId; reference = "DN-1001"
    }
    $null = Invoke-Api "/api/v1/parts/$($part.id)/receipts" $orgWide @{
        quantity = 10; unitCost = 9; rooftopId = $firstRooftopId; reference = "DN-1002"
    }
    $stocked = Invoke-RestMethod "$baseUrl/api/v1/parts/$($part.id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $shelf = @($stocked.stock)[0]
    "booked in 20, average cost {0}         (expect 7)" -f $shelf.unitCost

    # A job that sells two of them. This is the whole point: before parts were
    # real stock, invoicing recorded revenue and no cost at all.
    $partsJob = Invoke-Api "/api/v1/repair-orders" $orgWide @{
        rooftopId = $firstRooftopId; customerId = $customer.id; vehicleId = $vehicle.id
        complaint = "Brakes."; currency = "USD"
    }
    $null = Invoke-Api "/api/v1/repair-orders/$($partsJob.id)/lines" $orgWide @{
        kind = "Part"; description = "Front brake pad set"; unitAmount = 90
        partId = $part.id; partQuantity = 2
    }
    $null = Invoke-Api "/api/v1/repair-orders/$($partsJob.id)/status" $orgWide @{ status = "InProgress" }
    $null = Invoke-Api "/api/v1/repair-orders/$($partsJob.id)/status" $orgWide @{ status = "Completed" }
    $partsInvoiced = Invoke-Api "/api/v1/repair-orders/$($partsJob.id)/status" $orgWide @{ status = "Invoiced" }

    $soldLine = @($partsInvoiced.lines | Where-Object { $null -ne $_.partId })[0]
    "sold 2, cost recorded {0}             (expect 14)" -f $soldLine.cost

    $afterSale = Invoke-RestMethod "$baseUrl/api/v1/parts/$($part.id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    "stock left on the shelf {0}            (expect 18)" -f (@($afterSale.stock)[0].quantityOnHand)

    # And the books carry it: cost of parts sales debited, parts inventory
    # credited. That pair is what turns revenue into a profit figure.
    $partsEntries = Invoke-RestMethod "$baseUrl/api/v1/accounting/journal?reference=$($partsJob.id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $partsEntry = Invoke-RestMethod "$baseUrl/api/v1/accounting/journal/$(@($partsEntries)[0].id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $cogs = (@($partsEntry.lines | Where-Object { $_.accountCode -eq "5300" }) | Measure-Object -Property debit -Sum).Sum
    $shelfCredit = (@($partsEntry.lines | Where-Object { $_.accountCode -eq "1400" }) | Measure-Object -Property credit -Sum).Sum
    "posted cost {0}, off the shelf {1}     (expect 14 and 14)" -f $cogs, $shelfCredit
    $partsBalanced = ($partsEntry.totalDebits -eq $partsEntry.totalCredits)
    "parts entry debits {0} vs credits {1} (expect equal)" -f $partsEntry.totalDebits, $partsEntry.totalCredits

    # Selling what is not there is refused rather than going negative.
    $shortJob = Invoke-Api "/api/v1/repair-orders" $orgWide @{
        rooftopId = $firstRooftopId; customerId = $customer.id; vehicleId = $vehicle.id
        complaint = "More brakes."; currency = "USD"
    }
    $null = Invoke-Api "/api/v1/repair-orders/$($shortJob.id)/lines" $orgWide @{
        kind = "Part"; description = "Front brake pad set"; unitAmount = 90
        partId = $part.id; partQuantity = 500
    }
    $null = Invoke-Api "/api/v1/repair-orders/$($shortJob.id)/status" $orgWide @{ status = "InProgress" }
    $null = Invoke-Api "/api/v1/repair-orders/$($shortJob.id)/status" $orgWide @{ status = "Completed" }
    $shortStatus = Get-Status "/api/v1/repair-orders/$($shortJob.id)/status" "northgroup" $orgWide "Post" @{ status = "Invoiced" }
    "invoicing 500 of a part we have 18 of -> HTTP $shortStatus (expect 409)"

    # How the group values its stock is not a decision one lot makes.
    $scopedCosting = Get-Status "/api/v1/parts/costing" "northgroup" $scoped "Post" @{ method = "Fifo" }
    "one-lot user changes costing method   -> HTTP $scopedCosting (expect 403)"

    $siblingJob = Invoke-Api "/api/v1/repair-orders" $orgWide @{
        rooftopId = $siblingId; customerId = $customer.id; vehicleId = $vehicle.id
        complaint = "Service due."; currency = "USD"
    }
    $siblingJobStatus = Get-Status "/api/v1/repair-orders/$($siblingJob.id)" "northgroup" $scoped
    "scoped user -> sibling job by id      -> HTTP $siblingJobStatus (expect 403)"

    Write-Host "`n--- F&I: what was sold with the car, and what it made ---" -ForegroundColor Cyan

    # A provider arrangement is a group-level thing.
    $scopedProduct = Get-Status "/api/v1/finance/products" "northgroup" $scoped "Post" @{
        name = "Scoped attempt"; kind = "Warranty"; provider = "X"; defaultPrice = 1; defaultCost = 1
    }
    "a one-lot user adds to the catalogue  -> HTTP $scopedProduct (expect 403)"

    $cover = Invoke-Api "/api/v1/finance/products" $orgWide @{
        name = "3-year warranty $(New-Guid)"; kind = "Warranty"; provider = "Northgate Underwriting"
        defaultPrice = 1200; defaultCost = 700; currency = "USD"; termMonths = 36
    }

    # Sold at a discount to hold the deal together — the recorded gross has to
    # follow the price actually agreed, not the catalogue's.
    # A car of its own: a deal holds its unit, so reusing one would fail for a
    # reason that has nothing to do with F&I.
    $fiUnit = Invoke-Api "/api/v1/inventory" $orgWide @{
        vehicleId = $vehicle.id; rooftopId = $firstRooftopId; stockNumber = "F$suffix"
    }
    $null = Invoke-Api "/api/v1/inventory/$($fiUnit.id)/status" $orgWide @{ status = "Available" }

    $fiDeal = Invoke-Api "/api/v1/deals" $sales @{
        rooftopId = $firstRooftopId; customerId = $customer.id
        inventoryUnitId = $fiUnit.id; currency = "USD"
    }
    $null = Invoke-Api "/api/v1/deals/$($fiDeal.id)/terms" $orgWide @{
        charges = @(@{ kind = "VehiclePrice"; description = "Car"; amount = 20000 })
    }
    $withCover = Invoke-Api "/api/v1/deals/$($fiDeal.id)/products" $orgWide @{
        products = @(@{ financeProductId = $cover.id; price = 900; cost = 700 })
    }
    "the deal now owes {0}              (expect 20900)" -f $withCover.amountDue
    "and the cover made {0}                (expect 200)" -f $withCover.productGross

    # Next month's price list must not rewrite this month's gross.
    $null = Invoke-Api "/api/v1/finance/products/$($cover.id)/price" $orgWide @{
        defaultPrice = 1500; defaultCost = 950
    }
    $afterReprice = Invoke-RestMethod "$baseUrl/api/v1/deals/$($fiDeal.id)" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    "after repricing the catalogue: {0}    (expect 200)" -f $afterReprice.productGross

    Write-Host "`n--- the month can be closed, and a closed month refuses ---" -ForegroundColor Cyan

    $now = [DateTime]::UtcNow
    $periodPath = "/api/v1/accounting/periods/$($now.Year)/$($now.Month)"

    # One lot does not close the group's books.
    $scopedClose = Get-Status "$periodPath/close" "northgroup" $scoped "Post" @{ note = $null }
    "a one-lot user closes the month       -> HTTP $scopedClose (expect 403)"

    $null = Invoke-Api "$periodPath/close" $orgWide @{ note = "Month-end done." }

    # Anything that posts is now refused, and says why.
    $lockedJob = Invoke-Api "/api/v1/repair-orders" $orgWide @{
        rooftopId = $firstRooftopId; customerId = $customer.id; vehicleId = $vehicle.id
        complaint = "After the close."; currency = "USD"
    }
    $null = Invoke-Api "/api/v1/repair-orders/$($lockedJob.id)/lines" $orgWide @{
        kind = "Labour"; description = "An hour"; hours = 1; rate = 100
    }
    $null = Invoke-Api "/api/v1/repair-orders/$($lockedJob.id)/status" $orgWide @{ status = "InProgress" }
    $null = Invoke-Api "/api/v1/repair-orders/$($lockedJob.id)/status" $orgWide @{ status = "Completed" }
    $postIntoClosed = Get-Status "/api/v1/repair-orders/$($lockedJob.id)/status" "northgroup" $orgWide "Post" @{ status = "Invoiced" }
    "invoicing into a closed month         -> HTTP $postIntoClosed (expect 409)"

    # Reopening needs a reason on the record.
    $reopenNoReason = Get-Status "$periodPath/reopen" "northgroup" $orgWide "Post" @{ note = $null }
    "reopening with no reason              -> HTTP $reopenNoReason (expect 400)"

    # And reopening is its own permission, not the one that closed it.
    # Unique per run. The period's history is append-only and this script is run
    # repeatedly against the same database, so a fixed reason would accumulate and
    # the count below would climb with every run — which is what happened.
    $reopenReason = "A supplier invoice arrived on the 4th ($suffix)."
    $reopened = Invoke-Api "$periodPath/reopen" $orgWide @{ note = $reopenReason }
    "reopened, state now {0}              (expect Open)" -f $reopened.state

    $afterReopen = Invoke-Api "/api/v1/repair-orders/$($lockedJob.id)/status" $orgWide @{ status = "Invoiced" }
    "and the job invoices {0}             (expect 100)" -f $afterReopen.amountDue

    # Every transition is kept, including why a reported month was unlocked.
    $periods = Invoke-RestMethod "$baseUrl/api/v1/accounting/periods" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $thisMonth = @($periods | Where-Object { $_.year -eq $now.Year -and $_.month -eq $now.Month })[0]
    $reopenNote = @($thisMonth.history | Where-Object { $_.note -eq $reopenReason }).Count
    "the reopen is on the record: {0}       (expect 1)" -f $reopenNote

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

    Write-Host "`n--- whoever runs the servers is kept out of the data ---" -ForegroundColor Cyan
    # Two crossings, and neither is allowed. An administrator cookie is not a
    # caller at a business endpoint, and the most privileged dealership account
    # is still a dealership account at the control plane.
    # Enrolling is one-way, and this script runs more than once against the same
    # development database. Clearing the second factor first is what makes the
    # section below repeatable; nothing else here writes to the control plane.
    Invoke-Sql `
        "UPDATE [control].[Administrators] SET MfaSecretProtected = NULL, MfaConfirmedAt = NULL WHERE Email = '$adminEmail'" `
        $hostConn

    $adminSession = New-AdminSession $adminEmail

    $adminReadsData = Get-Status "/api/v1/inventory?limit=1" "northgroup" $adminSession
    "administrator reads dealership stock  -> HTTP $adminReadsData (expect 401)"

    $dealerReachesAdmin = Get-Status "/api/v1/admin/tenants" $null $scoped
    "dealership manager at the control plane -> HTTP $dealerReachesAdmin (expect 401)"

    # A second factor is not optional here: the account that can step into any
    # dealership is the one worth stealing.
    $adminBeforeMfa = Get-Status "/api/v1/admin/tenants" $null $adminSession
    "administrator without a second factor -> HTTP $adminBeforeMfa (expect 403)"

    $enrolment = Invoke-RestMethod "$baseUrl/api/v1/admin/mfa/enrol" -Method Post `
        -Headers @{ "X-Admin-CSRF-Token" = $adminSession.Csrf } -WebSession $adminSession
    $confirmed = Get-Status "/api/v1/admin/mfa/confirm" $null $adminSession "Post" `
        @{ code = (Get-TotpCode $enrolment.secret) }
    "...enrols one, in the same session    -> HTTP $confirmed (expect 204)"

    $adminAfterMfa = Get-Status "/api/v1/admin/tenants" $null $adminSession
    "...and can now run the installation   -> HTTP $adminAfterMfa (expect 200)"

    Write-Host "`n--- support access is deliberate, limited, and visible ---" -ForegroundColor Cyan
    $noReason = Get-Status "/api/v1/admin/support-access" "northgroup" $adminSession "Post" `
        @{ reason = "  "; minutes = 30 }
    "entering a dealership with no reason  -> HTTP $noReason (expect 400)"

    # Invoke-WebRequest rather than Invoke-RestMethod, because the tenant cookies
    # this hands back are the whole point and only the raw response carries them.
    $granting = Invoke-WebRequest "$baseUrl/api/v1/admin/support-access" -Method Post `
        -Body (@{ reason = "End-to-end check of the support path."; minutes = 1440 } | ConvertTo-Json) `
        -ContentType "application/json" `
        -Headers @{ "X-Tenant" = "northgroup"; "X-Admin-CSRF-Token" = $adminSession.Csrf } `
        -WebSession $adminSession -UseBasicParsing
    $grant = $granting.Content | ConvertFrom-Json

    # Asked for a day, and the ceiling is not negotiable.
    $grantMinutes = [Math]::Round(([DateTimeOffset]$grant.expiresAt - [DateTimeOffset]::UtcNow).TotalMinutes)
    "asked for 1440 minutes, granted {0}    (expect about 60)" -f $grantMinutes

    $support = New-TenantSession $granting
    $supportReads = Get-Status "/api/v1/inventory?limit=1" "northgroup" $support
    "support session reads the stock list  -> HTTP $supportReads (expect 200)"

    $supportWrites = Get-Status "/api/v1/customers" "northgroup" $support "Post" `
        @{ kind = "Person"; firstName = "Support"; lastName = "NoWrite$suffix" }
    "support session adds a customer       -> HTTP $supportWrites (expect 403: read-only)"

    # Visible where it matters: the dealership's own audit trail, not only ours.
    $seenByDealer = Invoke-Sql-Scalar `
        "SELECT TOP 1 Reason FROM [identity].[AuditEvents] WHERE Action = 'Support.AccessOpened' ORDER BY OccurredAt DESC" `
        (Get-TenantConnection "northgroup")
    $dealerCanSee = ($seenByDealer -ne $null -and $seenByDealer.Contains($adminEmail))
    "the dealership's own log names them: {0} (expect True)" -f $dealerCanSee

    $ended = Get-Status "/api/v1/admin/support-access/$($grant.grantId)/end" "northgroup" $adminSession "Post"
    "closing the grant                     -> HTTP $ended (expect 204)"

    $afterEnd = Get-Status "/api/v1/inventory?limit=1" "northgroup" $support
    "...the session stops on the next call -> HTTP $afterEnd (expect 401)"

    Write-Host "`n--- a dealership's old records arrive from a file ---" -ForegroundColor Cyan
    # Run against a live host with the background worker actually running, which
    # is the half the integration suite shares but a person cannot see.
    # Exactly seventeen characters: anything else is a legitimate VIN only when
    # the file says why, which is a separate case the integration suite covers.
    $vinStem = (New-Guid).ToString("N").Substring(0, 10).ToUpper()
    $file = "vin,modelyear,make,model,trim`n" +
            ("{0}A000000,2021,Toyota,RAV4,XLE`n" -f $vinStem) +
            ("{0}B000000,2019,Ford,F-150," -f $vinStem)

    function Submit-Import([string]$mode, [string]$content) {
        $body = @{ kind = "Vehicles"; mode = $mode; sourceName = "e2e.csv"; content = $content }
        $job = Invoke-Api "/api/v1/migration/imports" $orgWide $body

        # Submitting only stages the rows; the worker does the work. Poll until
        # it is finished rather than assuming a fixed wait is long enough.
        for ($i = 0; $i -lt 60; $i++) {
            $job = Invoke-RestMethod "$baseUrl/api/v1/migration/imports/$($job.id)" `
                -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
            if ($job.status -eq "Completed" -or $job.status -eq "Failed") { return $job }
            Start-Sleep -Milliseconds 500
        }
        throw "The import did not finish. Is the background worker running?"
    }

    $trial = Submit-Import "Trial" $file
    "a practice run says {0} would be added   (expect 2)" -f $trial.rowsCreated

    # An empty JSON array comes back as $null, and @($null).Count is 1 — so
    # counting the naive way would report a car that is not there.
    $found = Invoke-RestMethod "$baseUrl/api/v1/vehicles?search=$vinStem" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $beforeApply = if ($null -eq $found) { 0 } else { @($found).Count }
    "...and changed nothing: {0} car(s) here    (expect 0)" -f $beforeApply

    $applied = Submit-Import "Apply" $file
    "the real run adds {0}                      (expect 2)" -f $applied.rowsCreated

    $again = Submit-Import "Apply" $file
    "running the same file again adds {0}       (expect 0)" -f $again.rowsCreated
    "...recognising {0} already here            (expect 2)" -f $again.rowsSkipped

    # A bad row is reported by the line number a person sees, and does not stop
    # the rest of the file.
    $mixed = "vin,modelyear,make,model`n" +
             ("{0}C000000,2020,Honda,Civic`n" -f $vinStem) +
             ("{0}D000000,not-a-year,Mazda,CX-5" -f $vinStem)
    $partial = Submit-Import "Apply" $mixed
    "a file with one bad row: {0} in, {1} refused (expect 1 and 1)" -f $partial.rowsCreated, $partial.rowsFailed

    $problems = Invoke-RestMethod "$baseUrl/api/v1/migration/imports/$($partial.id)/rows?problemsOnly=true" `
        -Headers @{ "X-Tenant" = "northgroup" } -WebSession $orgWide
    $badRowNumber = @($problems)[0].rowNumber
    "...the bad row is line {0}                  (expect 3)" -f $badRowNumber

    $advisorImports = Get-Status "/api/v1/migration/imports" "northgroup" $scoped "Post" @{
        kind = "Vehicles"; mode = "Trial"; sourceName = "x.csv"; content = $file
    }
    "a one-lot user imports the group's data -> HTTP $advisorImports (expect 403)"

    Write-Host "`n--- and the dealership can take them away again ---" -ForegroundColor Cyan
    # The promise an open DMS makes: you can leave, and take your data. Proven by
    # exporting one dealership and feeding that exact file to a different one
    # through the ordinary import endpoint — no converter, no special handling.
    $exportUrl = "$baseUrl/api/v1/migration/exports/Vehicles"
    $export = Invoke-WebRequest $exportUrl -Headers @{ "X-Tenant" = "northgroup" } `
        -WebSession $orgWide -UseBasicParsing

    # Invoke-WebRequest hands back .Content as a string for a text media type and
    # as bytes for a binary one. Handle both rather than assuming, because the
    # wrong guess fails with a type error a long way from the cause.
    $exportText = if ($export.Content -is [byte[]]) {
        [Text.Encoding]::UTF8.GetString($export.Content)
    } else {
        [string]$export.Content
    }

    $sha = [Security.Cryptography.SHA256]::Create()
    $computed = ([BitConverter]::ToString(
        $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($exportText))) -replace '-', '').ToLower()
    $publishedHash = $export.Headers['X-Content-SHA256']
    if ($publishedHash -is [array]) { $publishedHash = $publishedHash[0] }
    $checksumMatches = ($computed -eq $publishedHash)
    "the export's checksum matches: {0}      (expect True)" -f $checksumMatches

    $exportRows = [int]($export.Headers['X-Row-Count'] | Select-Object -First 1)
    "northgroup exports {0} vehicle(s)        (expect 1 or more)" -f $exportRows

    # Straight into the other dealership, unmodified.
    $cityBody = @{
        kind = "Vehicles"; mode = "Apply"
        sourceName = "from-northgroup.csv"; content = $exportText
    } | ConvertTo-Json -Depth 5

    $handedOver = Invoke-RestMethod "$baseUrl/api/v1/migration/imports" -Method Post `
        -Body $cityBody -ContentType "application/json" `
        -Headers @{ "X-Tenant" = "citymotors"; "X-CSRF-Token" = $cityWide.Csrf } `
        -WebSession $cityWide

    $roundTrip = $null
    for ($i = 0; $i -lt 60; $i++) {
        $roundTrip = Invoke-RestMethod "$baseUrl/api/v1/migration/imports/$($handedOver.id)" `
            -Headers @{ "X-Tenant" = "citymotors" } -WebSession $cityWide
        if ($roundTrip.status -eq "Completed" -or $roundTrip.status -eq "Failed") { break }
        Start-Sleep -Milliseconds 500
    }

    "citymotors reads it back: {0} refused    (expect 0)" -f $roundTrip.rowsFailed
    $roundTripRead = ($roundTrip.rowsCreated + $roundTrip.rowsUpdated + $roundTrip.rowsSkipped)
    "...understanding {0} of {1} row(s)" -f $roundTripRead, $roundTrip.rowsTotal

    $advisorExports = Get-Status "/api/v1/migration/exports/Vehicles" "northgroup" $scoped
    "a one-lot user exports the group's data -> HTTP $advisorExports (expect 403)"

    Write-Host "`n--- sessions ---" -ForegroundColor Cyan
    $noSession = Get-Status "/api/v1/organization" "northgroup" $null
    "no session                            -> HTTP $noSession (expect 401)"

    $crossTenant = Get-Status "/api/v1/organization" "citymotors" $scoped
    "northgroup session at citymotors      -> HTTP $crossTenant (expect 401)"

    # Revoke, then prove the very same session stops working at once.
    $null = Get-Status "/api/v1/auth/logout" "northgroup" $orgWide "Post"
    $afterLogout = Get-Status "/api/v1/organization" "northgroup" $orgWide
    "after sign-out, same session          -> HTTP $afterLogout (expect 401)"

    Write-Host "`n--- guessing a password stops being answered (last: it uses up the allowance) ---" -ForegroundColor Cyan

    # Proven here rather than in the integration suite: that runs in-process and
    # makes hundreds of sign-ins down one connection, which no partitioning can
    # tell apart from an attack. This is a real host over a real socket, with the
    # production limit.
    $throttled = 0
    for ($i = 0; $i -lt 40; $i++) {
        $attempt = Get-Status "/api/v1/auth/login" "northgroup" $null "Post" @{
            email = "gm@dev.local"; password = "wrong-$i"
        }
        if ($attempt -eq 429) { $throttled++ }
    }
    "40 wrong passwords -> {0} refused as too many (expect 1 or more)" -f $throttled

    # And the headers a browser needs, on every response.
    $headers = (Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing).Headers
    $noSniff = $headers["X-Content-Type-Options"]
    $frameDeny = $headers["X-Frame-Options"]
    "security headers: {0} / {1}    (expect nosniff / DENY)" -f $noSniff, $frameDeny
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
        -and ($adminReadsData -eq 401) -and ($dealerReachesAdmin -eq 401) `
        -and ($adminBeforeMfa -eq 403) -and ($confirmed -eq 204) -and ($adminAfterMfa -eq 200) `
        -and ($noReason -eq 400) -and ($grantMinutes -ge 55 -and $grantMinutes -le 61) `
        -and ($supportReads -eq 200) -and ($supportWrites -eq 403) -and $dealerCanSee `
        -and ($ended -eq 204) -and ($afterEnd -eq 401) `
        -and $checksumMatches -and ($exportRows -ge 1) `
        -and ($roundTrip.rowsFailed -eq 0) -and ($roundTripRead -eq $roundTrip.rowsTotal) `
        -and ($advisorExports -eq 403) `
        -and ($trial.rowsCreated -eq 2) -and ($beforeApply -eq 0) `
        -and ($applied.rowsCreated -eq 2) `
        -and ($again.rowsCreated -eq 0) -and ($again.rowsSkipped -eq 2) `
        -and ($partial.rowsCreated -eq 1) -and ($partial.rowsFailed -eq 1) `
        -and ($badRowNumber -eq 3) -and ($advisorImports -eq 403) `
        -and ($noSession -eq 401) -and ($crossTenant -eq 401) -and ($afterLogout -eq 401) `
        -and ($missingTenant -eq 400) -and ($unknownTenant -eq 404) `
        -and ($salesReverse -eq 403) `
        -and ($pending.Count -eq 1) -and ($billTooSoon -eq 409) -and ($techAuthorises -eq 403) `
        -and ($invoiced.amountDue -eq 464) -and ($labour -eq 180) -and ($parts -eq 284) `
        -and $serviceBalanced -and ($siblingJobStatus -eq 403) `
        -and ($shelf.unitCost -eq 7) -and ($soldLine.cost -eq 14) `
        -and ((@($afterSale.stock)[0].quantityOnHand) -eq 18) `
        -and ($cogs -eq 14) -and ($shelfCredit -eq 14) -and $partsBalanced `
        -and ($shortStatus -eq 409) -and ($scopedCosting -eq 403) `
        -and ($throttled -ge 1) -and ($noSniff -eq "nosniff") -and ($frameDeny -eq "DENY") `
        -and ($scopedProduct -eq 403) -and ($withCover.amountDue -eq 20900) `
        -and ($withCover.productGross -eq 200) -and ($afterReprice.productGross -eq 200) `
        -and ($scopedClose -eq 403) -and ($postIntoClosed -eq 409) `
        -and ($reopenNoReason -eq 400) -and ($reopened.state -eq "Open") `
        -and ($afterReopen.amountDue -eq 100) -and ($reopenNote -eq 1)

    if ($ok) {
        Write-Host "`nPASS: tenants isolated, sessions enforced, rooftop scope holds, a deal needs a manager, work nobody agreed to is not billed, parts leave the shelf at cost so service has a profit figure, a closed month refuses postings until somebody reopens it on the record, a forged write is refused, a second factor can be demanded, whoever runs the servers is kept out of the data, an old system's records import safely, a dealership can take its data away and load it somewhere else, the month reads back exactly what the ledger holds, and the ledger balances." -ForegroundColor Green
    } else {
        Write-Host "`nFAIL: expectations not met." -ForegroundColor Red
        exit 1
    }
}
finally {
    Write-Host "Stopping Host..." -ForegroundColor DarkGray
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like "*DealerFOSS.Host*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}
