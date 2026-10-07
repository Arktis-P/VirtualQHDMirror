# Virtual Display Driver Onboarding Plan

## Purpose

A public VirtualQHDMirror release cannot assume that users already know how to install, enable, configure, repair, or remove a virtual display driver.

The stable v0.14.1 mirroring core remains intact while driver onboarding is built around it.

## Target first-run experience

The public application should guide the user through this sequence:

1. Check whether a compatible virtual display adapter/monitor already exists.
2. Check whether a usable QHD virtual mode is active.
3. If setup is already valid, continue without changing the system.
4. If the driver is missing, explain what is required and provide the supported installation path.
5. After installation, re-scan the display topology.
6. Confirm that the virtual display can expose the required mode.
7. Let the user select/confirm the physical destination when automatic selection is ambiguous.
8. Start mirroring only after the preflight is green.

The user should not need to know a `DISPLAYn` number.

## Driver strategy gate

Before bundling or automating a driver installation, the exact driver strategy must be fixed.

For any candidate IDD package verify:

- upstream project and maintained source,
- license,
- redistribution permission,
- modification/distribution obligations,
- driver signing state and signing requirements,
- Windows versions supported,
- install/update/uninstall mechanism,
- whether silent or guided installation is legally and technically supported,
- whether 2560×1440 can be configured without unsafe EDID/registry hacks,
- reboot requirements,
- rollback/recovery behavior.

Do not ship an unsigned/test-signed driver to general users and do not require disabling Windows security features.

## Staged implementation

### Stage D1 — Read-only preflight

Implement first.

- enumerate displays/adapters with stable device identity,
- distinguish likely virtual and physical targets,
- detect whether a compatible virtual display is present,
- detect active resolution/topology,
- present actionable status messages,
- make no driver or registry changes.

### Stage D2 — Public setup guide

Create an end-user setup path for the selected driver:

- prerequisites,
- download/source provenance,
- installation steps,
- UAC/reboot expectations,
- how VQM confirms successful installation,
- common failure messages,
- repair/reinstall steps,
- uninstall steps.

The guide must match the exact driver/version strategy selected for release.

### Stage D3 — DriverManager integration

After licensing/signing/install behavior is confirmed:

- detect package/driver state,
- start guided install or installer integration,
- re-scan after install,
- surface driver health,
- support repair and clean uninstall,
- avoid modifying physical monitor EDID.

### Stage D4 — Installer integration

Only after D1-D3 are validated:

- application + approved driver install,
- upgrade,
- rollback,
- uninstall,
- fresh-PC validation.

## Safety rules

Do not make the public flow depend on:

- CRU automation,
- physical monitor EDID override,
- forced GraphicsDrivers registry deletion,
- test-signing mode,
- Secure Boot disabling,
- manual Device Manager surgery as the normal path.

A manual fallback guide may exist for diagnostics, but it is not the target public UX.

## Validation matrix

Minimum driver onboarding validation:

- clean Windows 11 machine,
- already-installed compatible driver,
- missing driver,
- disabled virtual adapter,
- reinstall after uninstall,
- reboot after install,
- sleep/resume,
- Windows display topology change,
- multiple physical monitors,
- driver present but required virtual mode missing.

## Current status

Display discovery and real mirror handoff were validated on the current Windows development machine.

The next implemented layer is a **read-only DriverManager probe**:

- enumerate present Windows Display-class devices with SetupAPI,
- identify IddSampleDriver-compatible device descriptions / hardware IDs,
- correlate installed-device evidence with active virtual displays,
- read the ge9-style machine configuration path from `IDD_SAMPLE_DRIVER_CONFIG` or `C:\IddSampleDriver\option.txt`,
- report configured virtual-monitor count,
- verify that 2560×1440 exists in the configured modes,
- make no installation, registry, driver-state, or security changes.

Automatic installation remains blocked behind the driver strategy/signing gate.


## Multi-monitor capacity planning

DriverManager now computes a read-only full-coverage requirement for the current FHD profile.

Let:

- `P` = eligible physical 1920×1080 targets,
- `V` = active virtual 2560×1440 sources,
- `C` = configured IddSampleDriver monitor count when readable.

The app reports:

- independent active pair capacity: `min(P, V)`,
- additional active QHD sources needed for full 1:1 coverage: `max(0, P - V)`,
- whether the current driver config count is lower than `P`,
- whether config already requests enough monitors but fewer QHD virtual displays are currently active.

"Full coverage" means **only if the user wants every eligible physical FHD display to have an independent virtual QHD workspace**. VQM does not change the config or assume that this is always the desired mode.


## Safe configuration/reload plan

The current ge9-style compatibility driver loads `option.txt` from its device-add path, not continuously while it is running. Therefore changing the monitor-count line alone is not sufficient; the IDD device must be restarted/re-enumerated before the new count can take effect.

VQM now builds a **read-only configuration plan** before any privileged operation.

Plan states:

- `NotApplicable`: no eligible physical FHD target requires a QHD source,
- `NoChangeRequired`: configured count and active QHD count already cover the current full-coverage target,
- `RestartRecommended`: config count is already sufficient but fewer required QHD sources are active,
- `ReadyToIncrease`: the config count is below the physical FHD full-coverage target,
- `Blocked`: device/config identity is ambiguous or unsafe.

Safety rules:

- compatibility driver count is capped at 4 because the upstream sample recommends a small count below 5,
- VQM never reduces an already-higher configured monitor count automatically,
- exactly one recognized IddSampleDriver-compatible Display-class device is required before proposing a restart,
- `2560×1440` must already be present in the driver's configured mode list,
- no physical EDID or GraphicsDrivers registry modification is involved,
- active mirror sessions must be stopped before any future driver restart operation.

The next privileged implementation should use the detected **device instance ID** and Windows' built-in `pnputil /restart-device <instance-id>` operation rather than Device Manager automation. PnPUtil device restart is available on supported modern Windows versions and requires elevation.

The privileged write/restart helper is intentionally a separate next step; the normal VQM UI should not run elevated for routine mirroring.


## Elevated compatibility helper

The normal v0.20 WPF process remains non-elevated. Privileged compatibility-driver changes are delegated to `VirtualQHDMirror.ElevatedHelper.exe` only when a plan requires them.

The helper is intentionally narrow:

- accepts only the machine-configured `IDD_SAMPLE_DRIVER_CONFIG` path or `C:\IddSampleDriver\option.txt`,
- accepts only `ROOT\IddSampleDriver\...` device instances during the current compatibility phase,
- accepts monitor counts from 1 through 4,
- re-validates that the config is readable and contains 2560×1440,
- creates `option.txt.vqm.bak` before changing the first-line monitor count,
- restarts only the exact detected instance with `pnputil /restart-device`,
- restores the previous config when restart fails after a write,
- never edits physical-monitor EDID or GraphicsDrivers registry state.

The main UI disables Apply while a legacy mirror session is active. UAC is requested only for the helper process, not for routine VQM operation.

Current development limitation: after the helper exits successfully, v0.20 waits briefly and re-runs display discovery. A later recovery phase should replace the fixed delay with an explicit display-topology change watcher and bounded convergence loop.
