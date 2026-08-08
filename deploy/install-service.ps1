# install-service.ps1 — register a published folder as a Windows service.
#
# Use (as Administrator, on the target machine):
#   & .\deploy\install-service.ps1 -Path C:\DealerFOSS\app `
#       -Connection "Server=.;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" `
#       -KeyId 2026-08 -Key "<base64 from new-key.ps1>"
#
#   & .\deploy\install-service.ps1 -Uninstall
#
# Edit: two things here are deliberate and should not be "simplified".
#
#       The key and the connection string are set as SERVICE-SCOPED environment
#       variables, written into the service's own registry key rather than
#       machine-wide with setx. Machine-wide means every process on the box can
#       read the key that decrypts every dealership's connection string,
#       including anything a person runs from a browser download.
#
#       The script refuses to install without a key. The application already
#       refuses to start without one outside Development, so installing anyway
#       produces a service that fails on boot with a message nobody sees. Better
#       to fail here, where somebody is watching.

[CmdletBinding(DefaultParameterSetName = "Install")]
param(
    [Parameter(ParameterSetName = "Install", Mandatory = $true)]
    [string]$Path,

    [Parameter(ParameterSetName = "Install", Mandatory = $true)]
    [string]$Connection,

    [Parameter(ParameterSetName = "Install", Mandatory = $true)]
    [string]$KeyId,

    [Parameter(ParameterSetName = "Install", Mandatory = $true)]
    [string]$Key,

    [Parameter(ParameterSetName = "Install")]
    [string]$Url = "http://localhost:5080",

    # The account the service runs as. The default is the machine account, which
    # is what makes Trusted_Connection work against a SQL Server on the same
    # domain without a password anywhere.
    [Parameter(ParameterSetName = "Install")]
    [string]$Account = "NT AUTHORITY\NETWORK SERVICE",

    [Parameter(ParameterSetName = "Install")]
    [string]$Name = "DealerFOSS",

    [Parameter(ParameterSetName = "Uninstall", Mandatory = $true)]
    [switch]$Uninstall,

    [Parameter(ParameterSetName = "Uninstall")]
    [string]$ServiceName = "DealerFOSS"
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    throw "Run this from an elevated PowerShell. Registering a service needs Administrator."
}

if ($Uninstall) {
    $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $existing) {
        Write-Host "No service called $ServiceName. Nothing to do."
        return
    }

    if ($existing.Status -ne "Stopped") {
        Write-Host "Stopping $ServiceName..."
        Stop-Service -Name $ServiceName -Force
        $existing.WaitForStatus("Stopped", (New-TimeSpan -Seconds 30))
    }

    # sc.exe rather than Remove-Service, which needs PowerShell 6+. The target is
    # Windows PowerShell 5.1.
    & sc.exe delete $ServiceName | Out-Null
    Write-Host "Removed $ServiceName." -ForegroundColor Green
    Write-Host "The databases and the published folder are untouched. Delete them yourself if you meant to."
    return
}

# --- checks worth failing on, before anything is registered ---

$full = (Resolve-Path $Path).Path
$exe = Join-Path $full "DealerFOSS.App.exe"

if (-not (Test-Path $exe)) {
    throw "No DealerFOSS.App.exe in $full. Publish first: & .\deploy\publish.ps1 -Runtime win-x64 -Output $full"
}

if (-not (Test-Path (Join-Path $full "wwwroot/index.html"))) {
    throw "$full has no wwwroot/index.html, so this would install a service that serves a blank page. " +
          "Re-publish without -SkipFrontend."
}

try { [Convert]::FromBase64String($Key) | Out-Null }
catch { throw "The key is not valid base64. Generate one with & .\deploy\new-key.ps1" }

if ([Convert]::FromBase64String($Key).Length -ne 32) {
    throw "The key must be 32 bytes. Generate one with & .\deploy\new-key.ps1"
}

if (Get-Service -Name $Name -ErrorAction SilentlyContinue) {
    throw "A service called $Name already exists. Remove it first: & .\deploy\install-service.ps1 -Uninstall"
}

# --- register ---

Write-Host "Registering $Name..." -ForegroundColor Cyan

& sc.exe create $Name binPath= "`"$exe`"" start= auto obj= "$Account" DisplayName= "DealerFOSS" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed with $LASTEXITCODE." }

& sc.exe description $Name "DealerFOSS - open-source dealer management system." | Out-Null

# Restart on failure: first two quickly, then back off. A database that is slow
# to come up after a reboot must not leave the service dead until somebody
# notices.
& sc.exe failure $Name reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null

# --- configuration, scoped to this service only ---
#
# REG_MULTI_SZ under the service's own key. Not setx: that writes machine-wide,
# and the key below decrypts every dealership's connection string.
$serviceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$Name"

$environment = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "ASPNETCORE_URLS=$Url",
    "ConnectionStrings__HostCatalog=$Connection",
    "Secrets__CurrentKeyId=$KeyId",
    "Secrets__Keys__$KeyId=$Key"
)

New-ItemProperty -Path $serviceKey -Name "Environment" `
    -PropertyType MultiString -Value $environment -Force | Out-Null

Write-Host "Starting $Name..." -ForegroundColor Cyan
Start-Service -Name $Name

$service = Get-Service -Name $Name
$service.WaitForStatus("Running", (New-TimeSpan -Seconds 30))

Write-Host ""
Write-Host "$Name is running at $Url" -ForegroundColor Green
Write-Host ""
Write-Host "Check it answered: Invoke-RestMethod $Url/health/ready" -ForegroundColor DarkGray
Write-Host "Then create the first dealership - docs/OPERATING.md." -ForegroundColor DarkGray
Write-Host ""
Write-Host "The key is stored in the service's registry entry, readable only by" -ForegroundColor Yellow
Write-Host "Administrators. Keep your own copy somewhere else: a database backup" -ForegroundColor Yellow
Write-Host "without it cannot be restored." -ForegroundColor Yellow
