# Archived development launchers

These folders contain the version-specific development launchers that previously lived in the repository root.

They are preserved for historical comparison and rollback diagnosis only.

| Folder | Former root launcher |
| --- | --- |
| `v0.20/` | `run-v020.*` |
| `v0.21.0/` | `run-v021.*` |
| `v0.21.1/` | `run-v0211.*` |
| `v0.21.2/` | `run-v0212.*` |

The archived PowerShell launchers were written assuming they lived in the repository root. They are intentionally preserved as historical source rather than maintained as the active execution path.

Current development/release packaging uses the root `build-release.cmd` and `scripts/build-release.ps1`. The built user entry point is `VirtualQHDMirror.exe`.
