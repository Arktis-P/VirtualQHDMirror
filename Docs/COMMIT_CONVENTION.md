# Commit Message Convention

This repository uses an emoji-prefixed commit convention so humans can identify the purpose of a commit at a glance.

All contributors, including AI coding agents, MUST follow this convention for every commit unless the repository owner explicitly requests a different format.

## Required format

```text
<emoji> <type>: <short imperative summary>
```

Examples:

```text
✨ feat: add automatic display discovery
🐛 fix: restore taskbar click forwarding
⚡ perf: reduce idle mirror frame rate
📝 docs: document public release roadmap
♻️ refactor: separate display detection from rendering
✅ test: add monitor selection regression cases
🔧 chore: update build configuration
🚑 hotfix: recover cursor after source input injection
🚀 release: prepare v0.20 alpha
```

## Emoji / type map

| Emoji | Type | Use |
| --- | --- | --- |
| ✨ | feat | New user-facing feature |
| 🐛 | fix | Bug fix |
| ⚡ | perf | Performance / resource optimization |
| ♻️ | refactor | Internal restructuring without intended behavior change |
| 📝 | docs | Documentation only |
| ✅ | test | Tests, QA fixtures, validation |
| 🔧 | chore | Tooling, build, configuration, maintenance |
| 🚑 | hotfix | Urgent production/blocking fix |
| 🚀 | release | Release preparation, packaging, versioning |
| 💄 | style | UI/visual-only change |
| 🔒 | security | Security hardening or vulnerability fix |
| ⬆️ | deps | Dependency upgrade |
| ⬇️ | deps | Dependency downgrade |
| 🗑️ | remove | Intentional removal of obsolete code/assets |

## Rules

1. Every commit message starts with exactly one primary emoji from the table above.
2. Follow it with the conventional type and a colon.
3. Keep the first line concise and action-oriented.
4. Prefer English commit subjects for consistency with tooling and GitHub history.
5. Do not use vague subjects such as `update files`, `changes`, `work`, or `fix stuff`.
6. Use the emoji that best represents the main purpose of the commit. Do not stack multiple emojis in the subject.
7. If a commit contains mixed work, either split it into focused commits or choose the type that represents the dominant change.
8. Breaking changes must be called out explicitly in the body, for example:
   ```text
   BREAKING CHANGE: configuration file schema changed.
   ```
9. Commit bodies are optional, but should explain non-obvious rationale, compatibility risks, migration steps, or validation results.
10. AI agents must inspect this document before creating commits in this repository.

## AI agent examples

Good:

```text
⚡ perf: cache destination DC for long-running mirror sessions
🐛 fix: handle Unity custom caption buttons with input injection fallback
📝 docs: add driver signing requirements for public release
```

Bad:

```text
updated mirror
fix
✨🐛 stuff
chore: update files
```

## Scope

This convention applies to:

- direct commits to `main`
- feature branches
- AI-generated commits
- merge preparation commits
- release commits

Pull request titles should preferably use the same emoji + type prefix when practical.
