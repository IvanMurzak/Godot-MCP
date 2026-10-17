#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Update the package's MCP / reflection / query floors (com.IvanMurzak.McpPlugin / com.IvanMurzak.ReflectorNet /
    com.IvanMurzak.Rql) in its package and test csproj files.

.DESCRIPTION
    The package declares MIN-versions (floors) the core Godot-MCP addon (Godot-MCP/Godot-MCP.csproj) satisfies;
    com.IvanMurzak.Rql is the RQL module API. When the floors must advance, run this so the extension declares
    them in both. The testbed is a consumer that pins the core addon's exact versions; it is not edited here.
    Pass any of the three.

.EXAMPLE
    ./commands/update-core.ps1 -McpPlugin "6.11.0"
.EXAMPLE
    ./commands/update-core.ps1 -McpPlugin "6.11.0" -ReflectorNet "5.4.0" -WhatIf
.EXAMPLE
    ./commands/update-core.ps1 -Rql "0.2.0"
#>
param(
    [string]$McpPlugin,
    [string]$ReflectorNet,
    [string]$Rql,
    [switch]$WhatIf
)
$ErrorActionPreference = "Stop"
if (-not $McpPlugin -and -not $ReflectorNet -and -not $Rql) { throw "Pass -McpPlugin, -ReflectorNet and/or -Rql." }

$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$targets = @('src', 'tests') | ForEach-Object {
    $p = Join-Path $RepoRoot $_
    if (Test-Path $p) { Get-ChildItem -Path $p -Recurse -Filter *.csproj }
}

$pins = @{}
if ($McpPlugin)    { $pins['com.IvanMurzak.McpPlugin']  = $McpPlugin }
if ($ReflectorNet) { $pins['com.IvanMurzak.ReflectorNet'] = $ReflectorNet }
if ($Rql)          { $pins['com.IvanMurzak.Rql']          = $Rql }

foreach ($csproj in $targets) {
    $content = Get-Content $csproj.FullName -Raw
    $new = $content
    foreach ($id in $pins.Keys) {
        $pattern = '(<PackageReference\s+Include="' + [regex]::Escape($id) + '"\s+Version=")[^"]+(")'
        $new = [regex]::Replace($new, $pattern, "`${1}$($pins[$id])`${2}")
    }
    if ($new -ne $content) {
        Write-Host "Updating $($csproj.FullName.Substring($RepoRoot.Length+1))"
        foreach ($id in $pins.Keys) { Write-Host "  $id -> $($pins[$id])" }
        if (-not $WhatIf) { Set-Content -Path $csproj.FullName -Value $new -NoNewline }
    }
}
if ($WhatIf) { Write-Host "(WhatIf — no changes written)" -ForegroundColor Yellow }
else { Write-Host "Done. Rebuild + test to confirm compatibility." -ForegroundColor Green }
