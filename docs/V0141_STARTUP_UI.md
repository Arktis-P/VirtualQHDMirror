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

Use root `run.cmd` for normal stable operation. It launches `archive/v0.14.1/VirtualQHDMirror.vbs`, which starts the PowerShell host through Windows Script Host without showing a console window.

The root `run.cmd` is a convenience bridge to the frozen v0.14.1 launcher and immediately exits.

## Diagnostics

Use `archive/v0.14.1/scripts/run-console.cmd` when a persistent console is useful for regression testing. Fixed-FPS fallback launchers remain in the same archived scripts directory.
