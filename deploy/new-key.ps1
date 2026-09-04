# Copyright (c) 2026 The DealerFOSS contributors.
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Overview: Purpose, File Design, and Engineering
#   new-key.ps1 — generate a secret-protection key.
#
# Usage:
#   & .\deploy\new-key.ps1
#   & .\deploy\new-key.ps1 -KeyId 2026-10 -Format env
#
# Coding Instructions:
#   This exists as a script rather than as a snippet in the README because it
#   is the one step of an installation that cannot be repeated. The key
#   encrypts every tenant's connection string. Lose it and the databases are
#   still there and still unreadable — a backup without the key is not a
#   backup, and nobody discovers that until the day they need it.
#
#   So the script prints the warning next to the key, every time, rather
#   than trusting that whoever ran it also read the manual.
#
#   Nothing is written to disk here on purpose. A key in a file in the
#   repository folder is a key in a backup, in a screen share, and
#   eventually in a commit.

[CmdletBinding()]
param(
    # Names this key so a later one can be added beside it without a re-encrypt.
    # Dated by convention, because "which key is current" is a question somebody
    # asks eighteen months later.
    [string]$KeyId = (Get-Date -Format "yyyy-MM"),

    # text  — a labelled block to read and store in a password manager
    # env   — the two environment variables, ready to paste
    # json  — an appsettings fragment
    [ValidateSet("text", "env", "json")]
    [string]$Format = "text"
)

$ErrorActionPreference = "Stop"

# Create() and GetBytes(), NOT RandomNumberGenerator::Fill.
#
# Fill takes a Span<byte> and exists only on .NET Core 2.1 and later. Windows
# PowerShell 5.1 runs on .NET Framework, where the method is simply not there —
# so the obvious one-liner dies with "does not contain a method named 'Fill'" on
# the very shell an operator is most likely to be using. This form works on both.
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $bytes = New-Object byte[] 32
    $rng.GetBytes($bytes)
}
finally {
    $rng.Dispose()
}

$key = [Convert]::ToBase64String($bytes)

# Cleared as soon as it has been encoded. It survives in $key either way — this
# is tidiness, not a security control, and pretending otherwise would be worse
# than not doing it.
[Array]::Clear($bytes, 0, $bytes.Length)

switch ($Format) {
    "env" {
        "Secrets__CurrentKeyId=$KeyId"
        "Secrets__Keys__$KeyId=$key"
    }
    "json" {
        @"
"Secrets": {
  "CurrentKeyId": "$KeyId",
  "Keys": {
    "$KeyId": "$key"
  }
}
"@
    }
    default {
        ""
        "  Key id : $KeyId"
        "  Key    : $key"
        ""
    }
}

Write-Host ""
Write-Host "Back this up somewhere other than the server." -ForegroundColor Yellow
Write-Host "A database backup without this key cannot be restored: every tenant's" -ForegroundColor Yellow
Write-Host "connection string is encrypted with it. Put it in a password manager" -ForegroundColor Yellow
Write-Host "now, before you finish the installation." -ForegroundColor Yellow
Write-Host ""
Write-Host "Rotating later means ADDING a key beside this one and pointing" -ForegroundColor DarkGray
Write-Host "CurrentKeyId at the new one. Do not delete this one - nothing" -ForegroundColor DarkGray
Write-Host "re-encrypts existing values yet. See deploy/README.md." -ForegroundColor DarkGray
