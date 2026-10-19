# v0.22 Stage B — External driver onboarding pivot (2026-10-19)

## Decision

The user confirmed: **recommend the same ge9/IddSampleDriver compatibility driver currently used on the original computer**. Abandon the VQM-owned signed driver as the current public installation prerequisite. Keep B2 source and offline validation as historical optional work; do not ship or install its unsigned prototype.

Verified developer links:
- Upstream driver project: https://github.com/ge9/IddSampleDriver
- **Releases:** https://github.com/ge9/IddSampleDriver/releases
- Driver config documentation (option.txt, environment override): https://github.com/ge9/IddSampleDriver

The upstream README expects the config file at C:\IddSampleDriver\option.txt before installation and describes manual installation or a Scoop nonportable package. These flows may use elevated access or certificate trust changes; VQM does **not** execute either flow, download binaries, import certificates, disable Secure Boot or enable test-signing. Third-party signing and security are not guaranteed by VQM. The user must review upstream instructions and trust risks.

## Implemented user flow

1. On a clean PC, v0.22 detects no virtual display; user confirms this already worked with build/run-v022.
2. Virtual Display Setup shows a **Get driver** button alongside Recheck and Setup guide.
3. Clicking Get driver presents the third-party warning and opens the **official ge9 GitHub releases page** only after confirmation.
4. User installs the driver externally on their own responsibility, configures QHD 2560x1440 as documented, returns to VQM and presses **Recheck**.
5. Existing compatibility detection and mirror pairing remain unchanged. No auto downloads or privilege changes.

## Scope and status

- Code modified: MainWindow.xaml, MainWindow.xaml.cs only. No driver binaries or certificate materials bundled.
- Stage B VQM-owned signing/lifecycle release gate becomes **DEFERRED/ARCHIVED**, not falsely marked PASS.
- The revised external-onboarding Stage B can be closed after Windows QA confirms button link, safety warning, existing ge9 detection, QHD readiness and mirror regression. Installation itself remains a third-party user action.
- Windows compile/UI click tests of these edits have **not** been executed by the agent.

## Windows acceptance check

1. Pull gpt/v0.22; build-v022.cmd; run-v022.cmd.
2. On the clean desktop, Virtual Display Setup -> Get driver -> Yes opens https://github.com/ge9/IddSampleDriver/releases.
3. Recheck before installation still says missing; no UAC or driver install occurs from VQM.
4. On the existing ge9 PC, existing Ready and mirroring still work; no auto-download/modified option.txt.
