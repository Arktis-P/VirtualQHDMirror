# B2 source-only gate: no driver installation, signing or restart.
[CmdletBinding()]
param([switch]$StaticOnly, [string]$LogPath = "")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "driver\VirtualDisplay\Source\IddSampleDriver"
$driver = Join-Path $source "Driver.cpp"
$header = Join-Path $source "Driver.h"
$inf = Join-Path $source "IddSampleDriver.inf"
$project = Join-Path $source "IddSampleDriver.vcxproj"
$license = Join-Path $root "driver\VirtualDisplay\LICENSE-MS-PL.txt"
$provenance = Join-Path $root "driver\VirtualDisplay\UPSTREAM_PROVENANCE.md"

function Assert-Vqm([bool]$condition, [string]$message) {
    if (-not $condition) { throw "[BLOCKED] $message" }
}
foreach ($path in @($driver, $header, $inf, $project, $license, $provenance)) {
    Assert-Vqm (Test-Path -LiteralPath $path -PathType Leaf) ("Missing: " + $path)
}
$cpp = [System.IO.File]::ReadAllText($driver)
$h = [System.IO.File]::ReadAllText($header)
$infText = [System.IO.File]::ReadAllText($inf)

Assert-Vqm ($cpp.Contains("static constexpr ULONG VQM_MAX_MONITORS = 4")) "Monitor capacity must be four."
Assert-Vqm ($cpp.Contains("requested >= 1 && requested <= VQM_MAX_MONITORS")) "1-4 bounds check missing."
Assert-Vqm ($cpp.Contains('L"VqmMonitorCount"')) "Driver count key missing."
Assert-Vqm ($cpp.Contains('2560, 1440, 60')) "QHD 60Hz mode missing."
Assert-Vqm (-not $cpp.Contains("CoCreateGuid(&MonitorInfo.MonitorContainerId)")) "Random container ID not allowed."
Assert-Vqm (-not $cpp.Contains("s_SampleMonitors")) "Physical sample EDIDs must not be used."
Assert-Vqm ($cpp.Contains("m_MonitorSlots[ConnectorIndex]")) "Independent monitor slot tracking missing."
Assert-Vqm ($cpp.Contains("delete context->pContext")) "Monitor context teardown missing."
Assert-Vqm ($cpp.Contains("if (!NT_SUCCESS(Status))")) "IddCx failure guard missing."
Assert-Vqm ($h.Contains("std::array<IDDCX_MONITOR, 4>")) "Four independent slots missing."
Assert-Vqm ($infText.Contains('ROOT\VirtualQHDMirrorDisplay')) "Exact VQM device ID missing."
Assert-Vqm ($infText.Contains('"VirtualQHDMirrorIddGroup"')) "UMDF group missing."
Assert-Vqm ($infText.Contains('"VqmMonitorCount"')) "INF default count missing."
Assert-Vqm ($infText.Contains('DriverVer= ; TODO:')) "Unexpected release DriverVer; stop for review."
Assert-Vqm ($infText.Contains('<Your manufacturer name>')) "Unexpected release publisher; stop for review."

Write-Host "[PASS] B2 read-only source invariants (no system changes)."
if ($StaticOnly) { Write-Host "[READY] Static-only source gate. WDK build NOT RUN."; exit 0 }

$msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
if (-not $msbuild) {
    $programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
    $vswhere = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($found -and (Test-Path -LiteralPath $found)) { $msbuild = Get-Item -LiteralPath $found }
    }
}
if (-not $msbuild) {
    Write-Host "[NEEDS_USER_TOOLCHAIN] MSBuild not found. Install VS C++ and WDK."
    exit 2
}
$msbuildPath = if ($msbuild -is [System.Management.Automation.CommandInfo]) { $msbuild.Source } else { $msbuild.FullName }
# MSBuild.exe alone is insufficient: VS must also have the VC targets and WDK.
# Resolve this VS instance's C++ targets before launching MSBuild so a missing
# workload is reported explicitly instead of an opaque MSB4019 error.
$msbuildBin = Split-Path -Parent $msbuildPath
$msbuildRoot = Split-Path -Parent $msbuildBin
$vsRoot = Split-Path -Parent $msbuildRoot
$vcTargetsRoot = Join-Path $msbuildRoot "Microsoft\\VC"
$cppTargets = @(Get-ChildItem -LiteralPath $vcTargetsRoot -Filter "Microsoft.Cpp.Default.props" -Recurse -File -ErrorAction SilentlyContinue)
if ($cppTargets.Count -eq 0) {
    Write-Host "[NEEDS_USER_TOOLCHAIN] Visual Studio MSBuild exists, but Microsoft.Cpp.Default.props is missing."
    Write-Host "[ACTION] Open Visual Studio Installer > Modify > Desktop development with C++."
    Write-Host "[ACTION] Enable x64/x86 MSVC tools, the Windows SDK, and the Windows Driver Kit (WDK)."
    Write-Host "[ACTION] Visual Studio 2026 should use WDK 10.0.28000.2526 + matching SDK."
    Write-Host "[INFO] Searched: $vcTargetsRoot"
    exit 2
}
# If there are multiple matching MSBuild installations, use the current VS
# instance's targets. Avoid silently using a targets folder from another VS.
$expectedCppTargets = Join-Path (Join-Path $vcTargetsRoot "v180") "Microsoft.Cpp.Default.props"
if (($msbuildPath -match "[\\/]18[\\/]") -and -not (Test-Path -LiteralPath $expectedCppTargets)) {
    Write-Host "[NEEDS_USER_TOOLCHAIN] VS 2026 v180 C++ targets are missing: $expectedCppTargets"
    Write-Host "[ACTION] Modify the VS 2026 installation to add Desktop development with C++."
    exit 2
}
Write-Host "[PASS] Visual C++ MSBuild target files detected."
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $root "artifacts\driver\v022-b2-wdk-build.log"
}
$logDir = Split-Path -Parent $LogPath
if ($logDir -and -not (Test-Path -LiteralPath $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}
Write-Host "[INFO] Building VQM IDD source only. No signing or installation."
Write-Host "[INFO] MSBuild: $msbuildPath"
& $msbuildPath $project /t:Build /p:Configuration=Release /p:Platform=x64 /p:SignMode=Off /p:DriverSigning=Off /m:1 /nologo /v:minimal 2>&1 | Tee-Object -FilePath $LogPath
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) { Write-Host "[BLOCKED] WDK build failed ($exitCode). Log: $LogPath"; exit $exitCode }
Write-Host "[READY] B2 WDK source build succeeded. Driver UNSIGNED; DO NOT INSTALL."
Write-Host "[INFO] WDK build log: $LogPath"
