[CmdletBinding()]
param(
    [string]$ServerDir = "C:\rust\server",
    [string]$DownloadUrl = "https://umod.org/games/rust/download",
    [string]$ArchivePath = "C:\rust\mods\oxide-rust.zip"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if (-not (Test-Path -Path $ServerDir)) {
    throw "Rust server directory not found: $ServerDir"
}

Write-Host "Downloading Oxide from $DownloadUrl ..."
Invoke-WebRequest -Uri $DownloadUrl -OutFile $ArchivePath

Write-Host "Installing Oxide into $ServerDir ..."
Expand-Archive -Path $ArchivePath -DestinationPath $ServerDir -Force

$oxideDll = Join-Path -Path $ServerDir -ChildPath "RustDedicated_Data\Managed\Oxide.Rust.dll"
if (-not (Test-Path -Path $oxideDll)) {
    throw "Oxide install verification failed: $oxideDll not found"
}

Write-Host "Oxide installed successfully."
