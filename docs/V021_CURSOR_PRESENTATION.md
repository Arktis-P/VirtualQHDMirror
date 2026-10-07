# VirtualQHDMirror v0.21 Cursor Presentation Prototype

## Goal

v0.21 treats the remaining cursor complaint as a presentation problem. The Windows pointer itself remains native and Portal input geometry is unchanged.

## Prototype architecture

The session now attempts this path first:

1. Find the DXGI output whose device name matches the selected virtual source display.
2. Create a D3D11 device on that adapter.
3. Acquire desktop updates with DXGI Desktop Duplication.
4. Copy the latest desktop image into a shader-readable GPU texture.
5. Capture the current Windows cursor shape separately and keep cursor placement in floating-point destination coordinates.
6. Composite desktop + cursor in a pixel shader.
7. Present a flip-model swap chain with `Present(1)`, so visible cursor updates are paced by the destination VSync instead of only by WinForms timer cadence.

The existing GDI renderer remains intact as the automatic fallback.

## Cursor path

The D3D11 path does not apply pointer gain and does not call `SetCursorPos` to compensate for scaling.

Cursor position, hotspot, width, and height are scaled in floating-point. Rounding is deferred to rasterization in the D3D11 composition pass.

While Portal input is active, the cursor polling loop becomes the D3D11 presentation loop. The normal full-frame timer does not submit a second competing present during that period. Outside Portal mode, the existing adaptive full-frame cadence remains in control.

## Fallback and recovery

D3D11 is attempted automatically.

If D3D11 initialization fails, VirtualQHDMirror starts with the existing GDI path. If the D3D11 path fails while running, the session disposes the GPU renderer and attaches the existing GDI renderer to the same destination window.

For regression testing, set:

```text
VQM_RENDER_BACKEND=gdi
```

before launching to skip the D3D11 prototype.

## Diagnostics

The native session log records:

- selected presentation backend,
- D3D11 initialization/fallback exceptions,
- D3D11 presents,
- copied desktop frames,
- Desktop Duplication acquire timeouts,
- instantaneous and smoothed render/present cost,
- process private bytes and working set,
- handle count,
- GDI object count.

Log path remains:

```text
%LOCALAPPDATA%\VirtualQHDMirror\logs\native-session.log
```

## Dependencies

The prototype uses Vortice.Windows packages for Direct3D 11, DXGI, and D3DCompiler bindings. The session project pins all three packages to 3.8.3 for this checkpoint.

## Launcher separation

`run-v020.cmd`, `run-v020.vbs`, and `run-v020.ps1` remain unchanged as the v0.20 launcher baseline. v0.21 development/testing uses `run-v021.cmd → run-v021.vbs → run-v021.ps1` so launcher labels and logs match the active version.

## Validation status

Static/API review is complete for this checkpoint. A Windows build and runtime validation could not be executed from the current remote editing environment.

The required local gate is:

```powershell
dotnet restore .\VirtualQHDMirror.sln
dotnet build .\VirtualQHDMirror.sln --configuration Debug --nologo
.\run-v021.cmd
```

Runtime checks:

1. Confirm the session log says `Presentation backend=D3D11/DesktopDuplication/VSync`.
2. Move the pointer slowly and quickly across the mirrored destination and compare stepping against v0.20.
3. Verify text/link/resize cursor shapes.
4. Verify click/right-click/middle/XButton, wheel, drag, taskbar, title-bar, and Unity-window interaction.
5. Force `VQM_RENDER_BACKEND=gdi` and confirm the v0.20 fallback behavior still works.
6. Record CPU/GPU/private working set/handles/GDI objects for at least 10 minutes before the 1 h soak.
7. After functional stability, run the planned 1 h then 8 h/overnight soak.

## Known prototype limits

- Desktop Duplication is the first acquisition backend. Windows Graphics Capture remains the next fallback candidate if a target virtual-display driver does not support duplication reliably.
- Swap-chain resizing is intentionally not implemented in this checkpoint because the mirror window is fixed to the selected physical destination bounds. A destination-mode change causes D3D11 fallback instead of in-place swap-chain recreation.
- HDR/10-bit color and rotated-source handling are not the target of this first cursor milestone.
- Per-process GPU utilization is not yet collected by the internal diagnostics; use Task Manager/GPUView or another external counter during the first soak.
