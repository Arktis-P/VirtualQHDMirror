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
