# VirtualQHDMirror v0.20 → v0.21 Handoff

## Status

v0.20 is closed on branch `gpt/public-release-v0.20`.

Use the latest branch HEAD as the v0.21 starting baseline. Do not reopen the rejected pointer-gain experiments.

## v0.20 accepted behavior

- v0.14.1 remains frozen under `archive/v0.14.1/` as runnable fallback/reference.
- .NET 8/WPF setup UI is operational.
- QueryDisplayConfig-based display discovery identifies the development machine's physical and virtual displays without depending on DISPLAY numbers.
- Native `VirtualQHDMirror.Session` can run independently of the setup UI and be reattached after the setup UI is reopened.
- Native session binaries are shadow-copied under `%LOCALAPPDATA%\\VirtualQHDMirror\\runtime\\sessions` so live mirroring does not lock build outputs.
- Portal input parity is accepted for click/right-click/middle/XButton, wheel, drag, taskbar/title-bar and ordinary app interaction.
- Real source pointer movement remains Windows-native.
- Full-frame GDI rendering uses Adaptive approximately 60 / 30 / 15 Hz.
- Cursor-only dirty refresh is allowed as a GDI fallback optimization.
- GUI launcher uses `run-v020.cmd → run-v020.vbs → hidden run-v020.ps1`.

## Rejected experiments

Do not restore these as the primary v0.21 direction:

- full-screen 100/120 Hz GDI rendering merely to make the cursor smoother,
- QHD→FHD pointer gain / 4:3 movement correction,
- corrective `SetCursorPos` on ordinary Portal mouse movement,
- separate click-through cursor overlay window.

They either increased resource cost, introduced perceived lag, or previously caused cursor/window regressions.

## Known v0.20 limitation

The remaining cursor complaint is visual stepping of the software cursor on the 60 Hz physical destination. The actual Windows pointer/input no longer feels delayed after pointer correction was removed.

Treat this as a presentation problem, not an input-speed problem.

## v0.21 first milestone

Implement a GPU-native presentation prototype while retaining GDI as fallback:

1. D3D11-based presentation path; evaluate Windows Graphics Capture vs Desktop Duplication for source acquisition.
2. Present against the physical destination's VSync rather than WinForms timer cadence alone.
3. Keep cursor as an independent visual/composition layer.
4. Preserve cursor position/hotspot in floating-point/sub-pixel coordinates until final composition.
5. Do not change the actual Windows source pointer to compensate for scaling.
6. Measure CPU, GPU, private working set, handles/GDI objects, frame latency and dropped frames.
7. Run 1 h first, then 8 h/overnight soak once the GPU path is stable.
8. Preserve current display discovery, driver planning, session reattachment and Portal input behavior unless a specific regression requires change.

## v0.21 implementation progress

### M1-A — D3D11 cursor presentation prototype

Implemented for the first v0.21 checkpoint:

- D3D11 + DXGI Desktop Duplication source acquisition.
- Flip-model swap-chain presentation using `Present(1)` for destination VSync pacing.
- Cursor captured and composited as an independent GPU texture.
- Cursor destination placement/hotspot remains floating-point until shader rasterization.
- Portal-active D3D11 presentation uses one VSync-paced loop rather than competing full-frame and cursor presents.
- Automatic runtime fallback to the accepted v0.20 GDI renderer.
- `VQM_RENDER_BACKEND=gdi` override for regression comparison.
- D3D11 frame/present, memory, handle, and GDI-object diagnostics.

Detailed design and validation gate: `docs/V021_CURSOR_PRESENTATION.md`.

Remote editor limitation: Windows build/runtime validation is still required before M1-A is considered accepted. Do not remove the GDI fallback.

## Version policy

Next version is `v0.21`. Continue sequentially (`v0.22`, `v0.23`, ...) unless there is an explicit reason to make a larger version jump.
