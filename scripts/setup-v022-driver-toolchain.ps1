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

    Write-Host "[ACTION] Official Microsoft Visual Studio Installer and WinGet will install missing developer tools."
    Write-Host "[ACTION] This may use several GB and may request elevation or a restart."
    Write-Host "[INFO] Existing VS Community 2026 may be modified; no existing installation is uninstalled."
    Write-Host "[INFO] Visual Studio component list is pinned to reviewed Microsoft upstream commit."
    $approval = Read-Host "Type INSTALL to consent to developer toolchain changes (anything else cancels)"
    if ($approval -cne "INSTALL") {
        Write-Host "[CANCELLED] No installation started."
        exit 3
    }
    # VS Installer --passive requires UAC elevation at process start.
    # Avoid WinGet DSC because it can partially apply components without admin.
    $vsConfigUri = "https://raw.githubusercontent.com/microsoft/Windows-driver-samples/2dc3fd3a0cc84a2933f2194e7ec0871584979071/_wdk_utils/winget/configs/wdk-desktop.vsconfig"
    $vsConfigPath = Join-Path ([System.IO.Path]::GetTempPath()) ("vqm-wdk-" + [guid]::NewGuid().ToString("N") + ".vsconfig")
    try {
        Invoke-WebRequest -Uri $vsConfigUri -OutFile $vsConfigPath -ErrorAction Stop
        $components = Get-Content -LiteralPath $vsConfigPath -Raw | ConvertFrom-Json
        if (-not ($components.components -contains "Component.Microsoft.Windows.DriverKit") -or -not ($components.components -contains "Microsoft.VisualStudio.Component.VC.Tools.x86.x64")) {
            throw "Official VS configuration lacks required driver / C++ components."
        }
        $vsPath = Find-VsInstance
        $setup = Join-Path ([Environment]::GetFolderPath("ProgramFilesX86")) "Microsoft Visual Studio\Installer\setup.exe"
        if (-not $vsPath -or -not (Test-Path -LiteralPath $setup)) {
            throw "Existing VS instance or official Visual Studio Installer not found."
        }
        $preCheck = Get-ToolchainStatus
        if (-not $preCheck.CppTargets) {
            Write-Host "[ACTION] Approve Windows UAC elevation for the Visual Studio Installer."
            Write-Host "[INFO] Adding VS C++/WDK components to: $vsPath"
            $args = @("modify", "--installPath", ('"' + $vsPath + '"'), "--config", ('"' + $vsConfigPath + '"'), "--passive", "--norestart")
            $proc = Start-Process -FilePath $setup -ArgumentList $args -Verb RunAs -WorkingDirectory $root -Wait -PassThru -ErrorAction Stop
            if ($proc.ExitCode -notin @(0,3010)) {
                throw "Visual Studio Installer modify failed (exit $($proc.ExitCode))."
            }
            if ($proc.ExitCode -eq 3010) {
                Write-Host "[NEEDS_RESTART] Restart Windows, reopen PowerShell, then rerun the check."
                exit 3010
            }
        }
        else {
            Write-Host "[PASS] Visual Studio C++ targets already installed; skipping VS modify."
        }
    }
    catch {
        Write-Host "[BLOCKED] Visual Studio Installer failed: $($_.Exception.Message)"
        Write-Host "[ACTION] Approve UAC on retry or use VS Installer > Modify > Desktop development with C++."
        exit 4
    }
    finally {
        Remove-Item -LiteralPath $vsConfigPath -Force -ErrorAction SilentlyContinue
    }

    # An earlier WinGet DSC run may already have installed SDK/WDK packages.
    $postVs = Get-ToolchainStatus
    foreach ($pkg in @(
        @{ Id = "Microsoft.WindowsSDK.10.0.28000"; Key = "SDK" },
        @{ Id = "Microsoft.WindowsWDK.10.0.28000"; Key = "WDK" }
    )) {
        if ($postVs.($pkg.Key)) {
            Write-Host "[PASS] $($pkg.Id) already present; skipping."
            continue
        }
        Write-Host "[INFO] Installing/checking $($pkg.Id) through ordinary WinGet."
        & $winget.Source install --id $pkg.Id --exact --source winget --accept-package-agreements --accept-source-agreements
        $code = $LASTEXITCODE
        if ($code -ne 0) {
            Write-Host "[WARN] WinGet returned $code for $($pkg.Id); checking actual installed files."
        }
        $postVs = Get-ToolchainStatus
        if (-not $postVs.($pkg.Key)) {
            Write-Host "[NEEDS_USER_TOOLCHAIN] $($pkg.Id) did not pass on-disk verification."
            Write-Host "[ACTION] Review Windows SDK / WDK installer status and rerun check."
            exit 2
        }
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
