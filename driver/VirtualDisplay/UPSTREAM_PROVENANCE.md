# Official Microsoft IddSample source provenance

- Upstream: `microsoft/Windows-driver-samples`, `video/IndirectDisplay/`
- Pinned commit: `2dc3fd3a0cc84a2933f2194e7ec0871584979071`
- License: Microsoft's MS-PL, copied verbatim to `driver/VirtualDisplay/LICENSE-MS-PL.txt`
- Included: the full IddSample driver project, the companion sample application, solution and upstream README. Original source copyright comments are preserved.
- Reference: https://github.com/microsoft/Windows-driver-samples/tree/2dc3fd3a0cc84a2933f2194e7ec0871584979071/video/IndirectDisplay

## First VQM prototype changes
- Driver INF: exact VQM hardware ID `ROOT\VirtualQHDMirrorDisplay` and unique UMDF group `VirtualQHDMirrorIddGroup`.
- Display label and adapter diagnostics are VQM-branded (the legal INF manufacturer remains an unresolved sample placeholder).
- Prototype exposes one EDID-less virtual monitor at 2560x1440 @ 60 Hz, rather than sample physical EDIDs or unrelated resolutions.
- Random monitor container ID replaced with stable product GUID namespace and connector-index suffix.
- 2–4 independent monitor management and runtime count configuration NOT YET IMPLEMENTED. The original sample app still uses its Microsoft sample device identity and is for reference only.

## Security, distribution and validation
This is a **non-installable development scaffold**, not a signed release driver. INF `DriverVer`, manufacturer/publisher, catalog signing and additional production TODOs remain unresolved. No compiler, WDK, hardware or Windows driver install test has been run by this agent. Do NOT install on the user's main Windows system, distribute as a driver, or add it to `build-v022.cmd` / `run-v022.cmd`. Existing ge9-compatible mirror setup stays unchanged.

Next: Windows WDK compile on an isolated driver-development machine, production driver TODO cleanup, 1–4 monitor lifecycle and stable IDs, then a signed package with clean Windows 11 install / repair / uninstall QA.

## B2 monitor slots checkpoint
- Device hardware-key `VqmMonitorCount` accepts 1–4 (defaults to one when absent/invalid); the adapter reserves four IDs and reports separate IddCx monitor objects at startup.
- Legacy demo EDIDs and parsing were removed. Runtime hotplug, the privileged settings writer, transactional restart/rollback, production INF metadata and signing are still pending.
