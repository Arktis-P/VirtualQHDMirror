# v0.22 Stage A — Topology convergence checkpoint

## Implemented
- After the compatibility elevated helper exits successfully, the app polls display discovery and the driver setup evaluator instead of relying on the previous 1.5-second sleep.
- Poll every 750 ms for up to 20 seconds; require at least the planned number of active QHD virtual sources for two consecutive observations.
- Keep mirror, driver apply, and recheck actions disabled while a privileged change and convergence check are running.
- If the driver restart succeeds but the sources do not appear, report a **partial result** rather than claiming successful setup. Do not automatically repeat the privileged restart.
- Existing stable v0.21.2 mirror / Portal / cursor paths remain unchanged.

## Windows 11 local QA — NOT YET RUN
1. Build with `dotnet build VirtualQHDMirror.sln -c Release` on Windows 11; fix build errors before shipping.
2. With a working ge9 compatibility driver and a single physical FHD destination, restart the driver using the UI. Confirm Windows UAC and at least one active 2560×1440 virtual source.
3. With two FHD destinations and two requested QHD sources, confirm readiness is not reported until **two** QHD sources exist; allow selection of sources manually.
4. Temporarily prevent the second source from activating and confirm 20-second timeout and actionable Recheck prompt without a second driver restart.
5. Cancel UAC and verify no convergence polling begins.
6. Restart with an existing mirror session and verify the operation remains blocked.
7. Unplug or reconfigure a physical display while polling and verify the app remains responsive and never claims readiness from fewer than the planned QHD sources.
8. Verify v0.21.2 D3D11 / Portal / drag bridge / cursor regressions on the original monitor setup.

## Follow-up
- Stage A compatibility-driver recovery QA must be performed on the actual Windows host.
- Guided install remains informational: publicly redistributable, production-signed VQM IDD is a Stage B requirement.
- Do not enable GitHub Actions / hosted CI for this repository.
