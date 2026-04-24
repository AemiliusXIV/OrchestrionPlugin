# Orchestrion Release Script
# Usage: .\release.ps1 -Version 2.2.0.13
# Builds the plugin locally, publishes a GitHub release, and updates the Dalamud repo manifest.

param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$ProjectFile  = "$PSScriptRoot\Orchestrion\Orchestrion.csproj"
$ZipPath      = "$PSScriptRoot\Orchestrion\bin\Release\orchestrion\latest.zip"
$DalamudPluginsRepo = "C:\Users\tjarc\Documents\Code\DalamudPlugins"
$RepoJson     = "$DalamudPluginsRepo\repo.json"
$DotNet       = "C:\Program Files\dotnet\dotnet.exe"
$Dalamud      = "$env:AppData\XIVLauncher\addon\Hooks\dev"

Write-Host ""
Write-Host "=== Orchestrion Release ===" -ForegroundColor Cyan
Write-Host "Version: $Version"
Write-Host ""

# ── 1. Check Dalamud is installed locally ─────────────────────────────────────
if (-not (Test-Path $Dalamud)) {
    Write-Host "ERROR: Dalamud not found at $Dalamud" -ForegroundColor Red
    Write-Host "Make sure XIVLauncher has been run at least once."
    exit 1
}

# ── 2. Update version in .csproj ──────────────────────────────────────────────
Write-Host "Updating version in .csproj..." -ForegroundColor Yellow
$csproj = Get-Content $ProjectFile -Raw
$csproj = $csproj -replace '<PluginVersion>.*?</PluginVersion>', "<PluginVersion>$Version</PluginVersion>"
Set-Content $ProjectFile $csproj -Encoding UTF8
Write-Host "  Done."

# ── 3. Build ──────────────────────────────────────────────────────────────────
Write-Host "Building..." -ForegroundColor Yellow
& $DotNet build $ProjectFile --configuration Release --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed." -ForegroundColor Red
    exit 1
}
Write-Host "  Build succeeded."

# ── 4. Verify zip was produced ────────────────────────────────────────────────
if (-not (Test-Path $ZipPath)) {
    Write-Host "ERROR: latest.zip not found at $ZipPath" -ForegroundColor Red
    exit 1
}

# ── 5. Commit and push version bump in OrchestrionPlugin ─────────────────────
Write-Host "Committing version bump..." -ForegroundColor Yellow
git -C $PSScriptRoot add Orchestrion/Orchestrion.csproj
git -C $PSScriptRoot commit -m "Bump version to $Version"
git -C $PSScriptRoot push

# ── 6. Create GitHub release and upload zip ───────────────────────────────────
Write-Host "Creating GitHub release $Version..." -ForegroundColor Yellow
gh release create $Version $ZipPath `
    --repo AemiliusXIV/OrchestrionPlugin `
    --title "v$Version" `
    --notes "Release $Version"
Write-Host "  Release created."

# ── 7. Update AssemblyVersion in DalamudPlugins/repo.json ────────────────────
Write-Host "Updating DalamudPlugins/repo.json..." -ForegroundColor Yellow
if (-not (Test-Path $RepoJson)) {
    Write-Host "ERROR: repo.json not found at $RepoJson" -ForegroundColor Red
    exit 1
}
$json = Get-Content $RepoJson -Raw
$json = $json -replace '"AssemblyVersion":\s*"[^"]*"', ('"AssemblyVersion": "' + $Version + '"')
Set-Content $RepoJson $json -Encoding UTF8
Write-Host "  Updated to $Version."

# ── 8. Commit and push DalamudPlugins ─────────────────────────────────────────
Write-Host "Pushing repo.json update..." -ForegroundColor Yellow
git -C $DalamudPluginsRepo add repo.json
git -C $DalamudPluginsRepo commit -m "Update Orchestrion to $Version"
git -C $DalamudPluginsRepo push
Write-Host "  Done."

Write-Host ""
Write-Host "=== All done! ===" -ForegroundColor Green
Write-Host "Release:  https://github.com/AemiliusXIV/OrchestrionPlugin/releases/tag/$Version"
Write-Host "Manifest: https://raw.githubusercontent.com/AemiliusXIV/DalamudPlugins/main/repo.json"
Write-Host ""
