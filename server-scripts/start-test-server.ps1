[CmdletBinding()]
param(
    [string]$ServerDir = "C:\rust\server",
    [string]$Identity = "modtest",
    [string]$HostName = "Rust Mod Test Server",
    [string]$Description = "Local server for Rust mod testing",
    [int]$Port = 28015,
    [int]$RconPort = 28016,
    [string]$RconPassword = "ChangeMe_12345",
    [int]$MaxPlayers = 5,
    [int]$WorldSize = 1000,
    [int]$Seed = 12345,
    [string]$Level = "Procedural Map",
    [switch]$Insecure
)

$ErrorActionPreference = "Stop"

if ($WorldSize -lt 1000) {
    throw "WorldSize must be at least 1000."
}

$exePath = Join-Path -Path $ServerDir -ChildPath "RustDedicated.exe"
if (-not (Test-Path -Path $exePath)) {
    throw "RustDedicated.exe not found: $exePath"
}

$identityPath = Join-Path -Path $ServerDir -ChildPath ("server\" + $Identity)
if (-not (Test-Path -Path $identityPath)) {
    New-Item -ItemType Directory -Path $identityPath -Force | Out-Null
}

$appPort = $Port + 2
$arguments = @(
    "-batchmode",
    "-nographics",
    "+server.port", $Port,
    "+server.level", $Level,
    "+server.seed", $Seed,
    "+server.worldsize", $WorldSize,
    "+server.maxplayers", $MaxPlayers,
    "+server.hostname", $HostName,
    "+server.description", $Description,
    "+server.identity", $Identity,
    "+server.saveinterval", "300",
    "+rcon.ip", "0.0.0.0",
    "+rcon.port", $RconPort,
    "+rcon.password", $RconPassword,
    "+rcon.web", "1",
    "+app.port", $appPort
)

if ($Insecure) {
    $arguments += @("+server.secure", "false")
}

Write-Host "Starting Rust test server..."
Write-Host "WorldSize: $WorldSize | Seed: $Seed | Identity: $Identity"
Write-Host "Game port: $Port | RCON port: $RconPort"

Push-Location -Path $ServerDir
try {
    & $exePath @arguments
}
finally {
    Pop-Location
}
