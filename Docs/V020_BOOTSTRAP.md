# v0.20 public-release bootstrap

## Purpose

v0.20 begins the migration from the working PowerShell prototype to a maintainable .NET Windows application.

The migration is incremental. The runnable v0.14.1 implementation remains available at the repository root and under `archive/v0.14.1/` until equivalent functionality is proven in the .NET path.

## Initial structure

- `src/VirtualQHDMirror.App` — WPF application shell and future tray/settings UI.
- `src/VirtualQHDMirror.Core` — display discovery, capture, presentation, input, cursor, configuration, and diagnostics.
- `src/VirtualQHDMirror.DriverManager` — virtual display driver lifecycle and status.
- `VirtualQHDMirror.sln` — solution entry point.

## Migration order

1. QueryDisplayConfig-based display discovery.
2. GDI renderer migration as a compatibility baseline.
3. Portal/native input migration.
4. Configuration persistence and monitor selection UI.
5. GPU capture/presentation backend.
6. Driver lifecycle integration.
7. Installer and external alpha preparation.

## Compatibility rule

Do not remove or rewrite the root PowerShell implementation merely to fit the new architecture. Each subsystem is replaced only after the .NET implementation reaches feature parity and passes regression checks against v0.14.1.
