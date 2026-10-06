# VirtualQHDMirror v0.12 archive

This directory preserves the historical v0.12 PowerShell baseline that predates the public-release .NET migration.

It is kept under `archive/` for regression comparison and recovery. The files remain runnable with the original local launchers, but active development must not modify this snapshot.

## Included behavior

- output DC reuse
- adaptive 60 / 30 / 15 FPS
- taskbar interaction
- click / drag / wheel / XButton forwarding
- title-bar movement and double-click
- selected custom-title-bar native input fallback
- source cursor composition

## Launchers

- `run.cmd`: adaptive
- `run-30fps.cmd`: fixed 30 FPS
- `run-60fps.cmd`: fixed 60 FPS

A virtual 2560×1440 display and physical 1920×1080 display must already be configured.
