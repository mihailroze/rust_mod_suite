[CmdletBinding()]
param(
    [string]$OutDir = "",
    [string]$SourceBaseUrl = "https://wiki.rustclash.com/img/screenshots"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path -Path $PSScriptRoot -ChildPath "assets\container-icons"
}

$files = @(
    "barrel.png",
    "oil-barrel.png",
    "crate.png",
    "elite-crate.png",
    "military-crate.png",
    "tool-crate.png",
    "food-crate.png",
    "medical-crate.png",
    "foodbox.png",
    "supply-drop.png",
    "bradley-crate.png",
    "roadsign.png",
    "vehicle-parts.png"
)

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

Write-Host "Syncing container icons to: $OutDir"
foreach ($fileName in $files) {
    $url = "$SourceBaseUrl/$fileName"
    $target = Join-Path -Path $OutDir -ChildPath $fileName
    Invoke-WebRequest -Uri $url -OutFile $target
    Write-Host "Downloaded: $fileName"
}

Write-Host "Done."
