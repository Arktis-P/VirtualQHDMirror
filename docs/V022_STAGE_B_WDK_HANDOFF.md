# v0.22 Stage B-2 — WDK compile handoff (source-only)

## Completed code checkpoint
- Bounded 1–4 monitor startup setting from dedicated VQM device hardware key; invalid values default to one.
- Stable per-slot monitor identities, QHD 2560x1440 @ 60 Hz modes, no physical sample EDID.
- Failed IddCx device initialization no longer proceeds to create the driver context.
- Monitor WDF cleanup callback deletes the per-monitor context; failed arrival destroys the partial monitor.
- Local driver-only WDK build/static gate script, with no driver installation or UAC.

## User check: WDK toolchain availability / compile
On Windows with Visual Studio C++ and the matching Windows Driver Kit (WDK), from the root of the cloned repository run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-v022-driver.ps1
```

If WDK is not installed, the static-only gate is available:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-v022-driver.ps1 -StaticOnly
```

Send the last line containing [READY] or the error and `artifacts/driver/v022-b2-wdk-build.log` if blocked.

**Compile only:** the script never calls pnputil, Device Manager, devcon, an installer, signing, or a driver restart. Compiling does NOT authorize installation on the daily-use PC.

## Hold points
- INF DriverVer and publisher are not production-ready; driver not signed or released.
- No privileged monitor count mutation, runtime hotplug, transactional restart/rollback, clean install/upgrade/repair/uninstall, or runtime 1–4 monitor verification.
- Keep using ge9 compatibility and run-v022.cmd. No GitHub Actions.

## Windows 11 toolchain troubleshooting (MSB4019)
If MSBuild reports that `Microsoft.Cpp.Default.props` cannot be found in `MSBuild\\Microsoft\\VC\\v180`, the Visual Studio 2026 C++ project build tools are missing/incomplete. This is a **machine toolchain problem**, not a C++ driver compiler diagnostic. The updated script reports `[NEEDS_USER_TOOLCHAIN]` before trying to build.

Use **Visual Studio Installer → Visual Studio 2026 Community → Modify**. Select **Desktop development with C++** and verify MSVC x64/x86 build tools and the Windows SDK. Under **Individual components**, select **Windows Driver Kit** and the relevant Spectre-mitigated libraries. For Visual Studio 2026 Microsoft's current guidance recommends **WDK 10.0.28000.2526** and a matching **SDK** (same build number). Complete changes, reopen PowerShell and rerun the gate. Do not manually copy `.props` files into MSBuild directories.

Microsoft reference: https://learn.microsoft.com/en-us/windows-hardware/drivers/download-the-wdk
