# v0.14.1 Startup UI

## Loading

The PowerShell bootstrap shows a compact WinForms loading dialog while the embedded C# engine is prepared.

Stages currently shown:

- Preparing mirror engine
- Compiling mirror engine
- Detecting displays and starting Portal

The loader closes immediately before the mirror application enters its normal message loop.

## Errors

Startup failures use a WinForms error dialog. The previous blocking console Read-Host path is removed.

## No-console launcher

Use `VirtualQHDMirror.vbs` for normal operation. It starts the PowerShell host through Windows Script Host without showing a console window.

The root `run.cmd` is only a convenience bridge: it launches `VirtualQHDMirror.vbs` and immediately exits.

## Diagnostics

Use `scripts/run-console.cmd` when a persistent console is useful for development. Fixed-FPS CMD launchers remain available for regression testing.
