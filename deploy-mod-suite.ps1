<#
  Rust Mod Suite local deploy
  by Shmatko
#>

[CmdletBinding()]
param(
    [string]$ModsRoot = "C:\rust\mods",
    [string]$ServerRoot = "C:\rust\server",
    [string]$PrivilegeConfigSource = "",
    [string]$LootConfigSource = ""
)

$ErrorActionPreference = "Stop"

$pluginTargetDir = Join-Path -Path $ServerRoot -ChildPath "oxide\plugins"
$configTargetDir = Join-Path -Path $ServerRoot -ChildPath "oxide\config"

$privilegeSource = Join-Path -Path $ModsRoot -ChildPath "privilege-system\PrivilegeSystem.cs"
$lootSource = Join-Path -Path $ModsRoot -ChildPath "container-loot-manager\ContainerLootManager.cs"

if (-not (Test-Path -Path $privilegeSource)) {
    throw "Missing source plugin: $privilegeSource"
}
if (-not (Test-Path -Path $lootSource)) {
    throw "Missing source plugin: $lootSource"
}

New-Item -ItemType Directory -Path $pluginTargetDir -Force | Out-Null
New-Item -ItemType Directory -Path $configTargetDir -Force | Out-Null

$privilegeTarget = Join-Path -Path $pluginTargetDir -ChildPath "PrivilegeSystem.cs"
$lootTarget = Join-Path -Path $pluginTargetDir -ChildPath "ContainerLootManager.cs"

Copy-Item -Path $privilegeSource -Destination $privilegeTarget -Force
Copy-Item -Path $lootSource -Destination $lootTarget -Force

Write-Host "Deployed plugin: $privilegeTarget"
Write-Host "Deployed plugin: $lootTarget"

if ($PrivilegeConfigSource) {
    if (-not (Test-Path -Path $PrivilegeConfigSource)) {
        throw "Privilege config source not found: $PrivilegeConfigSource"
    }
    $privilegeCfgTarget = Join-Path -Path $configTargetDir -ChildPath "PrivilegeSystem.json"
    Copy-Item -Path $PrivilegeConfigSource -Destination $privilegeCfgTarget -Force
    Write-Host "Deployed config: $privilegeCfgTarget"
}

if ($LootConfigSource) {
    if (-not (Test-Path -Path $LootConfigSource)) {
        throw "Loot config source not found: $LootConfigSource"
    }
    $lootCfgTarget = Join-Path -Path $configTargetDir -ChildPath "ContainerLootManager.json"
    Copy-Item -Path $LootConfigSource -Destination $lootCfgTarget -Force
    Write-Host "Deployed config: $lootCfgTarget"
}

Write-Host ""
Write-Host "Next step on Rust server console:"
Write-Host "  oxide.reload ContainerLootManager"
Write-Host "  oxide.reload PrivilegeSystem"
