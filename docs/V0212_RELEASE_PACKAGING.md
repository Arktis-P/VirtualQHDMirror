# v0.21.2 Release Packaging

## Purpose

v0.21.2 replaces the version-specific root development launchers with a normal Windows application package.

The source repository keeps build scripts only. Generated binaries remain under ignored `artifacts/` output.

## Build entry point

From the repository root on Windows:

```text
build-release.cmd
```

This invokes:

```text
scripts/build-release.ps1
```

Requirements:

- Windows
- .NET 8 SDK
- no GitHub Actions / hosted CI

## Publish model

The build publishes three self-contained single-file win-x64 executables:

```text
VirtualQHDMirror.exe
VirtualQHDMirror.Session.exe
VirtualQHDMirror.ElevatedHelper.exe
```

All three must remain together.

The main executable is the only user-facing entry point. The session process owns the mirror window/render/input runtime. The elevated helper is started only when a driver configuration operation requires UAC approval.

## Output

Default output:

```text
artifacts/release/v0.21.2/win-x64/
```

Package:

```text
artifacts/release/v0.21.2/VirtualQHDMirror-v0.21.2-win-x64.zip
artifacts/release/v0.21.2/VirtualQHDMirror-v0.21.2-win-x64.zip.sha256
```

The root `run.cmd` launches the packaged main executable if it exists.

## Archive cleanup

Former root development launchers are stored under:

```text
archive/launchers/v0.20/
archive/launchers/v0.21.0/
archive/launchers/v0.21.1/
archive/launchers/v0.21.2/
```

They are historical snapshots, not the active runtime path.

## Local release validation

After building on Windows:

1. launch `VirtualQHDMirror.exe` directly;
2. confirm the v0.21.2 .NET/D3D11 engine is the default;
3. start/stop the mirror and confirm the sidecar session is found beside the app;
4. verify 1 → mirrored destination window/file drag bridging;
5. verify mirrored destination → adjacent physical display cursor exit;
6. verify Start with Windows can enable/disable the current executable;
7. if driver-plan apply is used, confirm the elevated helper is found beside the app;
8. close/reopen the setup UI and confirm an existing mirror session can be reattached;
9. verify the ZIP checksum.

The current agent environment cannot perform the Windows publish because it does not contain the .NET SDK and does not have access to the user's local Windows toolchain. Remote CI must not be substituted for this local gate.
