# VirtualQHDMirror

VirtualQHDMirror mirrors a virtual 2560x1440 Windows desktop onto a physical 1920x1080 monitor and lets the physical FHD monitor act as an interactive portal to the virtual QHD desktop.

## Current development baseline

- Version: v0.14.1
- Branch: v0.14.1
- Target topology: 1 | 2 | 3
  - 1: laptop display
  - 2: physical FHD mirror
  - 3: virtual QHD source
- Default input mode: Portal
- Diagnostic fallbacks: Captured and Legacy
- Cursor renderer: dedicated 120 Hz overlay by default
- Full-frame modes: Adaptive / 30 / 60 / experimental 120 FPS

## v0.14.1 Portal input

The default v0.14.1 path removes the manual capture/release workflow.

When the system pointer enters display 2, VirtualQHDMirror maps that position to display 3 and lets Windows handle the interaction natively on the source. The source cursor is rendered back onto the mirror.

A temporary source-bound cursor constraint is used only while the pointer is inside the portal so native taskbar/title-bar/hover behavior remains stable. It is released automatically at a portal edge. There is no normal Ctrl+Alt+F11 release step.

The entry edge is blocked until the pointer has moved inward, preventing an immediate bounce back to the monitor it came from.

When the mirrored pointer reaches an edge that has a real Windows monitor on the other side, the constraint is released and the pointer is placed just across the matching display-2 boundary. This preserves normal 1 -> 2 -> 3 and 3 -> 2 -> 1 navigation.

## Why not MagSetInputTransform as the default

MagSetInputTransform was evaluated because its coordinate-routing model matches the desired UX. Microsoft requires UIAccess for that API, and Windows treats UIAccess as a highly trusted accessibility mechanism. v0.14.1 therefore does not make UIAccess a runtime requirement.

## Run

- run.cmd: Portal input, adaptive FPS, 120 Hz cursor overlay
- scripts/run-30fps.cmd: Portal input, 30 FPS
- scripts/run-60fps.cmd: Portal input, 60 FPS
- scripts/run-120fps.cmd: Portal input, experimental 120 FPS
- scripts/run-captured-input.cmd: earlier captured experiment
- scripts/run-legacy-input.cmd: v0.13.1-style input path

## Runnable v0.13.1 archive

For immediate daily use, the exact v0.13.1 baseline is preserved at:

archive/v0.13.1/run.cmd

## Hotkeys

- Ctrl+Alt+F12: exit
- Ctrl+Alt+F11: no-op in Portal mode
- Ctrl+Alt+F10: diagnostic overlay
- Ctrl+Alt+F9: render mode cycle (Adaptive -> 30 -> 60 -> 120 -> Adaptive)

## v0.14.1 validation focus

1. Move from laptop display 1 into mirror display 2 without clicking to capture.
2. Move across display 2 and verify the source cursor tracks the mirror.
3. Push left from display 2 and return to display 1 without a hotkey.
4. Push right from display 2 and enter virtual display 3 without a hotkey.
5. Verify grouped taskbar thumbnails and hover remain stable.
6. Verify minimize/maximize/close, drag and resize.
7. Verify Unity/Electron/Chromium custom title bars.
8. Verify wheel and XButtons.
9. Verify Arrow, I-beam, Hand and resize cursor alignment.


## v0.14.1 latency work

The software cursor is no longer tied to the full QHD-to-FHD StretchBlt cadence. A separate click-through transparent overlay polls and redraws source cursor position/shape at 120 Hz by default. The physical output is still 60 Hz, so this does not create 120 visible refreshes; it reduces the chance that a displayed refresh uses an older cursor position.

Use -CursorHz 60, 120, or 240 for comparison. 120 Hz is the default balance.

The 120 FPS full-frame mode is experimental. On a 60 Hz physical monitor it can reduce frame age slightly, but it cannot display more than the monitor refresh rate and it increases GDI copy load.


## v0.14.1 startup UI

Startup now uses a small WinForms loading window while the embedded mirror engine is prepared. Startup errors are shown in a GUI error dialog instead of waiting for console input.

A dedicated no-console launcher is the intended normal-use entry point; the CMD launchers remain useful for diagnostics and fixed-FPS testing.
