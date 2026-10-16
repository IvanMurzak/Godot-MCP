#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Initialize a new Godot-MCP extension from this template: replace placeholders, rename files/folders,
    and activate the CI workflows.

.DESCRIPTION
    From a single -Feature name (PascalCase, e.g. "Particles") this derives and substitutes every
    placeholder, then renames any file/folder whose name contains YOUR_FEATURE, then activates CI by
    renaming `.github/workflows/*.yml-sample` -> `*.yml`.

    Derived values (override any with the matching parameter):
      Package id  : com.IvanMurzak.Godot.MCP.<Feature>     (the NuGet <PackageId>)
      Repo        : IvanMurzak/Godot-AI-<Feature>          (-GitHubRepository)
      Display name: "<Feature> Tools"                       (-DisplayName)
      Description : "AI MCP tools for Godot <Feature>."      (-Description)
      Tool prefix : <feature-lowercased>                     (-ToolPrefix; tool ids "<prefix>-*")

.EXAMPLE
    ./commands/init.ps1 -Feature "Particles"

.EXAMPLE
    ./commands/init.ps1 -Feature "Particles" -GitHubRepository "myuser/Godot-AI-Particles" -DisplayName "Particle Tools"
#>
param(
    [Parameter(Mandatory = $true)] [string]$Feature,
    [Parameter(Mandatory = $false)][string]$DisplayName,
    [Parameter(Mandatory = $false)][string]$Description,
    [Parameter(Mandatory = $false)][string]$GitHubRepository,
    [Parameter(Mandatory = $false)][string]$ToolPrefix
)

$ErrorActionPreference = "Stop"

if ($Feature -notmatch '^[A-Za-z][A-Za-z0-9]*$') {
    throw "Feature must be alphanumeric PascalCase (letters/digits, starts with a letter). Got: '$Feature'"
}

if ([string]::IsNullOrWhiteSpace($DisplayName))      { $DisplayName = "$Feature Tools" }
if ([string]::IsNullOrWhiteSpace($Description))       { $Description = "AI MCP tools for Godot $Feature." }
if ([string]::IsNullOrWhiteSpace($GitHubRepository))  { $GitHubRepository = "IvanMurzak/Godot-AI-$Feature" }
if ([string]::IsNullOrWhiteSpace($ToolPrefix))        { $ToolPrefix = $Feature.ToLower() }

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir

# Longest tokens first so no token is a prefix of another mid-replacement.
$Replacements = [ordered]@{
    "YOUR_GITHUB_USERNAME_REPOSITORY" = $GitHubRepository
    "YOUR_DISPLAY_NAME"               = $DisplayName
    "YOUR_DESCRIPTION"                = $Description
    "YOUR_TOOL_PREFIX"                = $ToolPrefix
    "YOUR_FEATURE"                    = $Feature
}

Write-Host "Initializing Godot-MCP extension:" -ForegroundColor Cyan
Write-Host "  Feature      : $Feature"
Write-Host "  Package id   : com.IvanMurzak.Godot.MCP.$Feature"
Write-Host "  Repository   : $GitHubRepository"
Write-Host "  Display name : $DisplayName"
Write-Host "  Tool prefix  : $ToolPrefix-*"
Write-Host ""

$ExcludeDirs = @('.git', 'bin', 'obj', '.godot', 'local-nuget', '.vs')

function Test-Excluded([string]$path) {
    foreach ($d in $ExcludeDirs) {
        if ($path -match "[\\/]$([regex]::Escape($d))([\\/]|$)") { return $true }
    }
    return $false
}

# 1) Replace placeholder content in all text files.
Write-Host "Replacing placeholders in file content..." -ForegroundColor Yellow
$textExt = @('.cs','.csproj','.props','.targets','.md','.json','.yml','.yaml','.yml-sample','.godot','.config','.sh','.ps1','.py','.editorconfig','.gitignore','.txt')
Get-ChildItem -Path $RepoRoot -Recurse -File | ForEach-Object {
    $file = $_
    if (Test-Excluded $file.FullName) { return }
    if ($file.Name -eq 'init.ps1' -or $file.Name -eq 'init.py') { return }  # don't rewrite the running script
    $ext = $file.Extension.ToLower()
    $isText = ($textExt -contains $ext) -or ($file.Name -like '*.yml-sample') -or ($file.Name -eq 'LICENSE')
    if (-not $isText) { return }
    $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($null -eq $content) { return }
    $new = $content
    foreach ($k in $Replacements.Keys) { $new = $new.Replace($k, $Replacements[$k]) }
    if ($new -ne $content) {
        Set-Content -Path $file.FullName -Value $new -NoNewline
        Write-Host "  updated $($file.FullName.Substring($RepoRoot.Length+1))" -ForegroundColor DarkGray
    }
}

# 2) Rename files/folders whose NAME contains YOUR_FEATURE (deepest-first so children rename before parents).
Write-Host "Renaming files and folders..." -ForegroundColor Yellow
Get-ChildItem -Path $RepoRoot -Recurse | Where-Object { -not (Test-Excluded $_.FullName) } |
    Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
        if ($_.Name -like '*YOUR_FEATURE*') {
            $newName = $_.Name.Replace('YOUR_FEATURE', $Feature)
            Rename-Item -Path $_.FullName -NewName $newName
            Write-Host "  renamed $($_.Name) -> $newName" -ForegroundColor DarkGray
        }
    }

# 3) Activate CI: *.yml-sample -> *.yml
Write-Host "Activating CI workflows (*.yml-sample -> *.yml)..." -ForegroundColor Yellow
$wf = Join-Path $RepoRoot ".github/workflows"
if (Test-Path $wf) {
    Get-ChildItem -Path $wf -Filter "*.yml-sample" | ForEach-Object {
        $target = $_.FullName -replace '\.yml-sample$', '.yml'
        Move-Item -Path $_.FullName -Destination $target -Force
        Write-Host "  activated $($_.Name) -> $([System.IO.Path]::GetFileName($target))" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "Done. Next:" -ForegroundColor Green
Write-Host "  1. Review the changes, then build + test:"
Write-Host "       dotnet build src/Godot-AI-$Feature/Godot-AI-$Feature.csproj"
Write-Host "       dotnet test  tests/Godot-AI-$Feature.Tests/Godot-AI-$Feature.Tests.csproj"
Write-Host "  2. Write your tools under src/Godot-AI-$Feature/Runtime/Tools (pure-managed) and Editor/Tools (#if TOOLS)."
Write-Host "  3. Configure the NUGET_API_KEY secret, then push to main with a bumped <Version> to release."
