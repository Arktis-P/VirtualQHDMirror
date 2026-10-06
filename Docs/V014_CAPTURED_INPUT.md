# v0.14 Captured Input Engine

## Goal

Replace the growing list of per-surface mouse forwarding exceptions with a same-session native input path inspired by remote-desktop input architecture.

Chrome Remote Desktop separates video, cursor state and input. Its Windows input injector uses `SendInput`, including absolute virtual-desktop mouse positioning. VirtualQHDMirror keeps those concepts but does not need the networking or protocol layers.

## State model

### OFF

- Mirror renders normally.
- Mouse input is not forwarded.
- Ctrl+Alt+F11 changes the state to ARMED.

### ARMED

- The real cursor is on the physical destination monitor.
- The mirror does not synthesize hover/move events into the source.
- A click on the mirror maps the FHD point to QHD and acquires CAPTURED mode.
- Mouse wheel over the mirror can also acquire CAPTURED mode.

### CAPTURED

- The real Windows cursor is moved to the mapped source point with absolute virtual-desktop `SendInput`.
- `ClipCursor` confines it to the virtual source monitor.
- Windows handles subsequent mouse movement/buttons/wheel/drag natively.
- The destination continues showing the source cursor through software composition.
- Ctrl+Alt+F11 releases capture.

## Release behavior

1. Read the current source cursor position.
2. Release `ClipCursor`.
3. Map QHD source coordinates back to the FHD destination.
4. Move the real cursor to that mapped destination point.
5. Return to ARMED.

This avoids the v0.13 behavior where the cursor repeatedly jumped between source and destination during one shell interaction.

## Legacy fallback

`-InputMode legacy` retains the v0.13.1 forwarding path:

- client-area PostMessage
- non-client hit testing
- caption command handling
- taskbar/custom-chrome native fallback sessions

Use `scripts/run-legacy-input.cmd` only for comparison and regression recovery.

## Expected improvements

Because Windows itself owns the source-side interaction while captured, no special-case forwarding should be required for:

- taskbar grouped thumbnail selection
- shell hover/flyout lifetime
- caption buttons
- title-bar drag
- resize borders
- Unity/Electron custom chrome
- application cursor changes

## Known tradeoffs / follow-up

- Windows has one OS cursor in this same-session design. CAPTURED mode intentionally transfers it to the virtual display.
- QHD source coordinates cover a larger pixel space than the FHD mirror. Native relative pointer travel may therefore feel slower than a client-coordinate remote-desktop cursor. Device testing should determine whether v0.14 needs a low-level/raw-input scaling layer later.
- UAC secure desktop and anti-cheat environments are outside the prototype support scope.
- If the process terminates abnormally during capture, Windows normally releases process-owned state, but explicit release paths remain mandatory in normal shutdown.

## QA checklist

- [ ] First mirror click enters CAPTURED and activates the intended source target.
- [ ] Taskbar grouped thumbnails stay open and are selectable.
- [ ] Mirror stays visible during Aero Peek/thumbnail hover.
- [ ] Standard minimize/maximize/close works.
- [ ] Unity title-bar controls work.
- [ ] Electron/Chromium custom title-bar controls work.
- [ ] Window drag and resize work without custom forwarding.
- [ ] Left/right/middle/XButton work.
- [ ] Wheel works.
- [ ] Cursor shape/hotspot matches click point.
- [ ] Ctrl+Alt+F11 releases capture.
- [ ] Released cursor lands at the corresponding mirror position.
- [ ] Ctrl+Alt+F12 exits and releases cursor confinement.
- [ ] Legacy launcher still starts.
