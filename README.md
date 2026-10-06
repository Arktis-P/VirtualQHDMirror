# VirtualQHDMirror

VirtualQHDMirror is a Windows prototype that mirrors a virtual 2560×1440 desktop onto a physical 1920×1080 monitor and forwards input back to the virtual desktop.

## Current baseline

- Version: **v0.13 prototype**
- Platform: Windows 11
- Source display: 2560×1440 virtual display
- Destination display: 1920×1080 physical display
- Rendering: Win32 GDI `StretchBlt`
- Input forwarding: Win32 window messages + targeted `SendInput` fallbacks with native source interaction sessions
- DPI awareness: Per-Monitor DPI Aware V2
- Default render mode: adaptive 60 / 30 / 15 FPS

The current prototype was developed for a configuration using an IDD virtual display and an external FHD monitor. It is not yet a general-purpose installer and does **not** install or configure the virtual display driver automatically.

## Run

The current v0.13 prototype source is stored under `prototype/v0.13/`. The previous `prototype/v0.12/` baseline is preserved unchanged for comparison.

1. Prepare a 2560×1440 virtual display.
2. Keep the physical destination monitor at 1920×1080.
3. Run `prototype/v0.13/run.cmd`.

Alternative launchers:

- `run-30fps.cmd`: fixed 30 FPS
- `run-60fps.cmd`: fixed 60 FPS

## Hotkeys

- `Ctrl + Alt + F12`: exit
- `Ctrl + Alt + F11`: toggle input forwarding
- `Ctrl + Alt + F10`: toggle diagnostic overlay
- `Ctrl + Alt + F9`: cycle render mode

## Known prototype limitations

- Display selection currently relies primarily on 2560×1440 / 1920×1080 resolution matching.
- A compatible virtual display must already exist.
- Some custom title bars and Windows shell surfaces still rely on native input fallback sessions.
- Cursor behavior is not yet complete for every custom cursor type.
- Rendering still uses GDI rather than a GPU-native capture/presentation path.
- No installer, driver lifecycle management, tray UI, crash recovery, telemetry, or automatic updates yet.

## Roadmap

See [Docs/PUBLIC_RELEASE_PLAN.md](Docs/PUBLIC_RELEASE_PLAN.md) for the path from this prototype to a public release.

## Repository status

This repository currently contains the **prototype baseline**, not a production-ready public release.