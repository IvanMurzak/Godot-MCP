#!/usr/bin/env pwsh
# Print the extension package version (the package csproj <Version>). Used by CI's version gate.
$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$csproj = Get-ChildItem -Path (Join-Path $RepoRoot "src") -Recurse -Filter *.csproj |
    Where-Object { (Get-Content $_.FullName -Raw) -match '<PackageId>' } | Select-Object -First 1
if (-not $csproj) { Write-Error "Could not find the package csproj under src/."; exit 1 }
if ((Get-Content $csproj.FullName -Raw) -match '<Version>([^<]+)</Version>') { Write-Output $Matches[1]; exit 0 }
Write-Error "No <Version> in $($csproj.Name)."; exit 1
