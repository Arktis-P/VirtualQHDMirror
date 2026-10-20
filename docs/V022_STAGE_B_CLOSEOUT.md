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


## 2026-10-19 decision / clean desktop initial QA

- Owner confirms **Team Burning Arktis** is the intended driver publishing brand. The business is already registered as a Korean sole proprietorship. For the actual certificate and Microsoft Hardware Developer Program, use the **verified legal entity name** approved by the issuing CA and Microsoft; never assume the marketing name alone suffices or overwrite INF manufacturer while legal validation is pending. GitHub profile is source-control identity, not driver signing identity.
- On a fresh Windows desktop with **no virtual display driver**, the owner reports PASS for: (1) `build-v022.cmd` build, (2) `run-v022.cmd` launch, (3) missing-driver / no-virtual-monitor setup state shown correctly. These are user-observed smoke-test results, **not** a driver installation or runtime test.
- Desktop may later be used for further Windows driver validation; require a dedicated recoverable OS environment before unsigned/driver lifecycle experiments. Do not encourage test-signing or disabling Secure Boot on the main OS.
- Product B stage remains held at production signing, authorized exact-device lifecycle implementation, 1–4 monitor runtime validation, and clean install/repair/uninstall verification. No edits to the existing ge9 setup.

### Hardware signing onboarding requirements

Current Microsoft Hardware Developer Program instructions require organization Microsoft Entra ID global admin access, an **EV code-signing certificate** matched to the legal organization, a legal contact who can accept agreements, registration in Partner Center, and completion of any verification questionnaire. A registered sole-proprietor business name does **not** by itself prove EV organizational validation eligibility. Confirm accepted company identity and CA eligibility **before purchasing a certificate**, and follow Microsoft verification requirements rather than guessing.

Microsoft guidance: https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/hardware-program-register
