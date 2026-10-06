# VirtualQHDMirror

VirtualQHDMirror mirrors a virtual 2560×1440 Windows desktop onto a physical 1920×1080 monitor and forwards interaction back to the virtual desktop.

## Current development baseline

- Version: **v0.14**
- Development branch: **`v0.14`**
- Platform: Windows 11
- Source display: 2560×1440 virtual display
- Destination display: 1920×1080 physical display
- Rendering: Win32 GDI `StretchBlt`
- Default input mode: **Captured native input**
- Fallback input mode: v0.13.1 legacy message/injection engine
- DPI awareness: Per-Monitor DPI Aware V2
- Default render mode: adaptive 60 / 30 / 15 FPS

## v0.14 — captured native input

v0.14 changes the default interaction model.

When the mirror is **ARMED**, the first click on the physical mirror maps that point to the virtual QHD display and transfers the real Windows cursor to the source display. The cursor is then confined to the virtual display with `ClipCursor`.

While **CAPTURED**, VirtualQHDMirror no longer emulates ordinary mouse interactions with `PostMessage`. Windows receives the user's real mouse input directly on the virtual display, so these surfaces use their native behavior:

- Windows taskbar and grouped-window thumbnails
- minimize / maximize / restore / close
- title-bar drag and resize
- Unity / Electron / Chromium custom chrome
- hover menus and shell flyouts
- application-defined cursor shapes
- normal click, drag, wheel, XButton and double-click behavior

The mirror continues to render the source cursor separately using its source position, cursor bitmap size and hotspot.

### Releasing capture

Press **Ctrl+Alt+F11** while captured.

The source cursor position is converted back into the corresponding physical-mirror position, `ClipCursor` is released, and the real cursor returns to the physical monitor. Input remains **ARMED**, so another click on the mirror captures it again.

When already released/ARMED, **Ctrl+Alt+F11 does nothing**. In captured mode F11 is intentionally a release-only hotkey so it cannot accidentally disable mirror input.

### Why this differs from Chrome Remote Desktop

Chrome Remote Desktop has separate client and host machines/sessions. VirtualQHDMirror runs both displays inside the same Windows session, so there is only one real OS cursor.

v0.14 therefore keeps the CRD concepts that fit this project—native Windows input, virtual-desktop absolute positioning, cursor state separated from the video frame—but transfers the one real cursor to the virtual display while interaction is captured.

## Run

- `run.cmd`: adaptive rendering + captured input
- `scripts/run-30fps.cmd`: fixed 30 FPS + captured input
- `scripts/run-60fps.cmd`: fixed 60 FPS + captured input
- `scripts/run-legacy-input.cmd`: v0.13.1-style input path for regression comparison

Requirements:

1. Prepare a 2560×1440 virtual display.
2. Keep the physical destination monitor at 1920×1080.
3. Run `run.cmd`.

## Hotkeys

- `Ctrl + Alt + F12`: exit
- `Ctrl + Alt + F11`: release captured input; no action while already ARMED
- `Ctrl + Alt + F10`: diagnostic overlay
- `Ctrl + Alt + F9`: render mode cycle

## Repository layout

- `VirtualQHDMirror.ps1`: current implementation
- `run.cmd`: current default launcher
- `scripts/`: alternate launchers
- `Docs/V014_CAPTURED_INPUT.md`: v0.14 input architecture and QA checklist
- `prototype/v0.12/`: frozen historical baseline

Do not create a separate prototype directory for every incremental version.

## Current validation focus

The first device test should verify:

1. click a grouped taskbar icon, then select one of its thumbnails
2. hover across grouped-window thumbnails without exposing the physical desktop
3. minimize / maximize / close standard Win32 windows
4. use Unity/Electron custom title-bar controls
5. drag/resize windows
6. verify I-beam, hand and resize cursors line up with the interaction point
7. press Ctrl+Alt+F11 and confirm the pointer returns to the corresponding mirror position

The legacy launcher remains available if captured mode exposes a regression.
