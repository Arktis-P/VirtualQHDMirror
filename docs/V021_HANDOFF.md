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

v0.21 launcher: `run-v021.cmd → run-v021.vbs → hidden run-v021.ps1`. Keep the existing `run-v020.*` files unchanged as the v0.20 launcher baseline.

Remote editor limitation: Windows build/runtime validation is still required before M1-A is considered accepted. Do not remove the GDI fallback.

### M1-B — device-feedback cursor correction

Implemented after the first v0.21 device test:

- removed D3D11 cursor placement based on asynchronous `GetCursorInfo`,
- use Desktop Duplication pointer position/visibility and pointer-shape updates,
- do not composite an extra cursor when DXGI reports the separate hardware pointer as not visible,
- moved D3D11 acquisition/composition/`Present(1)` to a dedicated presentation thread,
- set DXGI maximum frame latency to 1,
- keep Portal/input handling on the WinForms UI thread,
- retain automatic GDI fallback.

This checkpoint specifically targets the observed double cursor and the remaining small late-following feel.

## v0.21.0 stable checkpoint

The user completed an approximately 6–7 hour normal-use session on commit `f66bcb23e7b1ecf42ae7901d1d433372225ede15`. The D3D11 cursor/presentation path was reported as substantially more native-feeling than the earlier GDI path, with no significant reaction/frame-drop complaint during that session.

That exact state is frozen at:

- branch: `release/v0.21.0`
- commit: `f66bcb23e7b1ecf42ae7901d1d433372225ede15`
- launcher: `run-v021.cmd`

Do not modify that release branch for v0.21.1 work.

## v0.21.1 — logical cursor topology

v0.21.1 starts from the exact v0.21.0 stable commit and changes pointer topology only.

Goal: keep the virtual QHD source active as the native Windows backing workspace while making normal pointer navigation feel as though only the visible physical displays exist.

Implementation rules:

- exclude the selected backing source from the logical pointer-neighbor list,
- Portal exit routing is based on the physical destination's visible neighbors,
- if the backing source physically sits immediately between the destination and another visible monitor, allow the exit calculation to step through that one hidden backing rectangle,
- if no visible neighbor exists on an edge, keep the pointer at the mirrored destination edge instead of exposing the backing display,
- while Portal is inactive, redirect accidental pointer entry into the selected backing source to the corresponding destination position,
- preserve all v0.21.0 native source-side input behavior,
- do not implement logical window/app transfer in this checkpoint.

Detailed design: `docs/V0211_LOGICAL_CURSOR_TOPOLOGY.md`.

Launcher: `run-v0211.cmd → run-v0211.vbs → hidden run-v0211.ps1`.

## v0.21.1 latest cursor-topology checkpoint

The latest v0.21.1 branch state adds active-Portal clip recovery after diagnostics showed that `ClipCursor` could be released externally while Portal remained active.

Checkpoint:

- branch: `gpt/public-release-v0.21.1`
- commit: `b6b9e9eaae3d2c8c41549e1478c874dbf45e46b6`
- launcher: `run-v0211.cmd`

The user reports that mirrored workspace → adjacent physical display traversal is now working well. Leave this branch unchanged while v0.21.2 is tested.

## v0.21.2 — native drag bridge

Goal: make dragging windows/files from another physical display into the visible mirrored destination land on the backing source without forcing the user to traverse the physical destination and virtual source separately.

Implementation:

- poll global pointer position at 8 ms while Portal is inactive,
- track previous inactive pointer position,
- detect a mouse-button-held transition into the visible destination even when the originating application owns capture,
- start Portal from the destination coordinate without resending button-down,
- detect fast destination overshoot into the backing source and adopt that native drag in place,
- preserve v0.21.1 direct-exit topology and active clip recovery,
- do not add custom window move or file drop logic.

Detailed design: `docs/V0212_NATIVE_DRAG_BRIDGE.md`.

Launcher: `run-v0212.cmd → run-v0212.vbs → hidden run-v0212.ps1`.

## v0.21.2 — Start with Windows option

The WPF setup application now exposes a **Start with Windows** checkbox backed by the per-user Windows Run registry key.

- no administrator permission required,
- startup registration follows the current app executable path,
- existing enabled registration is refreshed after the app moves,
- Windows-login startup launches the app only; automatic mirroring is a separate future option,
- direct app startup can locate the native session without relying on launcher-provided `VQM_REPO_ROOT`.

Details: `docs/V0212_WINDOWS_STARTUP.md`.

## Version policy

Current patch line is `v0.21.2`. Preserve `release/v0.21.0` as the stable rollback point. Continue patching sequentially within v0.21 when the work is a small compatibility/usability improvement; use v0.22 only for a broader new milestone.
