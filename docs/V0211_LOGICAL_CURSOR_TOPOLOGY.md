# VirtualQHDMirror v0.21.1 Logical Cursor Topology

## Goal

Keep the selected virtual QHD source active as the real Windows backing workspace, while making normal pointer navigation feel as though only the user's visible physical displays exist.

v0.21.0 remains frozen at branch `release/v0.21.0`.

v0.21.1 changes cursor topology only. It does not yet implement logical window/app movement between the visible physical destination and its virtual backing source.

## Model

For one mirror pair:

- **Destination**: the physical display the user sees.
- **Backing source**: the virtual QHD display that actually owns the mirrored applications and native Windows input.
- **Visible displays**: every active display except the selected backing source.

The backing source stays in the real Windows desktop topology, but is removed from VQM's logical pointer topology.

Example:

```text
Windows topology:       1 | 2 | 3
                         physical | physical | virtual backing

VQM pointer topology:   1 | 2
```

While the pointer is visually on 2, native Windows input continues to operate on 3 exactly as in v0.21.0.

## Exit routing

When Portal input reaches an edge of the backing source, VQM maps that edge back to the same edge of the physical destination and checks the logical visible topology.

- If a visible monitor is directly adjacent to that destination edge, the pointer exits directly to it.
- The selected backing source is never accepted as a logical exit target.
- If the backing source itself physically occupies the space immediately beyond the destination edge, VQM can step through that one hidden backing rectangle and look for a visible monitor on its far side.
- If no visible logical neighbor exists, the Portal remains active and the pointer stays on the mirrored destination edge.

This preserves native source-side clicks, drag, title-bar, taskbar, and application behavior. It does not reintroduce per-control input mapping.

## Backing-source guard

A low-cost 60 Hz pointer guard runs while the session is active.

If Portal input is inactive but the real Windows pointer nevertheless lands inside the selected backing source, VQM maps that source position to the corresponding physical destination position and redirects the pointer there. Once the destination window receives the pointer, normal Portal entry resumes.

This guard exists to make the backing source difficult to enter accidentally through the ordinary Windows desktop topology.

## Scope and limits

This checkpoint intentionally does not:

- remove or disable the backing source at the Windows display-driver level,
- change Windows Display Settings,
- move windows/apps logically between visible monitors,
- rewrite the existing native Portal input path,
- add special taskbar/title-bar mappings.

The next separate milestone can address logical window transfer, for example making a window moved from physical display 1 toward visible destination 2 actually land on backing source 3.

## Transition race correction

First device testing exposed an intermittent exit race: a pointer leaving the mirrored destination could reach the adjacent physical monitor correctly, or bounce back to the destination/backing pair.

The race happened because Portal state could reset before an asynchronous absolute pointer move became visible to the backing guard. The guard could then observe the old source position and redirect it back to the destination.

The correction is intentionally narrow:

- topology entry/exit/guard warps use synchronous cursor positioning first, with the previous absolute input move retained only as fallback;
- destination-window mouse events can open Portal only when the current global cursor position is actually inside the physical destination;
- continuous pointer movement inside the mirrored workspace is unchanged;
- D3D11 presentation is unchanged.

This is not the rejected continuous cursor-correction experiment. Synchronous positioning is used only at discrete topology transitions.

## Validation

Use `run-v0211.cmd`.

Primary checks:

1. Build and start successfully.
2. Enter the mirrored physical destination from another physical monitor.
3. Work normally on the mirrored destination; clicks, drag, title bar, taskbar, wheel, XButtons, and application-specific controls must remain unchanged from v0.21.0.
4. Move back toward an adjacent physical monitor. The pointer should leave the mirrored destination directly for that physical monitor without visibly traversing the backing source/destination twice.
5. Move toward a destination edge with no visible physical neighbor. The pointer should remain at that visible edge rather than entering the hidden backing source.
6. If the Windows pointer is placed on the backing source outside Portal mode, the guard should redirect it to the corresponding destination position.
7. Run a normal multi-hour session and compare CPU/GPU/frame pacing with `release/v0.21.0`; the topology guard must not materially regress the stable v0.21.0 presentation behavior.
