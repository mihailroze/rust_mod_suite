<#
  Rust Mod Suite launcher
  by Shmatko
#>

[CmdletBinding()]
param(
    [string]$BindHost = "127.0.0.1",
    [int]$Port = 18765,
    [string]$PrivilegeConfigPath = "C:\rust\server\oxide\config\PrivilegeSystem.json",
    [string]$LootConfigPath = "C:\rust\server\oxide\config\ContainerLootManager.json",
    [string]$PluginTargetDir = "C:\rust\server\oxide\plugins"
)

$ErrorActionPreference = "Stop"

$serverScript = "C:\rust\mods\privilege-configurator\local_server.py"
if (-not (Test-Path -Path $serverScript)) {
    throw "Configurator server script not found: $serverScript"
}

$rootDir = "C:\rust\mods"
$baseUrl = "http://$BindHost`:$Port"
$openUrl = "$baseUrl/mod-suite/index.html"
$healthUrl = "$baseUrl/health"

function Test-SuiteServer {
    param([string]$Url)
    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 1
        return $resp.StatusCode -eq 200
    } catch {
        return $false
    }
}

if (-not (Test-SuiteServer -Url $healthUrl)) {
    $pythonExe = $null
    $pythonCmd = Get-Command python -ErrorAction SilentlyContinue
    if ($pythonCmd) {
        $pythonExe = $pythonCmd.Source
    } else {
        $pyCmd = Get-Command py -ErrorAction SilentlyContinue
        if ($pyCmd) {
            $pythonExe = $pyCmd.Source
        }
    }

    if (-not $pythonExe) {
        throw "Python launcher not found (python/py). Install Python."
    }

    $args = @(
        $serverScript,
        "--host", $BindHost,
        "--port", $Port,
        "--root", $rootDir,
        "--target-privilege-config", $PrivilegeConfigPath,
        "--target-loot-config", $LootConfigPath,
        "--plugin-target-dir", $PluginTargetDir
    )

    Start-Process -FilePath $pythonExe -ArgumentList $args -WorkingDirectory $rootDir -WindowStyle Hidden | Out-Null

    $isUp = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if (Test-SuiteServer -Url $healthUrl) {
            $isUp = $true
            break
        }
    }

    if (-not $isUp) {
        throw "Failed to start suite server at $baseUrl"
    }
}

Start-Process $openUrl
Write-Host "Opened Rust Mod Suite: $openUrl"
Write-Host "Privilege config target: $PrivilegeConfigPath"
Write-Host "Loot config target: $LootConfigPath"
Write-Host "Plugin deploy dir: $PluginTargetDir"
