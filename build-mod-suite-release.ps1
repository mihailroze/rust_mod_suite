<#
  Rust Mod Suite release builder
  by Shmatko
#>

[CmdletBinding()]
param(
    [string]$ModsRoot = "C:\rust\mods",
    [string]$ScriptsRoot = "C:\rust\scripts",
    [string]$OutDir = "C:\rust\mods\releases"
)

$ErrorActionPreference = "Stop"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$stageDir = Join-Path -Path $OutDir -ChildPath "rust-mod-suite-$timestamp"
$zipPath = "$stageDir.zip"

if (Test-Path -Path $stageDir) {
    Remove-Item -Path $stageDir -Recurse -Force
}

New-Item -ItemType Directory -Path $stageDir -Force | Out-Null

$includeDirs = @(
    "privilege-system",
    "container-loot-manager",
    "privilege-configurator",
    "loot-configurator",
    "mod-suite"
)

foreach ($name in $includeDirs) {
    $source = Join-Path -Path $ModsRoot -ChildPath $name
    if (-not (Test-Path -Path $source)) {
        throw "Missing directory: $source"
    }
    Copy-Item -Path $source -Destination (Join-Path -Path $stageDir -ChildPath $name) -Recurse -Force
}

$serverScriptsTarget = Join-Path -Path $stageDir -ChildPath "server-scripts"
if (Test-Path -Path $ScriptsRoot) {
    New-Item -ItemType Directory -Path $serverScriptsTarget -Force | Out-Null
    Copy-Item -Path (Join-Path -Path $ScriptsRoot -ChildPath "*.ps1") -Destination $serverScriptsTarget -Force
}
else {
    Write-Warning "Scripts root not found, skipping server scripts: $ScriptsRoot"
}

$rootFiles = @(
    "open-mod-suite.ps1",
    "deploy-mod-suite.ps1",
    "deploy-mod-suite-remote.ps1",
    "install-mod-suite.ps1",
    "verify-local-mod-suite.ps1",
    "MOD-SUITE-GUIDE.md",
    "README-MOD-SUITE-RU.md",
    "FORUM-ANNOUNCE-RU.md",
    "README-RUST-SERVER.md"
)

foreach ($fileName in $rootFiles) {
    $source = Join-Path -Path $ModsRoot -ChildPath $fileName
    if (-not (Test-Path -Path $source)) {
        throw "Missing file: $source"
    }
    Copy-Item -Path $source -Destination (Join-Path -Path $stageDir -ChildPath $fileName) -Force
}

# Keep package focused on server scripts + configurators + plugins without extra storefront docs.
Remove-Item -Path (Join-Path -Path $stageDir -ChildPath "privilege-system\README.md") -Force -ErrorAction SilentlyContinue

Get-ChildItem -Path $stageDir -Recurse -Filter "__pycache__" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path $stageDir -Recurse -Filter "*.pyc" | Remove-Item -Force -ErrorAction SilentlyContinue

if (Test-Path -Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

Compress-Archive -Path (Join-Path -Path $stageDir -ChildPath "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Release folder: $stageDir"
Write-Host "Release archive: $zipPath"
