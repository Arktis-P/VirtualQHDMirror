# v0.22 Stage B3 — package preflight checkpoint (offline/read-only)

## B2 evidence accepted

User Windows 11 toolchain and compile report on 2026-10-19:

- Visual Studio 2026 18.11 + SDK/WDK 10.0.28000: PASS.
- UMDF DLL compiled: PASS.
- WDK signability test: errors 0, warnings 0.
- Catalog generation: PASS (catalog generation does **not** imply signing).
- Standalone x64 InfVerif /w: PASS.
- Source compile/INF verification gate: **B2 COMPLETE**.

The binary is **unsigned**, the INF still has draft manufacturer and DriverVer fields (stamped for local compilation), and no install/runtime smoke test has been run. Do **not** install it on the user's main PC.

## This B3 checkpoint

Added `scripts/check-v022-package.ps1`, an independent read-only preflight for the generated driver package.

- Locates exactly one INF, one CAT, one driver DLL inside the given package directory.
- Checks exact ROOT\\VirtualQHDMirrorDisplay device ownership and Driver Store DIRID 13 isolation.
- Reports Authenticode catalog signature status but explicitly **does not** equate a signature check with a Microsoft-approved driver package.
- Default fail-closed: unsigned development packages blocked; even with a valid catalog signature, no production installation approval is issued.
- Optional `-AllowUnsignedDevelopmentPackage` is **inspection only**, never permits driver installation.
- No elevation, `pnputil`, driver registry writes, monitor restart or ge9 mutation.

### Local inspection

From repository root (after building with `scripts/check-v022-driver.ps1`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-v022-package.ps1 -AllowUnsignedDevelopmentPackage
```

This is a source/package inventory gate only. The script expects the generated INF, CAT and DLL in the WDK package output directory; if the toolchain uses an alternate output arrangement, report [BLOCKED] without moving or installing files.

## Still needed before public install

1. Replace draft manufacturer/publisher/DriverVer with real identity/version and preserve pinned driver provenance.
2. Complete Microsoft-signed catalog/package publishing; validate signer, certificate chain, package file coverage and target OS.
3. Implement exact-VQM-only elevated install/configure/restart/repair/uninstall with transactional rollback, explicit UAC, and strict ge9/physical adapter separation.
4. Validate driver runtime (1, 2, 3, 4 independent QHD@60 displays, stable IDs, sleep/resume, reboot, upgrade, rollback) on an **isolated Windows test environment**, not the existing ge9 daily-use PC.
5. Only then enable signed-driver automatic installation for public app releases.

**B3 status: offline preflight source added, untested on Windows; no privileged lifecycle enabled.**
