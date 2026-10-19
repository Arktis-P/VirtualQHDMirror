# Stage B release exit gate. Read-only; no installer, driver writes or restart.
[CmdletBinding()]
param([string]$PackageDirectory = "")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $PackageDirectory = Join-Path $root "driver\VirtualDisplay\Source\IddSampleDriver\x64\Release\IddSampleDriver"
}
$sourceInf = Join-Path $root "driver\VirtualDisplay\Source\IddSampleDriver\IddSampleDriver.inf"
$required = @(
    $sourceInf,
    (Join-Path $PSScriptRoot "check-v022-driver.ps1"),
    (Join-Path $PSScriptRoot "check-v022-package.ps1"),
    (Join-Path $PSScriptRoot "plan-v022-driver-change.ps1"),
    (Join-Path $PSScriptRoot "test-v022-driver-planner.ps1")
)
foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        Write-Host "[BLOCKED] Missing B-stage source artifact: $file"
        exit 1
    }
}
$inf = [IO.File]::ReadAllText($sourceInf)
$pending = @()
if ($inf -match 'DriverVer\s*=\s*;' -or $inf -match '(?i)TODO:\s*set DriverVer') {
    $pending += "Production DriverVer is not specified in source INF."
}
if ($inf -match '<Your manufacturer name>') {
    $pending += "Legal manufacturer/publisher is still a sample placeholder."
}
if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
    $pending += "Generated package directory not present on this host (B2 build evidence may be from another Windows machine)."
} else {
    $catalogs = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter "*.cat" -File -Recurse -ErrorAction SilentlyContinue)
    if ($catalogs.Count -ne 1) {
        $pending += "Exactly one driver catalog was expected; found $($catalogs.Count)."
    } else {
        $signature = Get-AuthenticodeSignature -LiteralPath $catalogs[0].FullName
        Write-Host "[CHECK] Catalog Authenticode: $($signature.Status)"
        if ($signature.Status -ne "Valid") {
            $pending += "Unsigned or invalid catalog: production Microsoft signing unavailable."
        } else {
            $pending += "Valid Authenticode alone does NOT establish Microsoft driver approval, full catalog file binding, or isolated Windows runtime PASS."
        }
    }
}
# Runtime and Microsoft submission are not authenticated by this repository.
# Never let local text fixtures or signatures turn this into a production PASS.
$pending += "Microsoft driver submission / signer and package trust not independently verified."
$pending += "Clean Windows 11 install/1-4 display/restart/rollback/sleep/upgrade/uninstall evidence missing."
$pending += "Signed-package-locked elevated VQM-only lifecycle installer is not implemented or QA-approved."
Write-Host "[PASS] Stage B source artifacts exist. B2 and B3 local gates have separately been reported PASS."
foreach ($item in $pending) { Write-Host "[HOLD] $item" }
Write-Host "[BLOCKED] Stage B production exit NOT APPROVED. Existing ge9 and physical displays remain untouched."
exit 2
