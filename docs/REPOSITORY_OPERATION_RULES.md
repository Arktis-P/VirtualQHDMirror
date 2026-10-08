# Repository Operation Rules

These rules exist to reduce unnecessary GitHub usage, avoid noisy remote history, and keep validation reproducible on the developer machine.

## 1. Remote validation prohibition

GitHub Actions and other hosted CI/remote build systems are not permitted as a validation mechanism unless the repository owner explicitly requests the specific remote run in the current task.

Rules:

1. Do not create, enable, dispatch, or rerun GitHub Actions for ordinary validation.
2. Do not use hosted CI, cloud builds, remote test runners, or remote artifact builds as a substitute for unavailable local tooling.
3. Prefer local commands and reusable local scripts for restore, build, test, lint/static checks, packaging dry-runs, and smoke validation.
4. If the current environment cannot execute the Windows/.NET validation locally, report that limitation and wait for the repository owner's local result.
5. Do not add workflow files merely to obtain a build result.
6. Do not trigger an existing workflow through the GitHub UI, API, CLI, or connector unless the repository owner explicitly asks for that run.
7. Release/signing automation also requires explicit owner approval for the specific remote run.
8. Artifact uploads from Actions are not used for ordinary checkpoints.

## 2. Local validation first

Before a remote push at a major checkpoint:

- run the relevant local build/test/static checks when the environment supports them,
- record the exact commands and important results,
- fix failures locally,
- do not add GitHub Actions solely to obtain a build result.

If local execution is unavailable, report that limitation explicitly. Do not claim PASS and do not invoke remote validation to compensate.

## 3. Commit and checkpoint policy

Two levels are used:

### Normal checkpoint

A normal checkpoint is an internal development boundary such as a finished subtask, refactor, test addition, or documentation update.

- Create as many focused commits as needed.
- Commits should remain reviewable and follow `docs/COMMIT_CONVENTION.md`.
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
