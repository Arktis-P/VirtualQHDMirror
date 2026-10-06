# VirtualQHDMirror v0.14.1 archive

This directory is a runnable frozen fallback snapshot of the v0.14.1 implementation from development head `8510dc54081ddf2b6fc70a5442d5e442e1df784c`.

It is preserved so later v0.15+ work can be compared against or rolled back to this known implementation without depending on branch history.

## Recommended launch

- `VirtualQHDMirror.vbs`: normal no-console startup
- `run.cmd`: convenience bridge to the VBS launcher
- `scripts/run-console.cmd`: diagnostic console startup

## Render launchers

- `scripts/run-30fps.cmd`
- `scripts/run-60fps.cmd`
- `scripts/run-120fps.cmd`

## Input fallbacks

- `scripts/run-captured-input.cmd`
- `scripts/run-legacy-input.cmd`

## Included design notes

- `Docs/V014_PORTAL_INPUT.md`
- `Docs/V014_CAPTURED_INPUT.md`
- `Docs/V0141_STARTUP_UI.md`

This archive is frozen. Future development should happen at the repository root, not inside this directory.
