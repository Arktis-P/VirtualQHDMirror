# VirtualQHDMirror

VirtualQHDMirror makes a lower-resolution physical Windows display behave like an interactive view of a higher-resolution virtual desktop.

## Current version

**v0.21.2** is the active checkpoint.

Current behavior includes:

- QueryDisplayConfig-based physical/virtual display discovery
- D3D11 + Desktop Duplication presentation with destination VSync pacing
- Portal/native Windows input on the virtual backing display
- logical cursor topology that hides the backing display during normal pointer traversal
- active Portal cursor-clip recovery
- native drag bridging so windows/files dragged from another physical monitor enter the backing display naturally
- optional **Start with Windows** registration for the current user
- GDI rendering fallback
- v0.14.1 legacy fallback retained under `archive/`

## Build the Windows executable

On Windows with the .NET 8 SDK installed, run:

```text
build-release.cmd
```

The build script creates a self-contained win-x64 package under:

```text
artifacts/release/v0.21.2/win-x64/
```

User entry point:

```text
VirtualQHDMirror.exe
```

Required sidecars in the same folder:

```text
VirtualQHDMirror.Session.exe
VirtualQHDMirror.ElevatedHelper.exe
```

A ZIP package and SHA-256 checksum are generated beside the output directory.

After building, the root `run.cmd` launches the packaged `VirtualQHDMirror.exe`.

Build outputs under `artifacts/` are intentionally not committed to Git.

## Repository layout

```text
VirtualQHDMirror/
├─ archive/
│  ├─ launchers/
│  │  ├─ v0.20/
│  │  ├─ v0.21.0/
│  │  ├─ v0.21.1/
│  │  └─ v0.21.2/
│  ├─ v0.12/
│  ├─ v0.13.1/
│  └─ v0.14.1/
├─ docs/
├─ driver/
├─ scripts/
│  └─ build-release.ps1
├─ src/
│  ├─ VirtualQHDMirror.App/
│  ├─ VirtualQHDMirror.Core/
│  ├─ VirtualQHDMirror.DriverManager/
│  ├─ VirtualQHDMirror.ElevatedHelper/
│  └─ VirtualQHDMirror.Session/
├─ AGENTS.md
├─ build-release.cmd
├─ Directory.Build.props
├─ README.md
├─ THIRD_PARTY_NOTICES.md
├─ VirtualQHDMirror.sln
└─ run.cmd
```

## Development history

The old root development launchers `run-v020.*` through `run-v0212.*` were moved to `archive/launchers/` during the v0.21.2 repository cleanup.

The frozen PowerShell implementations remain under their original version folders for regression comparison.

## Virtual display driver

The current repository still treats the installed development IDD as compatibility infrastructure. The public production driver remains a separate packaging/signing milestone.

See:

- `docs/DRIVER_RELEASE_STRATEGY.md`
- `docs/PUBLIC_DRIVER_INSTALL_CONTRACT.md`
- `docs/PUBLIC_RELEASE_PLAN.md`

## Validation policy

Normal development validation is local-first. GitHub Actions or other hosted CI are not used unless the repository owner explicitly requests a specific remote run.
