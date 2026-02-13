[CmdletBinding()]
param(
    [string]$SteamCmdPath = "C:\rust\steamcmd\steamcmd.exe",
    [string]$InstallDir = "C:\rust\server",
    [switch]$Validate
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $SteamCmdPath)) {
    throw "SteamCMD not found: $SteamCmdPath"
}

if (-not (Test-Path -Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

$updateArgs = @(
    "+force_install_dir", $InstallDir,
    "+login", "anonymous",
    "+app_update", "258550"
)

if ($Validate) {
    $updateArgs += "validate"
}

$updateArgs += "+quit"

Write-Host "Updating Rust server in $InstallDir ..."
& $SteamCmdPath @updateArgs

if ($LASTEXITCODE -ne 0) {
    throw "SteamCMD failed with exit code $LASTEXITCODE"
}

Write-Host "Rust server update completed."
