# Repository Operation Rules

These rules exist to reduce unnecessary GitHub usage, avoid noisy remote history, and keep validation reproducible on the developer machine.

## 1. GitHub Actions budget

GitHub Actions are **not** the default build/test mechanism for this repository.

Rules:

1. Prefer local commands and reusable local scripts for restore, build, test, lint/static checks, packaging dry-runs, and smoke validation.
2. Do not add a workflow merely because a local environment is temporarily unavailable.
3. Do not run CI on every commit or every push during normal development.
4. Do not repeatedly rerun remote workflows while debugging. Reproduce and fix failures locally first.
5. Artifact uploads from Actions are not used for ordinary checkpoints.
6. GitHub Actions may be introduced only when there is a clear remote-only need, such as:
   - release packaging that must be reproducible from a clean hosted environment,
   - signing/release steps that are intentionally hosted remotely,
   - a final public release gate explicitly approved by the repository owner.
7. If a workflow is eventually required, prefer manual `workflow_dispatch`, release/tag triggers, or another narrow trigger over broad push/PR triggers.
8. Keep workflow jobs minimal. Do not duplicate local validation jobs without a concrete release benefit.

## 2. Local validation first

Before a remote push at a major checkpoint:

- run the relevant local build/test/static checks when the environment supports them,
- record the exact commands and important results,
- fix failures locally,
- do not add GitHub Actions solely to obtain a build result.

If local execution is unavailable, report that limitation explicitly. Do not claim PASS and do not consume remote Actions minutes simply to compensate.

## 3. Commit and checkpoint policy

Two levels are used:

### Normal checkpoint

A normal checkpoint is an internal development boundary such as a finished subtask, refactor, test addition, or documentation update.

- Create as many focused commits as needed.
- Commits should remain reviewable and follow `Docs/COMMIT_CONVENTION.md`.
- A normal checkpoint does **not** require a remote push.

### Major checkpoint

A major checkpoint is a user-visible milestone such as completion of a planned phase, a handoff point, a validated integration state, or a user-requested upload.

- Perform **at most one remote push / branch ref update** for the checkpoint.
- Accumulate all intended commits first.
- Validate the accumulated state locally where possible.
- Push the complete checkpoint once.
- Do not push after each commit.
- If follow-up fixes are found before the checkpoint is reported complete, add more local commits and include them in the same single push.

For GitHub API based work, creating Git objects/commits without moving the branch is equivalent to local accumulation. The single `update_ref` at the end is the checkpoint push.

## 4. Exceptions

Additional pushes are allowed only when:

- the repository owner explicitly asks for an immediate additional push,
- a remote state must be repaired after a failed/incomplete push,
- an urgent hotfix must be published separately for safety or recoverability.

State the reason when using an exception.

## 5. Branch and history hygiene

- Avoid force-push unless explicitly required for recovery.
- Do not create new branches for trivial follow-up work when the active feature branch is appropriate.
- Do not use remote commits as temporary scratch storage.
- Keep the stable prototype and archive baselines intact while public-release migration continues.

## 6. Reporting

At a major checkpoint report:

- branch,
- commits included since the previous push,
- validation performed,
- known unvalidated items,
- exact next checkpoint.

Do not report a GitHub Actions result unless a workflow was intentionally approved and actually ran.
