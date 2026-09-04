# Copyright (c) 2026 The DealerFOSS contributors.
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Overview: Purpose, File Design, and Engineering
#   Backs up an installation: the host catalog and every tenant database.
#
#   It writes one .bak per database plus a manifest.json naming them, with a
#   SHA-256 of each file. The manifest is what restore.ps1 reads — it is not
#   decoration, and a backup folder without one cannot be restored by the script.
#
# Usage:
#   From the repository root, Windows PowerShell 5.1:
#
#   & .\deploy\backup.ps1
#   & .\deploy\backup.ps1 -HostConnection "..." -Path D:\backups
#
# Coding Instructions:
#   What this deliberately does NOT do, because doc 08 owns these and a script
#   that half-does them is worse than one that does not pretend: copy anything
#   off this host, encrypt the files, schedule itself, or expire old backups.
#
#   A .bak contains every customer record in plain form, so where these files
#   end up is a decision somebody has to make deliberately rather than inherit
#   from a default written here.

param(
    [string]$HostConnection = "Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False",
    [string]$Path = (Join-Path $PSScriptRoot "..\local\backups")
)

$ErrorActionPreference = "Stop"

function Invoke-Scalar([string]$sql, [string]$connectionString) {
    $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $sql
        return $command.ExecuteScalar()
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
        while ($reader.Read()) { $rows += $reader.GetString(0) }
        $reader.Close()
        return $rows
    } finally { $connection.Close() }
}

function Invoke-NonQuery([string]$sql, [string]$connectionString) {
    $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $sql
        # A backup of a large database takes longer than the default 30 seconds.
        $command.CommandTimeout = 0
        $null = $command.ExecuteNonQuery()
    } finally { $connection.Close() }
}

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $HostConnection
$catalog = $builder["Initial Catalog"]

# Connect to master to enumerate and to write backups: the host catalog itself
# is one of the databases being backed up.
$builder["Initial Catalog"] = "master"
$master = $builder.ConnectionString

Write-Host "Installation: $catalog" -ForegroundColor Cyan

# Tenant databases are named from the host catalog (DevelopmentSeeder.TenantDatabaseName),
# so they are discoverable without decrypting anything. A tenant whose stored
# connection points somewhere off-convention would be missed — see deploy/README.md.
$prefix = if ($catalog.EndsWith("_Host")) { $catalog.Substring(0, $catalog.Length - 5) } else { $catalog }
$tenantPattern = "${prefix}_Tenant_%"

$databases = @($catalog) + @(Invoke-Rows @"
SELECT name FROM sys.databases
WHERE name LIKE '$tenantPattern'
ORDER BY name
"@ $master)

if ($databases.Count -eq 1) {
    Write-Host "No tenant databases found matching '$tenantPattern'." -ForegroundColor Yellow
}

# Express (EngineEdition 4) cannot compress a backup, and LocalDB is Express.
# Asked rather than assumed: hard-coding either answer breaks somebody's drill on
# the edition we did not think of.
$engineEdition = [int](Invoke-Scalar "SELECT CAST(SERVERPROPERTY('EngineEdition') AS int)" $master)
$compression = if ($engineEdition -eq 4) { "" } else { ", COMPRESSION" }
if ($engineEdition -eq 4) {
    Write-Host "Express edition: backups will not be compressed." -ForegroundColor DarkGray
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$folder = Join-Path $Path $stamp
$null = New-Item -ItemType Directory -Force $folder
$folder = (Resolve-Path $folder).Path

$entries = @()
foreach ($database in $databases) {
    $exists = Invoke-Scalar "SELECT COUNT(*) FROM sys.databases WHERE name = '$database'" $master
    if ($exists -eq 0) {
        throw "Database '$database' does not exist. Is -HostConnection pointing at the right server?"
    }

    $file = Join-Path $folder "$database.bak"
    Write-Host "  backing up $database" -ForegroundColor DarkGray

    # COPY_ONLY so this cannot disturb whatever real backup chain exists. INIT
    # because the file is new every run and appending would grow it silently.
    Invoke-NonQuery @"
BACKUP DATABASE [$database] TO DISK = N'$file'
WITH COPY_ONLY, INIT, FORMAT$compression, NAME = N'DealerFOSS $database'
"@ $master

    $entries += [pscustomobject]@{
        database = $database
        file     = "$database.bak"
        bytes    = (Get-Item $file).Length
        sha256   = (Get-FileHash $file -Algorithm SHA256).Hash.ToLower()
    }
}

$manifest = [pscustomobject]@{
    takenAt        = (Get-Date).ToUniversalTime().ToString("o")
    hostCatalog    = $catalog
    server         = $builder["Data Source"]
    tenantPattern  = $tenantPattern
    databases      = $entries
}

$manifestPath = Join-Path $folder "manifest.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding UTF8

Write-Host ""
Write-Host "Backed up $($entries.Count) database(s) to $folder" -ForegroundColor Green
Write-Host "Restore it with:" -ForegroundColor DarkGray
Write-Host "  & .\deploy\restore.ps1 -From `"$folder`"" -ForegroundColor DarkGray
