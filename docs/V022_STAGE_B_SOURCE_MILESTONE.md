# v0.22 Stage B-2 — source baseline checkpoint

## Implemented
- Vendored official Microsoft IddSample code from pinned commit `2dc3fd3a0cc84a2933f2194e7ec0871584979071` into `driver/VirtualDisplay/Source/`.
- Copied MS-PL license and documented derivation/changes in `driver/VirtualDisplay/UPSTREAM_PROVENANCE.md`.
- Changed INF technical ID to `ROOT\VirtualQHDMirrorDisplay`, DeviceGroupId to `VirtualQHDMirrorIddGroup`, diagnostics/device friendly names to VQM.
- Restricted initial prototype to one 2560x1440 @ 60 Hz EDID-less virtual output, with a stable per-slot container GUID.
- No effect on the current compatibility driver, app launcher, auto-start or mirroring.
- No privileged operations or unsigned-driver install paths exposed.

## Outstanding Stage B-2 requirements
1. Windows WDK compile and INF verification; resolve any toolchain/API problems.
2. Refactor original sample to support 1–4 independently tracked monitors and controllable monitor count without physical EDID changes.
3. Confirm stable monitor identities across restart/update and correct QHD mode availability in display discovery.
4. Finish all remaining Microsoft production sample TODOs, frame-processing/teardown resilience, power and error diagnostics.
5. Finalize legal publisher identity and production catalog/signing prerequisites before creating installer/lifecycle tooling.

## User action
**None for this checkpoint.** Do not install this development driver on a production/main PC. Keep testing current v0.22 application against the functioning compatibility driver.

## Verification status
Upstream content taken from the pinned source; code edits anchored to upstream. **Windows WDK build / runtime tests not performed**, so NOT a driver release or Stage B completion. No hosted CI.
