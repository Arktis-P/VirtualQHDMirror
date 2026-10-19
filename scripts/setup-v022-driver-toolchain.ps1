# Developer-only Windows toolchain bootstrap. Never bundled in VQM releases.
# Default = inspection. -Install requires explicit approval before any change.
[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$SkipBuild
)
$ErrorActionPreference = "Stop"
$minimumVsMajor = 18
$requiredKitsBuild = "10.0.28000"
$wingetConfig = "https://raw.githubusercontent.com/microsoft/Windows-driver-samples/2dc3fd3a0cc84a2933f2194e7ec0871584979071/_wdk_utils/winget/configs/wdk-vscommunity.dsc.yaml"
$root = Split-Path -Parent $PSScriptRoot

function Find-VsInstance {
    $programFiles86 = [Environment]::GetFolderPath("ProgramFilesX86")
    $vswhere = Join-Path $programFiles86 "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path -LiteralPath $vswhere)) { return $null }
    $path = & $vswhere -latest -products * -property installationPath 2>$null | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($path)) { return $null }
    return $path
}

function Get-ToolchainStatus {
    $vs = Find-VsInstance
    $version = ""
    $targetsOk = $false
    if ($vs) {
        $vswhere = Join-Path ([Environment]::GetFolderPath("ProgramFilesX86")) "Microsoft Visual Studio\Installer\vswhere.exe"
        $version = & $vswhere -latest -products * -property installationVersion 2>$null | Select-Object -First 1
        $cppTargetsRoot = Join-Path $vs "MSBuild\Microsoft\VC"
        $targets = @(Get-ChildItem -LiteralPath $cppTargetsRoot -Filter Microsoft.Cpp.Default.props -File -Recurse -ErrorAction SilentlyContinue)
        $targetsOk = $targets.Count -gt 0
    }
    $major = 0
    if ($version -match '^(\d+)\.') { $major = [int]$Matches[1] }

    $kits = Join-Path ([Environment]::GetFolderPath("ProgramFilesX86")) "Windows Kits\10"
    $sdkVersions = @()
    $wdkVersions = @()
    $sdkInclude = Join-Path $kits "Include"
    $wdkLib = Join-Path $kits "Lib"
    if (Test-Path $sdkInclude) {
        $sdkVersions = @(Get-ChildItem -LiteralPath $sdkInclude -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "$requiredKitsBuild*" -and (Test-Path (Join-Path $_.FullName "um")) } |
            Select-Object -ExpandProperty Name)
    }
    # WDK driver toolset files and driver-specific library folders. The final
    # source-build gate still validates full integration by actually compiling.
    $wdkFiles = @()
    $wdkTarget = Join-Path $kits "build"
    if (Test-Path $wdkTarget) {
        $wdkFiles = @(Get-ChildItem -LiteralPath $wdkTarget -Filter WindowsDriver*.targets -File -Recurse -ErrorAction SilentlyContinue)
    }
    if (Test-Path $wdkLib) {
        $wdkVersions = @(Get-ChildItem -LiteralPath $wdkLib -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "$requiredKitsBuild*" } |
            Select-Object -ExpandProperty Name)
    }
    # WDF runtime libraries are commonly under Lib\wdf\umdf instead of
    # Lib\<SDK version>\umdf, so do not assume a versioned UMDF subfolder.
    $wdfUm = Join-Path $wdkLib "wdf\umdf"
    $hasWdf = Test-Path -LiteralPath $wdfUm
    [pscustomobject]@{
        VSPath = $vs
        VSVersion = $version
        VSCompatible = ($major -ge $minimumVsMajor)
        CppTargets = $targetsOk
        SDK = ($sdkVersions.Count -gt 0)
        SDKVersions = ($sdkVersions -join ", ")
        WDK = ($wdkFiles.Count -gt 0 -and ($wdkVersions.Count -gt 0 -or $hasWdf))
        WDKVersions = ($wdkVersions -join ", ")
    }
}

function Show-Status($s) {
    $vsPath = if ($s.VSPath) { $s.VSPath } else { "(not detected)" }
    Write-Host "[CHECK] Visual Studio: $($s.VSVersion) $vsPath"
    Write-Host "[CHECK] Visual Studio 2026+: $($s.VSCompatible)"
    Write-Host "[CHECK] Visual C++ targets: $($s.CppTargets)"
    Write-Host "[CHECK] Windows SDK $requiredKitsBuild*: $($s.SDK) $($s.SDKVersions)"
    Write-Host "[CHECK] Windows WDK $requiredKitsBuild*: $($s.WDK) $($s.WDKVersions)"
}

$status = Get-ToolchainStatus
Show-Status $status
$ready = $status.VSCompatible -and $status.CppTargets -and $status.SDK -and $status.WDK

if (-not $ready -and -not $Install) {
    Write-Host "[NEEDS_USER_TOOLCHAIN] Driver development prerequisites incomplete."
    Write-Host "[ACTION] Inspect output, then re-run with -Install to approve official Microsoft WinGet configuration."
    Write-Host "[INFO] Official pinned config: $wingetConfig"
    exit 2
}
if (-not $ready -and $Install) {
    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if (-not $winget) {
        Write-Host "[NEEDS_USER_TOOLCHAIN] Windows Package Manager (winget) not installed."
        Write-Host "[ACTION] Install/update Microsoft App Installer before running -Install."
        exit 2
    }

    Write-Host "[ACTION] Official Microsoft WinGet configuration will install/modify Visual Studio Community, VC++, SDK and WDK."
    Write-Host "[ACTION] This may use several GB and may request elevation or a restart."
    Write-Host "[INFO] Existing VS Community 2026 may be modified; no existing installation is uninstalled."
    Write-Host "[INFO] Configuration is pinned to reviewed upstream commit."
    $approval = Read-Host "Type INSTALL to consent to developer toolchain changes (anything else cancels)"
    if ($approval -cne "INSTALL") {
        Write-Host "[CANCELLED] No installation started."
        exit 3
    }
    & $winget.Source configure --file $wingetConfig --accept-configuration-agreements
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Write-Host "[BLOCKED] WinGet configuration failed ($exitCode); inspect output and rerun check."
        exit $exitCode
    }

    # Re-discover after installation; do not blindly report success.
    $status = Get-ToolchainStatus
    Show-Status $status
    $ready = $status.VSCompatible -and $status.CppTargets -and $status.SDK -and $status.WDK
}
if (-not $ready) {
    Write-Host "[NEEDS_USER_TOOLCHAIN] Toolchain not fully detected after setup. Check restart requirements / installer output."
    exit 2
}
Write-Host "[PASS] VS/VC++/SDK/WDK development prerequisites detected."
if ($SkipBuild) { Write-Host "[READY] Toolchain detected; actual WDK compilation NOT RUN."; exit 0 }
Write-Host "[INFO] Invoking driver-only build gate (no driver installation or signing)."
& (Join-Path $PSScriptRoot "check-v022-driver.ps1")
exit $LASTEXITCODE
