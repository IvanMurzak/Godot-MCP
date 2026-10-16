#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Set the extension package version (the single source of truth: the package csproj <Version>).

.DESCRIPTION
    Updates <Version> in the source-only package csproj under src/. The release workflow is version-gated
    on this value (it publishes only when <Version> increases AND the tag does not yet exist).

.EXAMPLE
    ./commands/bump-version.ps1 -NewVersion "1.0.1"
.EXAMPLE
    ./commands/bump-version.ps1 -NewVersion "1.0.1" -WhatIf
#>
param(
    [Parameter(Mandatory = $true)][string]$NewVersion,
    [switch]$WhatIf
)
$ErrorActionPreference = "Stop"

if ($NewVersion -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z\.\-]+)?$') {
    throw "Invalid semantic version: '$NewVersion' (expected major.minor.patch[-prerelease])."
}

$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$csproj = Get-ChildItem -Path (Join-Path $RepoRoot "src") -Recurse -Filter *.csproj |
    Where-Object { (Get-Content $_.FullName -Raw) -match '<PackageId>' } | Select-Object -First 1
if (-not $csproj) { throw "Could not find the package csproj (with <PackageId>) under src/." }

$content = Get-Content $csproj.FullName -Raw
if ($content -notmatch '<Version>([^<]+)</Version>') { throw "No <Version> element in $($csproj.Name)." }
$current = $Matches[1]
$new = $content -replace '<Version>[^<]+</Version>', "<Version>$NewVersion</Version>"

Write-Host "Package: $($csproj.Name)"
Write-Host "Version: $current -> $NewVersion"
if ($WhatIf) { Write-Host "(WhatIf — no changes written)" -ForegroundColor Yellow; exit 0 }
Set-Content -Path $csproj.FullName -Value $new -NoNewline
Write-Host "Updated." -ForegroundColor Green
Write-Host "Remember to commit, then push to main to trigger the version-gated release."
