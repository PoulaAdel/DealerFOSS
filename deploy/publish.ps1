# Copyright (c) 2026 The DealerFOSS contributors.
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Overview: Purpose, File Design, and Engineering
#   publish.ps1 — build the thing an operator installs.
#
# Usage:
#   & .\deploy\publish.ps1
#   & .\deploy\publish.ps1 -Output D:\builds\dealerfoss -Runtime win-x64
#
# Coding Instructions:
#   The frontend build is the step that is easy to forget and impossible to
#   notice. Without it the application starts, serves the API, answers health,
#   and shows a blank page — because wwwroot is empty and the shell fallback
#   is guarded. So this script builds the frontend FIRST and refuses to
#   continue if it produced nothing, rather than publishing a package whose
#   failure only appears in a browser.
#
#   Node is not installed on the development host and does not need to be:
#   the toolchain lives in the dealerfoss-node container. The script uses
#   whichever is available and says which it used.

[CmdletBinding()]
param(
    [string]$Output = "artifacts/app",

    # win-x64 for the Windows service; linux-x64 is what the Dockerfile uses.
    # Left as the SDK default (portable, needs a runtime installed) when empty.
    [string]$Runtime = "",

    # Skips the frontend build when you have just built it yourself. Named
    # honestly: it does not skip the CHECK that wwwroot has something in it.
    [switch]$SkipFrontend
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

<#
.SYNOPSIS
Runs a native executable and judges it by its exit code.

.DESCRIPTION
Windows PowerShell 5.1 turns ANY stderr output from a native command into a
terminating NativeCommandError while $ErrorActionPreference is Stop — even when
the command succeeded and returned zero. npm writes deprecation notices to
stderr on a perfectly good install, so a strict script fails on a warning.

This caught the script out on its first run: `npm ci` printed one deprecation
line and the publish stopped. The exit code is the real signal, so the strict
setting is relaxed for the duration of the call and the code checked instead.
#>
function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)][string]$What,
        [Parameter(Mandatory = $true)][scriptblock]$Command
    )

    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $Command
        if ($LASTEXITCODE -ne 0) { throw "$What failed with exit code $LASTEXITCODE." }
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

Push-Location $root

try {
    $webRoot = Join-Path $root "src/App/wwwroot"
    $dist = Join-Path $root "frontend/dist"

    if (-not $SkipFrontend) {
        Write-Host "--- frontend ---" -ForegroundColor Cyan

        # The container is preferred because it is what CI and every contributor
        # uses; a host npm may be a different major version.
        $inContainer = $false
        $running = (docker ps --filter "name=dealerfoss-node" --format "{{.Names}}" 2>$null)
        if ($running -eq "dealerfoss-node") { $inContainer = $true }

        if ($inContainer) {
            Write-Host "building in the dealerfoss-node container"
            Invoke-Native "The frontend build" {
                docker exec dealerfoss-node sh -c "cd /workspace/frontend && npm ci --no-audit --no-fund && npm run build"
            }
        }
        elseif (Get-Command npm -ErrorAction SilentlyContinue) {
            Write-Host "building with the host's npm"
            Push-Location (Join-Path $root "frontend")
            try {
                Invoke-Native "npm ci" { npm ci --no-audit --no-fund }
                Invoke-Native "The frontend build" { npm run build }
            }
            finally { Pop-Location }
        }
        else {
            throw "No way to build the frontend. Start the toolchain container with " +
                  "'docker compose -f deploy/docker-compose.yml --profile node up -d', " +
                  "or install Node 22, or pass -SkipFrontend if src/App/wwwroot is already current."
        }
    }

    # The check runs whether or not the build was skipped. A package with an empty
    # wwwroot is the failure this script exists to prevent.
    if (-not $SkipFrontend) {
        if (-not (Test-Path (Join-Path $dist "index.html"))) {
            throw "The frontend build produced no index.html at $dist."
        }

        Write-Host "--- copying the frontend into wwwroot ---" -ForegroundColor Cyan
        if (Test-Path $webRoot) { Remove-Item $webRoot -Recurse -Force }
        New-Item -ItemType Directory -Path $webRoot -Force | Out-Null
        Copy-Item (Join-Path $dist "*") $webRoot -Recurse -Force
    }

    if (-not (Test-Path (Join-Path $webRoot "index.html"))) {
        throw "src/App/wwwroot has no index.html, so this package would serve a blank page. " +
              "Run without -SkipFrontend."
    }

    Write-Host "--- application ---" -ForegroundColor Cyan

    if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }

    $publish = @(
        "publish", "src/App/App.csproj",
        "-c", "Release",
        "-o", $Output,
        "--nologo"
    )

    if ($Runtime) {
        # Self-contained so the target needs no .NET installed. That is the whole
        # point of a package: an operator should not have to get a runtime version
        # right before they can start.
        $publish += @("-r", $Runtime, "--self-contained", "true")
    }

    Invoke-Native "dotnet publish" { dotnet @publish }

    $shell = Join-Path $Output "wwwroot/index.html"
    if (-not (Test-Path $shell)) {
        throw "The published output has no wwwroot/index.html. The package would serve a blank page."
    }

    $size = [math]::Round(((Get-ChildItem $Output -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB), 1)

    Write-Host ""
    Write-Host "Published to $Output  ($size MB)" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next, on the machine this is going to:" -ForegroundColor DarkGray
    Write-Host "  1. Generate a key      : & .\deploy\new-key.ps1" -ForegroundColor DarkGray
    Write-Host "  2. Install the service : & .\deploy\install-service.ps1 -Path <this folder> ..." -ForegroundColor DarkGray
    Write-Host "  3. Read                : docs/OPERATING.md" -ForegroundColor DarkGray
}
finally {
    Pop-Location
}
