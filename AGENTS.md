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

- Current prototype baseline: `prototype/v0.12/`
- Public release roadmap: `Docs/PUBLIC_RELEASE_PLAN.md`
- Preserve the working v0.12 prototype as a comparison baseline while production code is introduced incrementally.
