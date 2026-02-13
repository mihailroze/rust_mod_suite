[CmdletBinding()]
param(
    [string]$ServerCatalogPath = "C:\rust\server\oxide\data\ContainerLootCatalog.json",
    [string]$TargetJsPath = "C:\rust\mods\loot-configurator\catalog-embedded.js"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $ServerCatalogPath)) {
    throw "Server catalog not found: $ServerCatalogPath`nRun /lootcfg exportcatalog on server first."
}

$raw = Get-Content -Path $ServerCatalogPath -Raw
$js = "window.DEFAULT_LOOT_CATALOG = " + $raw + ";"
Set-Content -Path $TargetJsPath -Value $js -Encoding UTF8

Write-Host "Embedded catalog updated: $TargetJsPath"
