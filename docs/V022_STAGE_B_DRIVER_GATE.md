# v0.22 Stage B — Driver readiness gate (B1)

This checkpoint implements **read-only, fail-closed identity detection**. It is NOT an automatic driver installer.

- Uses the existing Windows Display-class device probe.
- Matches only exact `ROOT\\VirtualQHDMirrorDisplay` hardware ID (case-insensitive).
- States: Missing / DeviceDetectedUnverified / Ambiguous / ProbeError.
- A matching device is never treated as a verified package or a confirmed owner of an active QHD endpoint.
- The existing ge9 compatibility path is unchanged. No install, UAC, driver restart, registry write, or signing bypass is performed.

## Stage B prerequisites before any write operations

1. Create the VQM-owned IDD source tree directly from the official Microsoft Windows-driver-samples IddSample at a **pinned upstream commit**; preserve MS-PL notices.
2. Replace sample INF identity and placeholders; implement stable 1–4 monitor identities and 2560x1440 @ 60 Hz.
3. Build/validate using WDK on Windows, then obtain a production Microsoft-signed driver catalog for the precise released package.
4. Restrict a future elevated lifecycle helper to signed VQM package and exact VQM device identity, never the compatibility ge9 device.
5. Validate install/upgrade/repair/uninstall, rollback, restart, reboot-required signaling and topology convergence on clean Windows 11.

## Local QA (not yet performed by this agent)

- Pull `gpt/v0.22` and rebuild with `build-v022.cmd`, then run `run-v022.cmd`.
- Stage B readiness should say **VQM-owned production driver not detected** on a ge9-only system. This is the expected result.
- Existing Stage A Ready and mirroring must remain operational.
- Opening the app must not trigger any privileged driver action or UAC prompt.

**Next:** B2 import/productize official IDD source, then B3 signed package/lifecycle operation. Do not use hosted CI.
