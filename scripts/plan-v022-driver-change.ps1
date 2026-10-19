# B3 device-count proposal. Offline JSON only. NEVER installs/restarts.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$SnapshotPath,
      [Parameter(Mandatory=$true)][int]$DesiredMonitorCount)
$ErrorActionPreference = "Stop"
function Block([string]$why) { Write-Host "[BLOCKED] $why"; exit 2 }
if ($DesiredMonitorCount -lt 1 -or $DesiredMonitorCount -gt 4) { Block "Desired count outside 1..4." }
if (-not (Test-Path -LiteralPath $SnapshotPath -PathType Leaf)) { Block "Snapshot missing." }
try {
    $snapshot = Get-Content -LiteralPath $SnapshotPath -Raw | ConvertFrom-Json
} catch { Block "Malformed snapshot." }
if ($snapshot.schemaVersion -ne 1 -or @($snapshot.devices).Count -ne 1) {
    Block "Only schema 1 and exactly one VQM device are allowed."
}
$d = @($snapshot.devices)[0]
$hardware = 'ROOT\VirtualQHDMirrorDisplay'
if (@($d.hardwareIds).Count -ne 1 -or
    -not [string]::Equals([string]$d.hardwareIds[0],$hardware,[StringComparison]::OrdinalIgnoreCase)) {
    Block "Exact hardware ID mismatch (other devices are forbidden)."
}
if (-not ([string]$d.instanceId).StartsWith(($hardware + '\'),[StringComparison]::OrdinalIgnoreCase)) {
    Block "VQM instance ID mismatch."
}
if ($d.deviceGroupId -cne 'VirtualQHDMirrorIddGroup' -or $d.deviceClass -ne 'Display') {
    Block "Device group/class mismatch."
}
if ($d.status -ne 'OK' -or $snapshot.mirrorSessionRunning -ne $false) {
    Block "Device unhealthy or mirror session running."
}
if ($d.configuredMonitorCount -isnot [int] -or $d.configuredMonitorCount -lt 1 -or $d.configuredMonitorCount -gt 4) {
    Block "Current monitor count invalid."
}
if ($d.activeQhdMonitorCount -isnot [int] -or $d.activeQhdMonitorCount -lt 0 -or $d.activeQhdMonitorCount -gt 4) {
    Block "Active QHD monitor count invalid."
}
$action = if ($DesiredMonitorCount -ne $d.configuredMonitorCount) {
    'PROPOSE_VQM_ONLY_CHANGE_AND_RESTART'
} elseif ($d.activeQhdMonitorCount -ne $DesiredMonitorCount) {
    'PROPOSE_TOPOLOGY_RECHECK'
} else { 'NO_CHANGE' }
$result = [ordered]@{
    authorization = 'NONE_OFFLINE_SIMULATION'
    operation = $action
    deviceInstanceId = $d.instanceId
    registryName = 'VqmMonitorCount'
    originalCount = $d.configuredMonitorCount
    requestedCount = $DesiredMonitorCount
    rollbackCount = $d.configuredMonitorCount
    expectedMode = '2560x1440@60'
    mayModifyWindows = $false
    requiresSeparateProductionSigningAndUac = $true
}
Write-Host "[PASS] Exact VQM-only plan constraints."
$result | ConvertTo-Json -Depth 4
Write-Host "[READY] B3 OFFLINE plan only. NO Windows device changes."
