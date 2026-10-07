# VirtualQHDMirror

VirtualQHDMirror makes a lower-resolution physical Windows display act as an interactive view of a higher-resolution virtual desktop.

The repository currently has two execution tracks:

- **v0.14.1 stable fallback** — frozen PowerShell implementation under `archive/v0.14.1/`.
- **v0.20 public-release migration** — .NET/WPF implementation under `src/`, currently focused on display discovery and topology assessment.

## Run

### Stable v0.14.1

Double-click:

```text
run.cmd
```

The root launcher hands off to the frozen `archive/v0.14.1/` implementation. The archived files are not edited during v0.20 development.

### v0.20 development shell

Double-click:

```text
run-v020.cmd
```

This runs:

```text
src/VirtualQHDMirror.App/VirtualQHDMirror.App.csproj
```

and opens the v0.20 display setup UI. It identifies active Physical / Virtual / Unknown displays, lets the user choose a Virtual QHD ↔ Physical FHD pair, and can start that pair through the frozen v0.14.1 Portal engine while the .NET mirror engine is still being migrated.

`run-v020.cmd` requires the .NET 8 SDK during development.

## Repository layout

```text
VirtualQHDMirror/
├─ archive/
│  ├─ v0.12/      # historical PowerShell baseline
│  ├─ v0.13.1/    # earlier runnable fallback
│  └─ v0.14.1/    # current stable runnable fallback
├─ docs/           # active design, roadmap, workflow and migration notes
├─ driver/         # VQM-owned production IDD design/source area
├─ src/
│  ├─ VirtualQHDMirror.App/
│  ├─ VirtualQHDMirror.Core/
│  ├─ VirtualQHDMirror.DriverManager/
│  ├─ VirtualQHDMirror.ElevatedHelper/
│  └─ VirtualQHDMirror.Session/
├─ AGENTS.md
├─ Directory.Build.props
├─ README.md
├─ VirtualQHDMirror.sln
├─ run.cmd
└─ run-v020.cmd
```

Archived snapshots keep their original internal layout for reproducibility. Active development uses lowercase `docs/`.

## Current v0.20 focus

The v0.20 public-release migration currently includes display preflight, driver lifecycle planning, and a native .NET GDI renderer backend:

- enumerate active Windows display paths,
- classify Physical / Virtual / Unknown,
- retain Internal / External as placement metadata rather than a hard eligibility rule,
- identify FHD physical candidates and QHD virtual candidates,
- avoid arbitrary pairing when multiple candidates exist,
- prepare for later driver setup, persistent pairing and multi-monitor sessions,
- host the v0.14.1-compatible GDI frame renderer under `VirtualQHDMirror.Core.Rendering` while the stable Portal engine remains the runtime default.

See `docs/PUBLIC_RELEASE_PLAN.md` and `docs/V020_DISPLAY_DISCOVERY.md`.

## Stable fallback notes

The v0.14.1 fallback still contains the proven Portal input path, GUI startup, cursor composition and Adaptive / 30 / 60 / experimental 120 FPS render modes.

Its auxiliary launchers are intentionally kept only inside:

```text
archive/v0.14.1/scripts/
```

so the active repository root does not duplicate frozen fallback files.


## Public virtual display driver

The public release will not bundle the currently installed ge9 compatibility binary.

The production target is a VQM-owned IddCx driver derived from Microsoft's official IddSample source, with unique VQM identity, stable 1–4 monitor lifecycle and Microsoft production signing.

See `docs/DRIVER_RELEASE_STRATEGY.md`.


## Native .NET mirror preview

The setup UI now includes an opt-in `.NET GDI + Portal (preview)` engine.

It runs in `VirtualQHDMirror.Session.exe` so mirroring can survive setup-window closure and be reattached later. The stable v0.14.1 engine remains the default until the preview passes regression testing.

See `docs/V020_NATIVE_SESSION_PREVIEW.md`.


### Development session isolation

Native preview sessions run from a shadow copy under `%LOCALAPPDATA%\VirtualQHDMirror\runtime\sessions` rather than directly from `src/.../bin`. This allows the setup app and solution to be rebuilt/reopened while a mirror session remains active.


During the preview phase, shadow-copy session directories are intentionally not auto-deleted at startup; safe stale-runtime cleanup will be added separately so a detached live session is never partially deleted.
