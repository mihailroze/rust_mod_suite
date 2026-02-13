<#
  Rust Mod Suite local verifier
  by Shmatko
#>

[CmdletBinding()]
param(
    [string]$BaseUrl = "http://127.0.0.1:18765",
    [string]$ServerRoot = "C:\rust\server",
    [switch]$RequireServerProcess
)

$ErrorActionPreference = "Stop"

$checks = New-Object System.Collections.Generic.List[object]

function Add-Check {
    param(
        [string]$Name,
        [bool]$Ok,
        [string]$Details
    )
    $checks.Add([pscustomobject]@{
        Name = $Name
        Ok = $Ok
        Details = $Details
    }) | Out-Null
}

try {
    $health = Invoke-RestMethod -Method Get -Uri "$BaseUrl/health" -TimeoutSec 2
    Add-Check -Name "Configurator API health" -Ok ($health.ok -eq $true) -Details "Endpoint: $BaseUrl/health"
}
catch {
    Add-Check -Name "Configurator API health" -Ok $false -Details $_.Exception.Message
}

$pluginDir = Join-Path -Path $ServerRoot -ChildPath "oxide\plugins"
$configDir = Join-Path -Path $ServerRoot -ChildPath "oxide\config"

$privPlugin = Join-Path -Path $pluginDir -ChildPath "PrivilegeSystem.cs"
$lootPlugin = Join-Path -Path $pluginDir -ChildPath "ContainerLootManager.cs"
$privConfig = Join-Path -Path $configDir -ChildPath "PrivilegeSystem.json"
$lootConfig = Join-Path -Path $configDir -ChildPath "ContainerLootManager.json"

Add-Check -Name "Privilege plugin deployed" -Ok (Test-Path -Path $privPlugin) -Details $privPlugin
Add-Check -Name "Loot plugin deployed" -Ok (Test-Path -Path $lootPlugin) -Details $lootPlugin
Add-Check -Name "Privilege config exists" -Ok (Test-Path -Path $privConfig) -Details $privConfig
Add-Check -Name "Loot config exists" -Ok (Test-Path -Path $lootConfig) -Details $lootConfig

$rustProc = Get-Process -Name "RustDedicated" -ErrorAction SilentlyContinue
if ($RequireServerProcess) {
    Add-Check -Name "Local Rust server process" -Ok ([bool]$rustProc) -Details "Process: RustDedicated"
}
else {
    Add-Check -Name "Local Rust server process (optional)" -Ok $true -Details ($(if ($rustProc) { "running" } else { "not running" }))
}

$oxideLog = Get-ChildItem -Path (Join-Path -Path $ServerRoot -ChildPath "oxide\logs") -Filter "oxide_*.txt" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($oxideLog) {
    $logTail = Get-Content -Path $oxideLog.FullName -Tail 400
    $hasPrivilege = $false
    $hasLoot = $false
    foreach ($line in $logTail) {
        if ($line -like "*Loaded plugin PrivilegeSystem*") { $hasPrivilege = $true }
        if ($line -like "*Loaded plugin ContainerLootManager*") { $hasLoot = $true }
    }
    Add-Check -Name "PrivilegeSystem loaded in Oxide log" -Ok $hasPrivilege -Details $oxideLog.FullName
    Add-Check -Name "ContainerLootManager loaded in Oxide log" -Ok $hasLoot -Details $oxideLog.FullName
}
else {
    Add-Check -Name "Oxide log check" -Ok $false -Details "No oxide_*.txt log files found."
}

Write-Host "=== Local verification report (by Shmatko) ==="
foreach ($row in $checks) {
    $prefix = if ($row.Ok) { "[OK]" } else { "[FAIL]" }
    Write-Host "$prefix $($row.Name) :: $($row.Details)"
}

$failed = $checks | Where-Object { -not $_.Ok }
if ($failed.Count -gt 0) {
    throw "Verification failed: $($failed.Count) check(s) are not OK."
}

Write-Host "All checks passed."
