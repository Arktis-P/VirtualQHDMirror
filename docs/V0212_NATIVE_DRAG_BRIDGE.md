# VirtualQHDMirror v0.21.2 Native Drag Bridge

## Goal

Make the visible physical destination behave like a real adjacent monitor when the user drags a window, file, folder, or other native drag payload from another physical display.

The user should experience:

```text
visible:  physical display A  ->  mirrored destination B
actual:   physical display A  ->  virtual backing source C
```

The Windows display topology remains unchanged. VQM only bridges the pointer/Portal transition while a native drag is already in progress.

## Why v0.21.1 is not enough

Ordinary pointer entry into the mirrored destination is handled by the VQM destination window's mouse events.

During an external drag, however, the originating application or Windows move/size loop can own mouse capture. In that state the VQM window may not receive `MouseMove` even though the global pointer visually crosses the destination. The real pointer can therefore traverse the physical destination and eventually enter the backing source without Portal taking ownership.

## v0.21.2 bridge

While Portal is inactive, VQM polls the global pointer at 8 ms and tracks the previous inactive position.

If any native mouse button is already down:

1. **Visible destination entry**
   - previous global pointer was outside the physical destination,
   - current global pointer is inside the physical destination,
   - VQM starts Portal from that destination-local coordinate,
   - the real pointer is moved to the corresponding backing-source coordinate.

2. **Backing-source overshoot**
   - a very fast drag crosses the destination between polls and the real pointer is already in the backing source,
   - previous global pointer was outside the backing source,
   - VQM starts Portal from the current source position instead of treating it as an accidental idle entry.

The originating application keeps its existing drag capture. VQM does not synthesize a new button-down for this bridge.

## Expected behavior

### Window/title-bar drag

Dragging a normal window from another physical display toward the visible mirrored destination should move the real cursor onto the backing source as soon as the visible destination boundary is crossed. Windows' existing move loop should then continue moving the window on the backing source, which is rendered on the physical destination.

### File/folder drag

Dragging from Explorer or another application should keep the original native/OLE drag operation alive. Once the pointer is bridged, hit testing and eventual drop occur on the backing source.

### Return path

The v0.21.1 logical cursor topology remains authoritative for leaving the mirrored workspace. A drag can move back toward a visible physical neighbor and Portal exit routing should return directly to that neighbor.

## Guardrails

- Do not implement separate window-moving APIs.
- Do not implement custom file transfer/drop.
- Do not resend mouse-button down for an already-running external drag.
- Do not change D3D11 presentation.
- Keep v0.21.1 active-Portal clip recovery.
- The bridge only starts when a mouse button is physically down and a cross-display transition is observed.
- A click that starts while already inside the mirrored destination continues to use the existing destination-window input path.

## Diagnostics

Search `native-session.log` for `DragBridge`.

Important events:

- `DragBridge destination-entry detected`
- `DragBridge activated from visible destination`
- `DragBridge backing-source overshoot detected`
- `DragBridge activated after destination overshoot`

The existing `CursorDiag` entries remain enabled for Portal/clip diagnosis.

## Local validation

Use `run-v0212.cmd`.

Primary checks:

1. Build/start.
2. Ordinary pointer 1 ↔ mirrored destination behavior remains the same as v0.21.1.
3. Drag a normal top-level window from a physical monitor into the mirrored destination.
4. Drag a file/folder from Explorer into a folder/app shown on the mirrored destination.
5. While still holding the drag, reverse direction and verify direct return to the adjacent physical display.
6. Verify normal click/drag operations begun inside the mirrored destination still work.
7. Confirm no repeated 2 → 3 → 2 bounce and no regression of active Portal clip recovery.

No GitHub Actions or hosted CI are used for this validation.
