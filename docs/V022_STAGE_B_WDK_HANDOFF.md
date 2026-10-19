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
