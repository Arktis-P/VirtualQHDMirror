# v0.22 distribution guide

- Windows 11 x64, self-contained .NET 8 app.
- App-only per-user Inno Setup installer; portable ZIP already supported.
- ge9 and experimental VQM IDD not bundled; Setup never changes device driver settings or trusted certificates.

Run on Windows with .NET 8 SDK and Inno Setup 6:

1. build-v022-installer.cmd: creates the existing app ZIP and optional Setup.exe, both with SHA256.
2. powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-v022-release.ps1: read-only asset integrity test.
3. Smoke-test Setup.exe fresh installation, startup, Get driver link, Recheck, mirror start/stop, and uninstall. Check auto-start behaviour if enabled. Also test ZIP.
4. After integration to main, install GitHub CLI and authenticate; on clean synced main run powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-v022-release.ps1 -Publish to create v0.22.0 Release with four assets.

The installer is unsigned; no release binaries have been built or executed by this assistant. No GitHub Actions are used. GitHub connector currently cannot upload local release binaries; a local publishing step is required.

## Inno Setup installation (one-time developer prerequisite)

Inno Setup is a THIRD-PARTY build tool, not a dependency to bundle in this repository or in the VQM release. Its upstream publishes an official winget package (JRSoftware.InnoSetup).

From the repository root, run `setup-inno-v022.cmd`. It checks for winget and asks for explicit confirmation before invoking the **official package** with `winget install --id JRSoftware.InnoSetup -e -s winget -i`. Follow the installer prompts. Then run `build-v022-installer.cmd`. If the tool is installed for the current user, the build script also searches LocalAppData paths.

Official documentation and download: https://jrsoftware.org/isdl.php

## Version naming and safe upgrades

- Stable installed identity: Inno Setup `AppId = {C3E58A21-1EC0-4B96-A841-17CC6679F664}` across all VQM releases.
- Stable application name: `VirtualQHDMirror`, EXE `VirtualQHDMirror.exe`, installed directory `%LOCALAPPDATA%\\Programs\\VirtualQHDMirror`, shortcut name `VirtualQHDMirror`.
- `AppVersion` changes per release so Windows Add/Remove Programs reports the installed version.
- Version numbers belong in downloadable filenames (e.g. `VirtualQHDMirror-v0.22-win-x64-Setup.exe`), ZIP names, release tags and EXE VERSION resource. Changing the Setup.exe *filename* does not create a new Windows application.
- A later version with the **same AppId, install mode and location** should perform an in-place upgrade. Installing a second copy to a different chosen directory or changing AppId/scope can break this guarantee. Confirm installer upgrade/uninstall behaviour locally before publishing future versions.
- Portable ZIP versions coexist in separate folders and are not Windows-installed applications.
