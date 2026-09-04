# Copyright (c) 2026 The DealerFOSS contributors.
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Overview: Purpose, File Design, and Engineering
#   Restores an installation from a backup folder, alongside the original.
#
#   Everything is restored under a prefix, so the drill can be run on a machine
#   that is already serving the originals without touching them. That is also the
#   only way to *prove* a restore: a copy that overwrote the original tells you
#   nothing about whether the backup was any good.
#
# Usage:
#   From the repository root, Windows PowerShell 5.1:
#
#   & .\deploy\restore.ps1 -From .\local\backups\20260804-093000
#   & .\deploy\restore.ps1 -From ... -Prefix Restored_ -Verify
#
# Coding Instructions:
#   THE STEP PEOPLE FORGET IS THE LAST ONE. Each tenant's connection string
#   lives in the host catalog encrypted, so a freshly restored catalog still
#   names the ORIGINAL databases — a "restored" installation would quietly read
#   and write the live ones.
#
#   Only something holding the deployment's keys can rewrite them, so this
#   script calls the application to do it rather than being handed the keys
#   itself. Do not "simplify" that into decrypting here: it would put the key
#   that opens every dealership into a shell script's memory and its history.

param(
    [Parameter(Mandatory = $true)][string]$From,
    [string]$Prefix = "Restored_",
    [string]$Server,
    [switch]$Verify
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Invoke-NonQuery([string]$sql, [string]$connectionString, [int]$timeout = 0) {
    $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $sql
        $command.CommandTimeout = $timeout
        $null = $command.ExecuteNonQuery()
    } finally { $connection.Close() }
}

function Invoke-Rows([string]$sql, [string]$connectionString) {
    $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $sql
        $reader = $command.ExecuteReader()
        $rows = @()
        while ($reader.Read()) {
            $row = @{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) { $row[$reader.GetName($i)] = $reader.GetValue($i) }
            $rows += [pscustomobject]$row
        }
        $reader.Close()
        return $rows
    } finally { $connection.Close() }
}

$folder = (Resolve-Path $From).Path
$manifestPath = Join-Path $folder "manifest.json"
if (-not (Test-Path $manifestPath)) {
    throw "No manifest.json in $folder. That folder was not written by backup.ps1."
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$targetServer = if ($Server) { $Server } else { $manifest.server }

Write-Host "Restoring $($manifest.hostCatalog) taken at $($manifest.takenAt)" -ForegroundColor Cyan
Write-Host "  onto $targetServer, prefixed '$Prefix'" -ForegroundColor DarkGray

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder["Data Source"] = $targetServer
$builder["Initial Catalog"] = "master"
$builder["Integrated Security"] = $true
$builder["TrustServerCertificate"] = $true
$builder["Encrypt"] = $false
$master = $builder.ConnectionString

# --- 1. Every file is checked before anything is restored. A backup that was
#        truncated in transit is worse than one that failed, because it looks
#        like data until the day you need it. ---
foreach ($entry in $manifest.databases) {
    $file = Join-Path $folder $entry.file
    if (-not (Test-Path $file)) { throw "Missing backup file: $($entry.file)" }

    $actual = (Get-FileHash $file -Algorithm SHA256).Hash.ToLower()
    if ($actual -ne $entry.sha256) {
        throw "$($entry.file) does not match its checksum. This backup is damaged; do not restore it."
    }
}
Write-Host "  all $($manifest.databases.Count) file(s) match their checksums" -ForegroundColor DarkGray

# --- 2. Restore each one under the prefix, relocating its files. ---
foreach ($entry in $manifest.databases) {
    $file = Join-Path $folder $entry.file
    $target = "$Prefix$($entry.database)"

    # Each logical file inside the backup needs its own MOVE, or the restore
    # tries to write over the original database's files and fails.
    $files = Invoke-Rows "RESTORE FILELISTONLY FROM DISK = N'$file'" $master
    $dataRoot = Invoke-Rows "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)) AS p" $master
    $root = $dataRoot[0].p

    $moves = @()
    foreach ($logical in $files) {
        $extension = if ($logical.Type -eq "L") { "_log.ldf" } else { ".mdf" }
        $moves += "MOVE N'$($logical.LogicalName)' TO N'$root$target$($logical.LogicalName)$extension'"
    }

    Write-Host "  restoring $target" -ForegroundColor DarkGray

    # Dropped first so the drill is repeatable: a second run must not fail
    # because the first one already made this database.
    Invoke-NonQuery @"
IF DB_ID('$target') IS NOT NULL
BEGIN
    ALTER DATABASE [$target] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$target];
END
"@ $master

    Invoke-NonQuery "RESTORE DATABASE [$target] FROM DISK = N'$file' WITH $($moves -join ', '), RECOVERY" $master
}

# --- 3. Re-point. Without this the restored catalog still names the originals. ---
$restoredHost = "$Prefix$($manifest.hostCatalog)"
$builder["Initial Catalog"] = $restoredHost
$restoredConnection = $builder.ConnectionString

Write-Host ""
Write-Host "Re-pointing the restored catalog at the restored tenants..." -ForegroundColor Cyan

$env:ConnectionStrings__HostCatalog = $restoredConnection
$serverArgument = if ($Server) { @("--server", $Server) } else { @() }

Push-Location $repoRoot
try {
    & dotnet run --project src/App -c Release --no-build -- `
        --repoint-tenants --prefix $Prefix @serverArgument
    if ($LASTEXITCODE -ne 0) { throw "Re-pointing failed. The restored catalog still names the original databases." }
} finally {
    Pop-Location
    Remove-Item Env:\ConnectionStrings__HostCatalog -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Restored to $restoredHost" -ForegroundColor Green

# --- 4. Prove it works. ---
if ($Verify) {
    Write-Host ""
    Write-Host "Checking the restored databases actually contain the data..." -ForegroundColor Cyan

    # This guard exists because the end-to-end check seeds what it does not find.
    # Without it, a restore that produced empty databases would be seeded from
    # scratch and pass — proving the application works and the backup does not.
    # Counting rows is not sufficient proof, but it is necessary.
    $builder["Initial Catalog"] = $restoredHost
    $tenantRows = Invoke-Rows "SELECT Slug FROM dbo.Tenants" $builder.ConnectionString
    if ($tenantRows.Count -eq 0) {
        throw "The restored catalog has no tenants. The backup was empty; this is not a restore."
    }

    foreach ($row in $tenantRows) {
        $builder["Initial Catalog"] = "$Prefix$($manifest.hostCatalog -replace '_Host$', '')_Tenant_$($row.Slug)"
        $counts = Invoke-Rows @"
SELECT
  (SELECT COUNT(*) FROM [org].[Rooftops]) AS rooftops,
  (SELECT COUNT(*) FROM [vehicles].[Vehicles]) AS vehicles,
  (SELECT COUNT(*) FROM [identity].[Users]) AS users
"@ $builder.ConnectionString

        $c = $counts[0]
        Write-Host "  $($row.Slug): $($c.rooftops) rooftop(s), $($c.vehicles) vehicle(s), $($c.users) user(s)" -ForegroundColor DarkGray

        if ($c.rooftops -eq 0 -or $c.users -eq 0) {
            throw "$($row.Slug) restored empty. A backup that restores to nothing is not a backup."
        }
    }

    Write-Host ""
    Write-Host "Verifying the restored copy by running the full end-to-end check against it..." -ForegroundColor Cyan

    Push-Location $repoRoot
    try {
        & .\deploy\verify-e2e.ps1 -HostConnection $restoredConnection -Port 5099
        if ($LASTEXITCODE -ne 0) { throw "The restored copy did not pass. This backup is not a backup." }
    } finally { Pop-Location }

    Write-Host ""
    Write-Host "The restored installation works." -ForegroundColor Green
} else {
    Write-Host "Pass -Verify to prove it actually works, which is the whole point of a drill." -ForegroundColor DarkGray
}
