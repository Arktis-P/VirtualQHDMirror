# VirtualQHDMirror — Start with Windows

## Scope

This checkpoint adds a per-user **Start with Windows** option to the WPF setup application.

It does not automatically start mirroring. Automatic mirror-session startup remains a separate future option.

## Registration

VQM uses the current-user Windows Run key:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
Value: VirtualQHDMirror
```

The value points to the currently running `VirtualQHDMirror.exe`.

No administrator permission is required for this setting.

## Behavior

- Checking **Start with Windows** registers the current executable.
- Unchecking it removes the VQM Run value.
- The UI reads the registry on startup and reflects the current state.
- If startup is already enabled and VQM is later run from a different executable location, the registered path is refreshed to the current executable. This supports the later transition from repository Debug builds to an installed/release executable.

## Direct-start compatibility

The development launcher normally provides `VQM_REPO_ROOT`. Windows login startup does not.

The native-session resolver therefore supports:

1. `VQM_REPO_ROOT` when a development launcher supplied it,
2. `VirtualQHDMirror.Session.exe` beside the application for packaged/release layouts,
3. development repository discovery by walking parent directories from the app/current directory and checking Debug/Release session outputs.

This keeps **Start Mirror** usable when the WPF app was started directly by Windows login rather than through `run-v0212.cmd`.

## Validation

Use `run-v0212.cmd` for the first build after pulling this checkpoint.

Local checks:

1. Open VQM and enable **Start with Windows**.
2. Close and reopen VQM; the checkbox should remain enabled.
3. Confirm the current-user Run value named `VirtualQHDMirror` exists and points to the current app executable.
4. Sign out/in or reboot and confirm VQM opens automatically.
5. Start the native mirror from that automatically launched app and confirm the session executable is found.
6. Disable **Start with Windows** and confirm the Run value is removed.
7. Sign out/in again and confirm VQM no longer launches automatically.

No GitHub Actions or hosted CI are used for this validation.
