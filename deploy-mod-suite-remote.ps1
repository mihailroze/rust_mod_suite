<#
  Rust Mod Suite remote deploy (SSH/SCP)
  by Shmatko
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RemoteHost,
    [Parameter(Mandatory = $true)]
    [string]$RemoteUser,
    [int]$RemotePort = 22,
    [string]$RemoteRoot = "/home/rustserver/serverfiles",
    [string]$LocalModsRoot = "C:\rust\mods",
    [string]$LocalServerRoot = "C:\rust\server",
    [string]$PrivilegePluginSource = "",
    [string]$LootPluginSource = "",
    [string]$PrivilegeConfigSource = "",
    [string]$LootConfigSource = "",
    [string]$IdentityFile = "",
    [switch]$SkipPlugins,
    [switch]$SkipConfigs,
    [switch]$CreateBackups,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$sshCmd = Get-Command ssh.exe -ErrorAction SilentlyContinue
$scpCmd = Get-Command scp.exe -ErrorAction SilentlyContinue
if (-not $sshCmd -or -not $scpCmd) {
    throw "OpenSSH tools not found (ssh.exe/scp.exe). Install OpenSSH client."
}

if ([string]::IsNullOrWhiteSpace($PrivilegePluginSource)) {
    $PrivilegePluginSource = Join-Path -Path $LocalModsRoot -ChildPath "privilege-system\PrivilegeSystem.cs"
}
if ([string]::IsNullOrWhiteSpace($LootPluginSource)) {
    $LootPluginSource = Join-Path -Path $LocalModsRoot -ChildPath "container-loot-manager\ContainerLootManager.cs"
}
if ([string]::IsNullOrWhiteSpace($PrivilegeConfigSource)) {
    $PrivilegeConfigSource = Join-Path -Path $LocalServerRoot -ChildPath "oxide\config\PrivilegeSystem.json"
}
if ([string]::IsNullOrWhiteSpace($LootConfigSource)) {
    $LootConfigSource = Join-Path -Path $LocalServerRoot -ChildPath "oxide\config\ContainerLootManager.json"
}

$remotePluginDir = "$RemoteRoot/oxide/plugins"
$remoteConfigDir = "$RemoteRoot/oxide/config"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

function New-SshArgs {
    param([bool]$ForScp)
    if ($ForScp) {
        $args = @("-P", $RemotePort, "-o", "StrictHostKeyChecking=accept-new")
    }
    else {
        $args = @("-p", $RemotePort, "-o", "StrictHostKeyChecking=accept-new")
    }
    if (-not [string]::IsNullOrWhiteSpace($IdentityFile)) {
        if (-not (Test-Path -Path $IdentityFile)) {
            throw "Identity file not found: $IdentityFile"
        }
        $args += @("-i", $IdentityFile)
    }
    return ,$args
}

function Invoke-RemoteCommand {
    param([string]$CommandText)
    $sshArgs = New-SshArgs -ForScp:$false
    if ($DryRun) {
        Write-Host "[DRY-RUN][SSH] $RemoteUser@$RemoteHost :: $CommandText"
        return
    }
    & $sshCmd.Source @sshArgs "$RemoteUser@$RemoteHost" $CommandText
    if ($LASTEXITCODE -ne 0) {
        throw "SSH command failed: $CommandText"
    }
}

function Copy-ToRemote {
    param(
        [string]$LocalPath,
        [string]$RemotePath
    )
    if (-not (Test-Path -Path $LocalPath)) {
        throw "Local source file not found: $LocalPath"
    }

    $scpArgs = New-SshArgs -ForScp:$true
    if ($DryRun) {
        Write-Host "[DRY-RUN][SCP] $LocalPath -> $RemoteUser@${RemoteHost}:$RemotePath"
        return
    }

    & $scpCmd.Source @scpArgs $LocalPath "$RemoteUser@${RemoteHost}:$RemotePath"
    if ($LASTEXITCODE -ne 0) {
        throw "SCP copy failed: $LocalPath -> $RemotePath"
    }
}

function Backup-RemoteFile {
    param([string]$RemotePath)
    $cmd = "if [ -f '$RemotePath' ]; then cp '$RemotePath' '$RemotePath.$timestamp.bak'; fi"
    Invoke-RemoteCommand -CommandText $cmd
}

Write-Host "=== Remote deploy (by Shmatko) ==="
Write-Host "Target: $RemoteUser@${RemoteHost}:$RemoteRoot"

Invoke-RemoteCommand -CommandText "mkdir -p '$remotePluginDir' '$remoteConfigDir'"

if (-not $SkipPlugins) {
    $pluginMap = @(
        @{ Local = $PrivilegePluginSource; Remote = "$remotePluginDir/PrivilegeSystem.cs" },
        @{ Local = $LootPluginSource; Remote = "$remotePluginDir/ContainerLootManager.cs" }
    )
    foreach ($entry in $pluginMap) {
        if ($CreateBackups) {
            Backup-RemoteFile -RemotePath $entry.Remote
        }
        Copy-ToRemote -LocalPath $entry.Local -RemotePath $entry.Remote
        Write-Host "Plugin uploaded: $($entry.Remote)"
    }
}
else {
    Write-Host "Skipping plugin upload."
}

if (-not $SkipConfigs) {
    $configMap = @(
        @{ Local = $PrivilegeConfigSource; Remote = "$remoteConfigDir/PrivilegeSystem.json" },
        @{ Local = $LootConfigSource; Remote = "$remoteConfigDir/ContainerLootManager.json" }
    )
    foreach ($entry in $configMap) {
        if ($CreateBackups) {
            Backup-RemoteFile -RemotePath $entry.Remote
        }
        Copy-ToRemote -LocalPath $entry.Local -RemotePath $entry.Remote
        Write-Host "Config uploaded: $($entry.Remote)"
    }
}
else {
    Write-Host "Skipping config upload."
}

Write-Host ""
Write-Host "Remote deploy completed."
Write-Host "Recommended next steps on production server:"
Write-Host "  1) Verify files under:"
Write-Host "     $remotePluginDir"
Write-Host "     $remoteConfigDir"
Write-Host "  2) Reload plugins in Rust console/RCON:"
Write-Host "     oxide.reload ContainerLootManager"
Write-Host "     oxide.reload PrivilegeSystem"
