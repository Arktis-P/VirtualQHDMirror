# AGENTS.md

This file contains repository-wide instructions for AI coding agents and automated contributors.

## Required reading

Before making changes, read:

- `docs/COMMIT_CONVENTION.md`
- `docs/REPOSITORY_OPERATION_RULES.md`
- `docs/PUBLIC_RELEASE_PLAN.md` when work relates to the public-release roadmap
- `docs/DRIVER_ONBOARDING_PLAN.md` when work relates to virtual display setup, packaging, installation, or recovery
- `docs/DRIVER_RELEASE_STRATEGY.md` and `driver/VirtualDisplay/PRODUCTION_REQUIREMENTS.md` before modifying public driver source or packaging

## Commit messages

All commits in this repository MUST follow the emoji-prefixed convention documented in `docs/COMMIT_CONVENTION.md`.

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

## GitHub resource and push policy

The repository-wide resource rules in `docs/REPOSITORY_OPERATION_RULES.md` are mandatory.

In particular:

- Do not use GitHub Actions or hosted CI for validation unless the repository owner explicitly requests a specific remote run.
- Prefer local build, test, static validation, and reusable local scripts.
- If local validation is unavailable, report that limitation and wait for the repository owner's local result; do not substitute remote CI.
- Normal checkpoints may create as many focused local commits as needed.
- A major checkpoint permits at most **one remote push/ref update**. Accumulate commits locally, validate them, then push once.
- Do not use repeated remote pushes as a substitute for local iteration.
- When operating through GitHub API tools, create commits/trees first and update the branch ref only once at the end of the major checkpoint.

Exceptions require an explicit user request or an urgent recovery/hotfix where delaying the remote update would create more risk.

## Repository baseline

- Stable runnable fallback: `archive/v0.14.1/`
- Current package version: `v0.21.2`
- Current release build entry: `build-release.cmd` → `scripts/build-release.ps1`
- Current root runtime entry: `run.cmd` → built `artifacts/release/v0.21.2/win-x64/VirtualQHDMirror.exe`
- Historical development launchers: `archive/launchers/`
- Public-release v0.20 baseline: `main` / `gpt/public-release-v0.20` (closed)
- Stable v0.21.0 checkpoint: `release/v0.21.0`
- v0.21.1 logical-cursor checkpoint branch: `gpt/public-release-v0.21.1`
- Active v0.21.2 branch: `gpt/public-release-v0.21.2`
- v0.21 cursor-presentation handoff: `docs/V021_HANDOFF.md`
- v0.21.1 logical cursor topology: `docs/V0211_LOGICAL_CURSOR_TOPOLOGY.md`
- v0.21.2 native drag bridge: `docs/V0212_NATIVE_DRAG_BRIDGE.md`
- v0.21.2 Windows startup: `docs/V0212_WINDOWS_STARTUP.md`
- v0.21.2 release packaging: `docs/V0212_RELEASE_PACKAGING.md`
- Public-release .NET code: `src/`
- Active documentation: `docs/`
- Historical runnable baselines: `archive/v0.12/`, `archive/v0.13.1/`, `archive/v0.14.1/`
- v0.14 primary input design: `docs/V014_PORTAL_INPUT.md`
- captured fallback notes: `docs/V014_CAPTURED_INPUT.md`
- Public release roadmap: `docs/PUBLIC_RELEASE_PLAN.md`

Archived version directories are frozen snapshots. Do not rewrite their internal layout merely to match current repository conventions.

Keep `archive/v0.14.1/` intact as the legacy rollback reference. The active root `run.cmd` now targets the locally built v0.21.2 package; do not restore version-specific development launchers to the repository root.


## Public driver policy

- The ge9/IddSampleDriver family is development compatibility infrastructure only; do not vendor its binary/certificate into the public package.
- The production driver source target is the Microsoft official IddSample lineage under MS-PL, productized under a VQM-owned identity.
- Do not import driver source without recording the exact upstream commit and preserving required notices.
- Do not introduce public test-signing, Secure Boot disabling, manual root-certificate trust, or Device Manager setup as the normal user path.
