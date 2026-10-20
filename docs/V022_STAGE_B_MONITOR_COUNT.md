# v0.22 Stage B2 — bounded monitor-count startup

Implemented (source only): VQM-owned device-key DWORD `VqmMonitorCount` (default 1, strict 1–4), four reserved stable slot identities, one distinct IddCx monitor object per slot on adapter initialization. Default and target mode stay 2560x1440 @ 60 Hz. Removed unused physical EDID demo data and reject unsolicited physical monitor descriptors. The mode callbacks now avoid writing when the caller provides insufficient buffer capacity.

This **does not** provide runtime hotplug or automatic installer; applying count changes requires a future narrowly-scoped VQM-only privileged helper and controlled restart with rollback.

Validation: source-level anchors and conservative bounds checked; **Windows WDK compilation, INF validation, signing and runtime tests NOT RUN**. Do not install this prototype on the user's working Windows computer. Existing app, startup and compatibility driver unchanged. No GitHub Actions.

Next: Windows isolated WDK build, then verified VQM-specific device configuration updates and 1–4 monitor lifecycle QA.
