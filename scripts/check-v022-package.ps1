# VQM Stage B3 read-only package preflight. Does NOT install any driver.
[CmdletBinding()]
param(
    [string]$PackageDirectory = "",
    [switch]$AllowUnsignedDevelopmentPackage
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $PackageDirectory = Join-Path $root "driver\VirtualDisplay\Source\IddSampleDriver\x64\Release\IddSampleDriver"
}
function Stop-Gate([string]$reason) {
    Write-Host "[BLOCKED] $reason"
    exit 1
}
function Get-OneFile([string]$base, [string]$pattern) {
    $matches = @(Get-ChildItem -LiteralPath $base -Filter $pattern -File -Recurse -ErrorAction SilentlyContinue)
    if ($matches.Count -ne 1) { Stop-Gate "Expected exactly one $pattern; found $($matches.Count)." }
    return $matches[0]
}
if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
    Stop-Gate "Package directory not found: $PackageDirectory. Compile B2 first."
}
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
Write-Host "[INFO] Read-only package preflight: $package"
$inf = Get-OneFile $package "*.inf"
$cat = Get-OneFile $package "*.cat"
$dll = Get-OneFile $package "IddSampleDriver.dll"
$infText = [IO.File]::ReadAllText($inf.FullName)
if ($infText -notmatch '(?im)^\s*[^;].*ROOT\\VirtualQHDMirrorDisplay\b') {
    Stop-Gate "Exact VQM hardware ID absent from INF."
}
if ($infText -notmatch '(?im)^\s*UMDriverCopy\s*=\s*13\s*(?:;.*)?$') {
    Stop-Gate "Driver DLL is not isolated to Driver Store DIRID 13."
}
if ($infText -notmatch '(?im)^\s*ServiceBinary\s*=\s*%13%\\IddSampleDriver\.dll\s*(?:;.*)?$') {
    Stop-Gate "UMDF ServiceBinary is not rooted in DIRID 13."
}
if ($infText -match '(?im)^\s*[^;].*ROOT\\(?!VirtualQHDMirrorDisplay\b)\S+') {
    Stop-Gate "INF contains another ROOT device ID; manual review required."
}
Write-Host "[PASS] Exact VQM identity and isolated UMDF binary path."
$catalogSignature = Get-AuthenticodeSignature -LiteralPath $cat.FullName
Write-Host "[CHECK] Catalog signature status: $($catalogSignature.Status)"
if ($catalogSignature.Status -ne "Valid") {
    if ($AllowUnsignedDevelopmentPackage) {
        Write-Host "[DEV_ONLY] Unsigned package observed. Installation is prohibited."
    } else {
        Stop-Gate "Catalog is not Authenticode-valid. This package cannot be installed by VQM."
    }
}
# A valid catalog signature by itself is insufficient: publisher trust, WHQL/
# attestation, catalog membership and device binding require additional review.
# Never print "signed production READY" based solely on Get-AuthenticodeSignature.
Write-Host "[CHECK] INF: $($inf.Name); DLL: $($dll.Name); CAT: $($cat.Name)"
if ($AllowUnsignedDevelopmentPackage) {
    Write-Host "[READY] B3 offline development inventory passed. NOT production-authorized. DO NOT INSTALL."
} else {
    Write-Host "[NEEDS_RELEASE_APPROVAL] Catalog status checked, but publisher/device signing and package binding are unverified."
    Write-Host "[ACTION] Complete Microsoft signing and clean-machine runtime gate before enabling VQM installation."
    exit 2
}
