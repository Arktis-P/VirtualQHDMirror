# v0.20 GDI Renderer Migration

## Purpose

This checkpoint extracts the proven v0.14.1 GDI frame path from the PowerShell runtime-compiled C# block into the normal .NET source tree.

It does **not** switch the public v0.20 Start Mirror button to the new backend yet.

The verified v0.14.1 Portal engine remains the runtime default until input/cursor/session-window responsibilities are migrated.

## Migrated behavior

The new `VirtualQHDMirror.Core.Rendering` layer contains:

- source display DC creation via `CreateDC("DISPLAY", sourceDevice)`,
- destination window DC attachment via `GetDC(hwnd)`,
- `HALFTONE` stretch mode,
- `SetBrushOrgEx(0, 0)`,
- full-frame `StretchBlt(..., SRCCOPY)`,
- source and destination dimension validation,
- render cost timing,
- v0.14.1-compatible smoothed render-cost EMA,
- v0.14.1 render interval policy:
  - experimental 120 FPS → 8 ms,
  - 60 FPS → 16 ms,
  - 30 FPS → 33 ms,
  - Adaptive active → 16 ms,
  - Adaptive normal idle → 33 ms,
  - Adaptive long idle → 66 ms,
- deterministic DC cleanup through `IDisposable`.

## Intentional exclusions

Migrated in the following Core checkpoint:

- Portal geometry/native pointer state is now under `Core.Input`,
- frame-synced software cursor composition and cursor-shape cache are now attached to the GDI renderer.

Still not migrated into a runnable native session:

- mirror-window ownership,
- captured/legacy input fallbacks,
- diagnostic overlay,
- hotkeys,
- DWM Peek isolation.

Those concerns are kept separate so the renderer can later be replaced by a GPU backend without dragging input/window logic with it.

## Threading contract

The destination window DC is attached on a specific managed thread.

Render and destination-detach/dispose calls must stay on that same thread while a destination DC is attached.

This matches the intended future UI/session-window design and avoids treating the HWND DC as an arbitrary worker-thread rendering target.

## Runtime status

Current default:

`v0.20 UI → v0.14.1 stable PowerShell Portal engine`

Migration target:

`v0.20 UI → native .NET mirror session → GDI backend → native .NET Portal input`

Later:

`v0.20 UI → native .NET mirror session → D3D11/WGC backend → native .NET Portal input`

The new GDI backend is therefore a compatibility/reference backend, not the final performance backend.

## Validation before switching runtime default

Before replacing the stable-engine bridge:

- build succeeds on Windows .NET 8,
- exact source device renders to an exact destination HWND,
- destination resize/path loss returns a controlled failure,
- repeated attach/detach does not leak DC handles,
- 30/60/adaptive timing matches v0.14.1 behavior,
- Portal input has been migrated,
- cursor composition has been migrated or an explicit temporary fallback is chosen.

Until those are true, `Start Mirror` stays on v0.14.1.
