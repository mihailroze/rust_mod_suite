<#
  Rust Mod Suite auto installer
  by Shmatko
#>

[CmdletBinding()]
param(
    [string]$BaseDir = "C:\rust",
    [string]$ModsRoot = "C:\rust\mods",
    [string]$ServerRoot = "C:\rust\server",
    [string]$ScriptsRoot = "C:\rust\scripts",
    [switch]$SkipLocalRustSetup,
    [switch]$SkipLocalDeploy,
    [switch]$SkipServerStart,
    [switch]$SkipOpenSuite,
    [switch]$ValidateRustFiles,
    [switch]$InsecureServer
)

$ErrorActionPreference = "Stop"

Write-Host "=== Rust Mod Suite auto-install (by Shmatko) ==="

$bundledScriptsRoot = Join-Path -Path $ModsRoot -ChildPath "server-scripts"
if (-not (Test-Path -Path $ScriptsRoot) -and (Test-Path -Path $bundledScriptsRoot)) {
    $ScriptsRoot = $bundledScriptsRoot
    Write-Host "Using bundled server scripts: $ScriptsRoot"
}

$setupScript = Join-Path -Path $ScriptsRoot -ChildPath "setup-rust-test-server.ps1"
$startScript = Join-Path -Path $ScriptsRoot -ChildPath "start-test-server.ps1"
$deployScript = Join-Path -Path $ModsRoot -ChildPath "deploy-mod-suite.ps1"
$openSuiteScript = Join-Path -Path $ModsRoot -ChildPath "open-mod-suite.ps1"
$verifyScript = Join-Path -Path $ModsRoot -ChildPath "verify-local-mod-suite.ps1"

$SetupLocalRustServer = -not $SkipLocalRustSetup
$DeployPluginsToLocalServer = -not $SkipLocalDeploy
$StartLocalServer = -not $SkipServerStart
$OpenConfiguratorSuite = -not $SkipOpenSuite

if ($SetupLocalRustServer -and -not (Test-Path -Path $setupScript)) {
    throw "Local server setup script not found: $setupScript"
}
if ($StartLocalServer -and -not (Test-Path -Path $startScript)) {
    throw "Local server start script not found: $startScript"
}
if ($DeployPluginsToLocalServer -and -not (Test-Path -Path $deployScript)) {
    throw "Local deploy script not found: $deployScript"
}
if ($OpenConfiguratorSuite -and -not (Test-Path -Path $openSuiteScript)) {
    throw "Mod suite launcher not found: $openSuiteScript"
}

if ($SetupLocalRustServer) {
    Write-Host "[1/4] Preparing local Rust test server..."
    if ($ValidateRustFiles) {
        & $setupScript -BaseDir $BaseDir -Validate
    }
    else {
        & $setupScript -BaseDir $BaseDir
    }
}
else {
    Write-Host "[1/4] Skipped local Rust server setup."
}

if ($DeployPluginsToLocalServer) {
    Write-Host "[2/4] Deploying plugins/config files to local server..."
    & $deployScript -ModsRoot $ModsRoot -ServerRoot $ServerRoot
}
else {
    Write-Host "[2/4] Skipped local deploy."
}

if ($StartLocalServer) {
    Write-Host "[3/4] Starting local Rust server..."
    $alreadyRunning = Get-Process -Name "RustDedicated" -ErrorAction SilentlyContinue
    if ($alreadyRunning) {
        Write-Host "RustDedicated is already running. Skip start."
    }
    else {
        $startArgs = @(
            "-ExecutionPolicy", "Bypass",
            "-File", $startScript,
            "-ServerDir", $ServerRoot
        )
        if ($InsecureServer) {
            $startArgs += "-Insecure"
        }
        Start-Process -FilePath "powershell" -ArgumentList $startArgs -WorkingDirectory $ServerRoot | Out-Null
        Write-Host "Local server start command sent."
    }
}
else {
    Write-Host "[3/4] Skipped local Rust server start."
}

if ($OpenConfiguratorSuite) {
    Write-Host "[4/4] Opening mod suite hub..."
    & $openSuiteScript
}
else {
    Write-Host "[4/4] Skipped opening mod suite hub."
}

if (Test-Path -Path $verifyScript) {
    Write-Host ""
    Write-Host "Running quick local verification..."
    try {
        & $verifyScript -ServerRoot $ServerRoot
    }
    catch {
        Write-Warning "Verification finished with warnings/errors: $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Auto-install completed."
Write-Host "Next steps:"
Write-Host "  1) Configure plugins in the UI."
Write-Host "  2) Test everything locally."
Write-Host "  3) Deploy to production server:"
Write-Host "     powershell -ExecutionPolicy Bypass -File $ModsRoot\deploy-mod-suite-remote.ps1 -RemoteHost <host> -RemoteUser <user> -RemoteRoot <path>"
