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

## Diagnostics

CMD launchers remain available while the prototype is under development. A separate no-console launcher is used for normal operation.
