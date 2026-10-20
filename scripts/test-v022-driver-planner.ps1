# Offline regression tests for the B3 plan. No PnP/registry writes.
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
$planner = Join-Path $PSScriptRoot "plan-v022-driver-change.ps1"
$temp = Join-Path ([IO.Path]::GetTempPath()) ("vqm-b3-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temp | Out-Null
$count = 0
function Case([string]$label, $fixture, [int]$target, [bool]$expectPass) {
    $file = Join-Path $temp ($label + ".json")
    $fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $file -Encoding UTF8
    $out = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $planner -SnapshotPath $file -DesiredMonitorCount $target 2>&1)
    $passed = ($LASTEXITCODE -eq 0 -and ($out -join ' ').Contains('[READY]'))
    if ($passed -ne $expectPass) { throw "[FAIL] $label expected=$expectPass got=$passed output=$out" }
    $script:count++
    Write-Host "[PASS] $label"
}
try {
    $normal = @{ schemaVersion=1; mirrorSessionRunning=$false; devices=@(@{
        hardwareIds=@('ROOT\VirtualQHDMirrorDisplay');
        instanceId='ROOT\VirtualQHDMirrorDisplay\0001';
        deviceGroupId='VirtualQHDMirrorIddGroup'; deviceClass='Display';
        status='OK'; configuredMonitorCount=1; activeQhdMonitorCount=1
    })}
    Case 'no-change' $normal 1 $true
    Case 'increase-to-four' $normal 4 $true
    Case 'underflow' $normal 0 $false
    Case 'overflow' $normal 5 $false
    $ge9 = $normal | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $ge9.devices[0].hardwareIds[0] = 'ROOT\IddSampleDriver'
    Case 'reject-ge9' $ge9 2 $false
    $dup = $normal | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $dup.devices = @($dup.devices[0], $dup.devices[0])
    Case 'reject-ambiguous' $dup 2 $false
    $running = $normal | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $running.mirrorSessionRunning = $true
    Case 'reject-running-mirror' $running 2 $false
    $disabled = $normal | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $disabled.devices[0].status = 'Disabled'
    Case 'reject-disabled' $disabled 2 $false
    $group = $normal | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $group.devices[0].deviceGroupId = 'Other'
    Case 'reject-group' $group 2 $false
    $invalid = $normal | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $invalid.devices[0].configuredMonitorCount = 0
    Case 'reject-invalid-count' $invalid 2 $false
    Write-Host "[READY] B3 offline regression cases passed: $count. No Windows changes."
} finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
