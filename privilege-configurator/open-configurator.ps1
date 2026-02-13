[CmdletBinding()]
param(
    [string]$BindHost = "127.0.0.1",
    [int]$Port = 18765,
    [string]$TargetConfigPath = "C:\rust\server\oxide\config\PrivilegeSystem.json",
    [string]$LootConfigPath = "C:\rust\server\oxide\config\ContainerLootManager.json"
)

$ErrorActionPreference = "Stop"

$serverScript = Join-Path -Path $PSScriptRoot -ChildPath "local_server.py"
if (-not (Test-Path -Path $serverScript)) {
    throw "Configurator server script not found: $serverScript"
}

$rootDir = Split-Path -Path $PSScriptRoot -Parent
$baseUrl = "http://$BindHost`:$Port"
$openUrl = "$baseUrl/privilege-configurator/index.html"
$healthUrl = "$baseUrl/health"

function Test-ConfiguratorServer {
    param([string]$Url)
    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 1
        return $resp.StatusCode -eq 200
    } catch {
        return $false
    }
}

if (-not (Test-ConfiguratorServer -Url $healthUrl)) {
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
        throw "Python launcher not found (python/py). Install Python or run configurator without auto-save."
    }

    $args = @(
        $serverScript,
        "--host", $BindHost,
        "--port", $Port,
        "--root", $rootDir,
        "--target-privilege-config", $TargetConfigPath,
        "--target-loot-config", $LootConfigPath
    )

    Start-Process -FilePath $pythonExe -ArgumentList $args -WorkingDirectory $rootDir -WindowStyle Hidden | Out-Null

    $isUp = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if (Test-ConfiguratorServer -Url $healthUrl) {
            $isUp = $true
            break
        }
    }

    if (-not $isUp) {
        throw "Failed to start configurator server at $baseUrl"
    }
}

Start-Process $openUrl
Write-Host "Opened configurator: $openUrl"
Write-Host "Auto-save target: $TargetConfigPath"
