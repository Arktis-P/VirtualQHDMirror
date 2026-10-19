# v0.22 Stage B — closeout status and release handoff

Date: 2026-10-19. Branch: gpt/v0.22. Scope: **VQM-owned indirect display driver**, distinct from the existing ge9 compatibility driver.

## Verified by the Windows operator

- **B2 source-only WDK build**: Visual Studio 2026 + Windows SDK/WDK 28000, DLL and catalog generated, standalone InfVerif PASS.
- **B3 package inventory**: VQM root ID + DIRID 13 PASS; catalog reports **NotSigned**; development-only inventory READY. No installation occurred.
- **B3 offline planner regressions**: all ten tests PASS, including ge9 rejection and ambiguous-device rejection. Tests consume fixtures, **not live Windows device state**.

These results close **B2 build-preparation** and **B3 offline feasibility checkpoints**, not the full stage or a production-driver release.

## Follow-up developed

Read-only stage exit gate: `scripts/check-v022-stage-b-exit.ps1`. It reports release blockers even when local build/inventory tests pass. It **always refuses production approval until independent signing and runtime QA are available**; it does not touch registry, PnP, UAC, driver files or display state.

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-v022-stage-b-exit.ps1
```

Expected currently: HOLD lines and `[BLOCKED] Stage B production exit NOT APPROVED` — because publisher and source DriverVer are draft, CAT is unsigned and actual lifecycle tests are pending. That is **intentional** and not a reason to repair WDK or install an unsigned driver.

## Mandatory inputs for actual Stage B closure

1. **Legal publisher/manufacturer identity** for the production INF and signing account; decide distribution eligibility and Microsoft Hardware Dev Center/EV requirements.
2. **Microsoft-approved production-signed driver package** that targets intended Windows 11 builds and architectures.
3. **Dedicated clean Windows 11 test machine or disposable VM** with an appropriate virtual display/graphics environment. Do not use the daily ge9 machine or disable Secure Boot/test-mode.
4. Implement and test the signed-package-gated elevated lifecycle helper: exact VQM instance only; stop VQM mirrors; config write/readback 1–4; isolated PnP restart; bounded monitor enumeration; failure rollback; upgrade/repair/uninstall and reboot-needed handling.
5. Collect run evidence for one/two/three/four separate QHD@60 monitor outputs; identity stability; multi-GPU; sleep/resume; reboot; upgrade; repair; uninstall; no effect on physical adapters or ge9.

The current unsigned prototype and offline fixture JSON cannot establish these facts. Therefore Stage B **full release exit = HOLD_EXTERNAL_DEPENDENCIES**, not PASS. Keep v0.21.2 stable main and v0.22 compatibility path unchanged.
