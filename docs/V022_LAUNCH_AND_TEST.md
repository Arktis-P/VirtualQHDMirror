# v0.22 Windows launcher and verification

## Dedicated preview launch

From the repository root on branch `gpt/v0.22`:

1. Run `build-v022.cmd` after pulling changes to rebuild the v0.22 self-contained package.
2. Double-click `run-v022.cmd` to launch **that package**, not the v0.21.2 package from `run.cmd`.
3. If the v0.22 package is missing/incomplete, `run-v022.cmd` automatically invokes `build-v022.cmd` once before launching.
4. Check the header reads `v0.22 — virtual display setup preview`; open **Virtual Display Setup**, confirm **Ready** and QHD source detection.
5. Do not run a driver-restart test while mirroring. That test requires stopping the active session first and explicit administrator approval.

The output is `artifacts/release/v0.22/win-x64/`, including the application, session sidecar, and elevated helper. Existing `run.cmd`, v0.21.2 artifacts, and the Windows-login auto-start registration are intentionally untouched. A v0.22 UI can attach to an existing v0.21.2 mirror session; seeing a legacy session label does not imply the v0.22 launcher failed if the window header says v0.22.

The launcher does **not** silently change Windows auto-start to the preview build. Change startup registration only after the new version passes validation.

## Verification status

- Script paths and version routing implemented in repository.
- **Windows execution/build not verified by this agent**: run `build-v022.cmd` locally and report any error output.
- A successful generic `dotnet build VirtualQHDMirror.sln -c Release` is not the same as producing the self-contained v0.22 package.
- No hosted CI or GitHub Actions.
