# v0.22 Stage B3 — Offline lifecycle checkpoint

User confirmed B2 x64 WDK compile + INF verification PASS and B3 unsigned package inventory PASS (NotSigned).

New files:
- scripts/plan-v022-driver-change.ps1 : reads an UNTRUSTED offline JSON snapshot, never real devices. Exact VQM hardware ID/group, one adapter, stopped mirror, 1–4 current/target counts; only a non-executable plan and rollback count. It cannot authorize changes.
- scripts/test-v022-driver-planner.ps1 : 10 isolated PowerShell regression fixtures for correct no-change/scale-to-four and reject ge9, ambiguous devices, invalid counts, active session, disabled device and wrong group.

To validate on Windows:
    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-v022-driver-planner.ps1

Expected: [READY] B3 offline regression cases passed: 10. No Windows changes.

HOLD: B stage is NOT finished. Real VQM driver is unsigned; no install or restart is approved. Production manufacturer/publisher, DriverVer, Microsoft signature and isolated clean Windows runtime lifecycle 1–4 / rollback / reboot / upgrade / uninstall remain needed. Keep ge9 compatibility and physical display settings untouched. No hosted CI.