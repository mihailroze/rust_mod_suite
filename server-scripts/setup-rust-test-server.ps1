[CmdletBinding()]
param(
    [string]$BaseDir = "C:\rust",
    [switch]$Validate
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$steamCmdDir = Join-Path -Path $BaseDir -ChildPath "steamcmd"
$serverDir = Join-Path -Path $BaseDir -ChildPath "server"
$modsDir = Join-Path -Path $BaseDir -ChildPath "mods"
$steamCmdExe = Join-Path -Path $steamCmdDir -ChildPath "steamcmd.exe"
$steamZip = Join-Path -Path $steamCmdDir -ChildPath "steamcmd.zip"

New-Item -ItemType Directory -Path $steamCmdDir -Force | Out-Null
New-Item -ItemType Directory -Path $serverDir -Force | Out-Null
New-Item -ItemType Directory -Path $modsDir -Force | Out-Null

if (-not (Test-Path -Path $steamCmdExe)) {
    Write-Host "Downloading SteamCMD ..."
    Invoke-WebRequest -Uri "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip" -OutFile $steamZip
    Expand-Archive -Path $steamZip -DestinationPath $steamCmdDir -Force
}

Write-Host "Updating Rust server files ..."
$updateScript = Join-Path -Path $PSScriptRoot -ChildPath "update-rust-server.ps1"
if ($Validate) {
    & $updateScript -SteamCmdPath $steamCmdExe -InstallDir $serverDir -Validate
}
else {
    & $updateScript -SteamCmdPath $steamCmdExe -InstallDir $serverDir
}

Write-Host "Installing Oxide ..."
$oxideScript = Join-Path -Path $PSScriptRoot -ChildPath "install-oxide.ps1"
$oxideArchive = Join-Path -Path $modsDir -ChildPath "oxide-rust.zip"
& $oxideScript -ServerDir $serverDir -ArchivePath $oxideArchive

Write-Host "Setup complete."
Write-Host "Run the server with:"
Write-Host "powershell -ExecutionPolicy Bypass -File $PSScriptRoot\start-test-server.ps1"
