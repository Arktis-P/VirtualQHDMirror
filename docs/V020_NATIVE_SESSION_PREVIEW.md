# v0.20 Native .NET Mirror Session Preview

## Purpose

This checkpoint connects the migrated .NET GDI renderer, frame-synced cursor compositor and Portal input core into a separate native session process.

The stable v0.14.1 PowerShell engine remains the default.

## Engine choices

The v0.20 setup window now exposes:

- `Stable v0.14.1 Portal` — existing validated fallback and default.
- `.NET GDI + Portal (preview)` — new native .NET session path.

Only one mirror engine may run at a time.

## Native session architecture

`VirtualQHDMirror.Session.exe` is a separate WinForms process.

This preserves the useful lifecycle already proven by the compatibility bridge:

- setup UI may close while mirroring continues,
- reopening v0.20 can discover the session by exact window/process identity,
- Stop Mirror can close the discovered session,
- the session owns rendering/input cleanup independently from the setup UI.

## Native session behavior

The preview session currently includes:

- exact source/destination rediscovery by device name,
- borderless topmost destination window,
- no-activate/tool-window style,
- DWM Peek exclusion,
- GDI HALFTONE StretchBlt renderer,
- frame-synced cursor composition,
- Portal pointer transfer using native virtual-desktop SendInput,
- source cursor clip while Portal is active,
- adjacent-monitor Portal exit,
- Adaptive / 30 / 60 / experimental 120 timing policy,
- Ctrl+Alt+F9 render-mode cycle,
- Ctrl+Alt+F12 clean session exit,
- five-consecutive-render-failure shutdown guard.

## Why this is a preview

The stable v0.14.1 path has more field time and remains the fallback.

Before the .NET session becomes default it needs direct regression testing for:

- pointer visibility and hotspot,
- hover,
- left/right/middle/X buttons,
- wheel,
- drag,
- taskbar,
- Explorer,
- browser/Electron,
- Unity/custom title bars,
- multi-monitor edge transitions,
- close/reopen setup UI and session reattachment,
- render-mode hotkey,
- clean stop/exit.

## Test procedure

1. Stop any existing stable mirror session.
2. Run `run-v020.cmd`.
3. Confirm source/destination are correct.
4. Select `.NET GDI + Portal (preview)`.
5. Click `Start Mirror`.
6. Verify the physical FHD shows the virtual QHD workspace.
7. Move the pointer over the mirror and verify the composed cursor remains visible.
8. Test normal desktop interaction and window/taskbar controls.
9. Close only the v0.20 setup window; mirroring should remain.
10. Reopen `run-v020.cmd`; it should attach to the existing .NET preview PID.
11. Use `Stop Mirror` or Ctrl+Alt+F12 and confirm the pointer is returned and the mirror window closes.

If the preview fails, Ctrl+Alt+F12 is the primary clean escape path. The stable v0.14.1 engine remains selectable afterward.


## Startup diagnostics

The preview launcher now treats process creation and mirror readiness as separate states.

`run-v020.cmd` first builds the complete solution so the App, Session and helper binaries are from the same checkout/configuration.

When Start Mirror launches the .NET preview:

1. the controller starts `VirtualQHDMirror.Session.exe`,
2. waits up to 5 seconds for the exact `VirtualQHDMirror .NET Session` top-level window,
3. only then marks the mirror as running,
4. if the process exits early, maps its startup exit code to a user-facing error,
5. writes detailed native-session diagnostics to:

`%LOCALAPPDATA%\VirtualQHDMirror\logs\native-session.log`

This prevents a fast-crashing child process from appearing as a successful Start Mirror operation.


WinForms UI-thread exceptions are configured to propagate to the session's outer startup handler, so renderer/window initialization failures are logged instead of being silently swallowed by the child process.


## Engine selector synchronization

When the setup UI is reopened while a mirror session is already running, the engine selector is synchronized to the **actual attached session** before it is locked:

- existing v0.14.1 session → `Stable v0.14.1 Portal`,
- existing native session → `.NET GDI + Portal (preview)`,
- no running session → preserve the user's current/default selection,
- ambiguous simultaneous sessions → do not claim either engine as authoritative.

This prevents the disabled selector from showing the default Stable value while the UI is actually attached to a native preview session.


## Build-output lock isolation

A running native session must not lock the development build output, because the setup UI is intentionally allowed to close and reopen while mirroring continues.

The runtime now uses a shadow-copy model:

1. build `VirtualQHDMirror.Session` normally,
2. copy the complete session output into
   `%LOCALAPPDATA%\VirtualQHDMirror\runtime\sessions\<unique-id>\`,
3. launch the copied `VirtualQHDMirror.Session.exe`,
4. leave `src/VirtualQHDMirror.Session/bin/.../` free for subsequent builds,
5. keep session runtime directories intact during this preview phase so cleanup can never damage a live detached session. A later housekeeping step will remove directories only when process ownership can be proven absent.

Compatibility for sessions started before this change:

- `run-v020.cmd` checks whether `VirtualQHDMirror.Session.exe` is already running,
- if so, it builds only the setup App (plus its Core/DriverManager compile dependencies),
- the App project no longer has build-only ProjectReferences to Session/ElevatedHelper,
- therefore reopening the setup UI does not try to overwrite a DLL held by the existing session.

Once that old direct-from-bin session is stopped and a new preview session is started, the shadow-copy runtime path takes over and full solution builds can coexist with the running mirror.
