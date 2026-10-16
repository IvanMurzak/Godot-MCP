#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Update the core MCP/reflection pins (com.IvanMurzak.McpPlugin / com.IvanMurzak.ReflectorNet) in
    lockstep across the package, tests, and testbed csproj files.

.DESCRIPTION
    These pins MUST match the core Godot-MCP addon (Godot-MCP/Godot-MCP.csproj). When the addon advances
    its pins, run this so the extension declares compatible MIN-versions everywhere. Pass either or both.

.EXAMPLE
    ./commands/update-core.ps1 -McpPlugin "6.11.0"
.EXAMPLE
    ./commands/update-core.ps1 -McpPlugin "6.11.0" -ReflectorNet "5.4.0" -WhatIf
#>
param(
    [string]$McpPlugin,
    [string]$ReflectorNet,
    [switch]$WhatIf
)
$ErrorActionPreference = "Stop"
if (-not $McpPlugin -and -not $ReflectorNet) { throw "Pass -McpPlugin and/or -ReflectorNet." }

$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$targets = @('src', 'tests', 'testbed') | ForEach-Object {
    $p = Join-Path $RepoRoot $_
    if (Test-Path $p) { Get-ChildItem -Path $p -Recurse -Filter *.csproj }
}

$pins = @{}
if ($McpPlugin)    { $pins['com.IvanMurzak.McpPlugin']  = $McpPlugin }
if ($ReflectorNet) { $pins['com.IvanMurzak.ReflectorNet'] = $ReflectorNet }

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
