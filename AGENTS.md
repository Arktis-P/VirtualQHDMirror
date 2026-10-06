# AGENTS.md

This file contains repository-wide instructions for AI coding agents and automated contributors.

## Required reading

Before making changes, read:

- `Docs/COMMIT_CONVENTION.md`
- `Docs/REPOSITORY_OPERATION_RULES.md`
- `Docs/PUBLIC_RELEASE_PLAN.md` when work relates to the public-release roadmap
- `Docs/DRIVER_ONBOARDING_PLAN.md` when work relates to virtual display setup, packaging, installation, or recovery

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

## GitHub resource and push policy

The repository-wide resource rules in `Docs/REPOSITORY_OPERATION_RULES.md` are mandatory.

In particular:

- Do not add or enable GitHub Actions as the default validation path.
- Prefer local build, test, static validation, and reusable local scripts.
- GitHub Actions require explicit justification and should normally be reserved for release/signing/package work that cannot reasonably be validated locally.
- Normal checkpoints may create as many focused local commits as needed.
- A major checkpoint permits at most **one remote push/ref update**. Accumulate commits locally, validate them, then push once.
- Do not use repeated remote pushes as a substitute for local iteration.
- When operating through GitHub API tools, create commits/trees first and update the branch ref only once at the end of the major checkpoint.

Exceptions require an explicit user request or an urgent recovery/hotfix where delaying the remote update would create more risk.

## Repository baseline

- Current working implementation: repository-root `VirtualQHDMirror.ps1` (**v0.14.1 stable prototype**)
- Public-release migration branch: `gpt/public-release-v0.20`
- Recommended no-console launcher for the stable prototype: repository-root `VirtualQHDMirror.vbs`
- Convenience bridge: repository-root `run.cmd`
- Auxiliary/diagnostic launchers: `scripts/`
- v0.14 primary input design: `Docs/V014_PORTAL_INPUT.md`
- captured fallback notes: `Docs/V014_CAPTURED_INPUT.md`
- runnable v0.14.1 archive: `archive/v0.14.1/`
- runnable v0.13.1 archive: `archive/v0.13.1/`
- Frozen historical comparison baseline: `prototype/v0.12/`
- Public release roadmap: `Docs/PUBLIC_RELEASE_PLAN.md`

Do not create a new `prototype/v0.xx/` directory for each incremental revision. Keep the stable PowerShell prototype intact while the .NET public-release path is built alongside it. Do not remove the prototype as the normal fallback until the replacement reaches functional parity and passes regression validation.
