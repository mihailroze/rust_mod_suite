[CmdletBinding()]
param(
    [int]$ProcessId
)

$ErrorActionPreference = "Stop"

if ($ProcessId -gt 0) {
    Stop-Process -Id $ProcessId -Force
    Write-Host "Stopped RustDedicated process: $ProcessId"
    exit 0
}

$rustProcesses = Get-Process -Name "RustDedicated" -ErrorAction SilentlyContinue
if (-not $rustProcesses) {
    Write-Host "No RustDedicated process is running."
    exit 0
}

$rustProcesses | Stop-Process -Force
Write-Host "Stopped RustDedicated processes: $($rustProcesses.Id -join ', ')"
