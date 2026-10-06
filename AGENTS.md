# AGENTS.md

This file contains repository-wide instructions for AI coding agents and automated contributors.

## Required reading

Before making changes, read:

- `Docs/COMMIT_CONVENTION.md`
- `Docs/PUBLIC_RELEASE_PLAN.md` when work relates to the public-release roadmap

## Commit messages

All commits in this repository MUST follow the emoji-prefixed convention documented in `Docs/COMMIT_CONVENTION.md`.

Required shape:

```text
<emoji> <type>: <short imperative summary>
```

Examples:

```text
✨ feat: add automatic display discovery
🐛 fix: restore taskbar click forwarding
⚡ perf: reduce idle mirror frame rate
📝 docs: document setup requirements
```

Do not create vague or emoji-less commit messages unless the repository owner explicitly instructs otherwise.

## Repository baseline

- Current working implementation: repository-root `VirtualQHDMirror.ps1` (**v0.14.1 on the `v0.14` development branch**)
- Recommended no-console launcher: repository-root `VirtualQHDMirror.vbs`
- Convenience bridge: repository-root `run.cmd`
- Auxiliary/diagnostic launchers: `scripts/`
- v0.14 primary input design: `Docs/V014_PORTAL_INPUT.md`
- captured fallback notes: `Docs/V014_CAPTURED_INPUT.md`
- runnable v0.13.1 archive: `archive/v0.13.1/`
- Frozen historical comparison baseline: `prototype/v0.12/`
- Public release roadmap: `Docs/PUBLIC_RELEASE_PLAN.md`

Do not create a new `prototype/v0.xx/` directory for each incremental revision. Keep the active prototype at the repository root unless the repository owner explicitly requests another structure.
