[CmdletBinding()]
param(
    [string]$SourceFile = "C:\rust\mods\container-loot-manager\ContainerLootManager.cs",
    [string]$TargetDir = "C:\rust\server\oxide\plugins"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $SourceFile)) {
    throw "Source plugin file not found: $SourceFile"
}

if (-not (Test-Path -Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
}

$targetFile = Join-Path -Path $TargetDir -ChildPath "ContainerLootManager.cs"
Copy-Item -Path $SourceFile -Destination $targetFile -Force

Write-Host "Plugin deployed: $targetFile"
