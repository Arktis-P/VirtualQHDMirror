# VirtualQHDMirror

VirtualQHDMirror mirrors a virtual 2560×1440 Windows desktop onto a physical 1920×1080 monitor and forwards input back to the virtual desktop.

## Current baseline

- Version: **v0.13.1 prototype**
- Platform: Windows 11
- Source display: 2560×1440 virtual display
- Destination display: 1920×1080 physical display
- Rendering: Win32 GDI `StretchBlt`
- Input forwarding: Win32 messages + targeted native `SendInput` fallbacks
- Native pointer fallback: virtual-desktop absolute coordinates
- DPI awareness: Per-Monitor DPI Aware V2
- Default render mode: adaptive 60 / 30 / 15 FPS

v0.13.1 focuses on interaction reliability before the larger public-release rewrite.

## v0.13.1 input changes

- The mirror window opts out of DWM Peek/Aero Peek so taskbar thumbnail selection should not expose the physical desktop underneath the mirror.
- Taskbar and custom-chrome fallbacks move the Windows pointer through absolute virtual-desktop `SendInput`, similar to a remote-desktop input injector, with `SetCursorPos` retained only as a fallback.
- Standard minimize, maximize/restore, and close buttons are identified through `WM_NCHITTEST` first and executed through `WM_SYSCOMMAND`.
- Unity/Electron/custom title bars that report their controls as client area still use native input fallback.
- Source cursor hotspot/size scaling from v0.13 is retained.

## Run

The current implementation is at the repository root.

1. Prepare a 2560×1440 virtual display.
2. Keep the physical destination monitor at 1920×1080.
3. Run `run.cmd`.

Files:

- `VirtualQHDMirror.ps1`: current implementation
- `run.cmd`: adaptive mode
- `scripts/run-30fps.cmd`: fixed 30 FPS
- `scripts/run-60fps.cmd`: fixed 60 FPS
- `prototype/v0.12/`: frozen historical comparison baseline

The obsolete `prototype/v0.13/` working copy is no longer used; current revisions live at the repository root.

## Hotkeys

- `Ctrl + Alt + F12`: exit
- `Ctrl + Alt + F11`: toggle input forwarding
- `Ctrl + Alt + F10`: toggle diagnostic overlay
- `Ctrl + Alt + F9`: cycle render mode

## Known prototype limitations

- Display selection still relies primarily on 2560×1440 / 1920×1080 resolution matching.
- A compatible virtual display must already exist.
- Same-session Windows input means there is still only one real OS cursor; native fallbacks temporarily operate that cursor on the virtual display.
- Custom title bars can vary by application and still require compatibility testing.
- Rendering still uses GDI rather than a GPU-native capture/presentation path.
- No installer, driver lifecycle management, tray UI, crash recovery, telemetry, or automatic updates yet.

## Roadmap

See `Docs/PUBLIC_RELEASE_PLAN.md` for the path from this prototype to a public release.
