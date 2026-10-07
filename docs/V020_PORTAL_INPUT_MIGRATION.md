# v0.20 Portal Input and Cursor Migration

## Scope

This checkpoint moves the core v0.14.1 Portal geometry/native-pointer path and frame-synced cursor composition into normal .NET source.

It does not yet replace the stable PowerShell mirror session.

## Portal geometry preserved

`VirtualQHDMirror.Core.Input.PortalInputGeometry` preserves the v0.14.1 rules:

- half-pixel destination↔source mapping,
- source/destination clamping,
- 16 px entry-edge detection,
- 40 px blocked-edge release margin,
- source-edge exit thresholds,
- adjacent-monitor exit validation,
- destination-side pointer restoration.

The geometry is independent of WPF/WinForms.

## Native pointer injection

`WindowsPointerInjector` owns:

- `GetCursorPos`,
- source `ClipCursor`,
- clip release,
- virtual-desktop absolute `SendInput`,
- `SetCursorPos` fallback,
- native mouse-button injection,
- native wheel injection.

This is the same fundamental mechanism used by the stable Portal path.

## Portal state controller

`PortalInputController` owns:

- active/inactive state,
- blocked entry edge,
- last source cursor position,
- begin portal,
- edge-unblock,
- adjacent-monitor exit,
- destination restoration,
- activity notification for Adaptive render timing.

The future mirror session can therefore use native Windows input on the source screen instead of reimplementing app-specific click behavior.

## Cursor composition

`GdiCursorCompositor` is now called from `GdiMirrorRenderer` after a successful frame stretch.

It preserves:

- source-screen cursor visibility check,
- source bounds check,
- cursor handle shape cache,
- hotspot extraction,
- monochrome mask sizing,
- scaled cursor dimensions,
- scaled hotspot offset,
- `DrawIconEx` composition into the destination DC.

This restores the frame-synced cursor path needed once the real pointer has moved onto the virtual source.

## Still pending before runtime switch

- dedicated .NET mirror-session window/process,
- exact destination HWND positioning,
- DWM Peek isolation,
- timer lifecycle,
- Ctrl+Alt+F12 exit,
- render-mode hotkey,
- session process discovery/reattach,
- end-to-end Portal regression test.

Once those pieces are connected, v0.20 can expose a .NET-session preview without removing the stable v0.14.1 fallback.
