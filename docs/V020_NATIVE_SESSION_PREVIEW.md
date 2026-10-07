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


## Build-output lock verification

Verified on the development Windows machine:

- native .NET mirror session remained active,
- `run-v020.cmd` was launched again,
- the complete solution rebuilt successfully while the session was still running,
- the setup UI reopened and reattached to the existing native session.

This confirms the shadow-copy session runtime isolates live session DLL locks from `src/.../bin/Debug`.

The temporary process-detection/App-only build fallback is no longer needed. `run-v020.cmd` now always performs a normal full solution build before reopening the setup UI.

## GUI development launcher

The v0.20 development entry point now mirrors the stable launch experience:

- run-v020.cmd immediately delegates to run-v020.vbs,
- VBScript starts run-v020.ps1 with the PowerShell window hidden,
- a small WinForms preparation dialog remains visible while the local solution is built,
- successful builds launch the WPF VirtualQHDMirror.exe directly, without dotnet run and without a persistent console,
- build output is written to %LOCALAPPDATA%\VirtualQHDMirror\logs\launcher-v020-build.log,
- build/startup failures are reported with a GUI message box.

## Final v0.20 cursor policy: v0.14.1 parity

Direct comparison against the frozen v0.14.1 Portal path showed that the stable implementation already has the preferred interaction model:

- no pointer gain or scale compensation after Portal entry,
- the real Windows pointer moves natively on the QHD source,
- one WinForms timer performs Portal polling and full-frame rendering together,
- Adaptive falls back to 33 ms and 66 ms while idle,
- any detected input calls MarkInput and immediately returns Adaptive to 16 ms (~60 Hz),
- cursor composition remains frame-synced with the mirrored desktop.

Therefore the temporary v0.20 experiments that used a separate 100 Hz cursor-only timer and micro-movement gain have been removed. The .NET preview now follows the stable pointer/cursor behavior while retaining the .NET architecture, shadow-copy session runtime, reduced per-frame result allocation, improved startup diagnostics, and removed first-click/wheel fixed sleeps.

This means an overlay/status reading of "Adaptive / 30 fps" while idle does not describe active pointer movement: as soon as the stable or .NET Portal detects motion, the active interval returns to approximately 60 fps.


## GUI launcher existing-instance behavior

The GUI launcher now checks for an already-running VirtualQHDMirror setup process before building:

- if an existing setup window is available, it is restored/activated and no build is attempted,
- this prevents the running setup process from locking App/Core/DriverManager binaries during a redundant rebuild,
- if the process exists but has no usable window yet, the launcher reports that state instead of attempting a conflicting build,
- dotnet CLI output is forced to English for redirected build logs/error messages to avoid mixed-codepage mojibake.
