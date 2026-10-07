# Public Driver Install / Upgrade / Recovery Contract

## Scope

This document defines the user-facing contract for the future signed VQM-owned virtual display driver.

It deliberately separates:

- routine non-elevated VQM operation,
- privileged driver lifecycle operations,
- compatibility-driver development support.

## First install

Target flow:

1. User starts the VQM installer.
2. Installer verifies Windows version/architecture and package integrity.
3. Installer determines whether the VQM driver is already installed.
4. If missing, one UAC-approved driver-install operation is performed.
5. The exact VQM device is created/enabled.
6. VQM waits for display topology convergence.
7. VQM verifies:
   - hardware/device identity,
   - at least one VQM virtual display is active,
   - 2560×1440 is available,
   - requested logical-monitor count is satisfied.
8. App starts non-elevated.
9. DisplayDiscovery pairs virtual source(s) with user-selected/eligible physical target(s).

Normal users must not need Device Manager, certificate import, CRU, registry editing, or `DISPLAYn` knowledge.

## Install primitive

The final implementation may use Windows-supported package/device installation APIs or PnPUtil behind a narrowly scoped elevated helper/installer.

Rules:

- exact VQM package/device identity only,
- no broad Display-class disable/remove,
- no physical display mutation,
- log exact package/device result locally,
- return structured error state to the normal app.

The public installer must not inherit the ge9 manual "Add Legacy Hardware" workflow.

## Upgrade

Upgrade must be transactional at the product level.

Required sequence:

1. Stop active VQM mirror sessions.
2. Record current VQM logical-monitor count and stable monitor identities.
3. Stage the new signed driver package.
4. Upgrade only the VQM driver/device.
5. Re-enumerate/wait for topology convergence.
6. Verify expected VQM monitors and QHD mode.
7. Restore previous logical monitor count/pairing.
8. Mark upgrade successful.

If verification fails, provide a repair/rollback route rather than leaving a half-configured display adapter.

## Repair

Repair is offered when:

- driver package exists but no VQM adapter is present,
- VQM adapter exists but no expected monitor arrives,
- package/device version mismatch occurs,
- requested monitor count cannot be reached,
- repeated restart/re-enumeration does not converge.

Repair may:

- re-stage the same approved package,
- recreate/restart the exact VQM device,
- reapply VQM-owned configuration,
- re-run topology verification.

Repair must not touch unrelated display adapters.

## Uninstall

Target uninstall sequence:

1. Stop active VQM mirror sessions.
2. Remove/depart VQM logical monitors.
3. Remove the VQM virtual display device.
4. Remove the VQM driver package when no longer referenced.
5. Remove VQM-specific driver configuration.
6. Preserve or remove user app settings according to uninstall choice.
7. Re-run topology verification.
8. Confirm no VQM virtual display remains.

Uninstall must not:

- reset physical monitor EDID,
- delete global GraphicsDrivers configuration,
- alter unrelated display modes,
- require Device Manager cleanup.

## Reboot policy

Preferred behavior: no reboot for ordinary install/configure/remove.

A reboot is acceptable only when Windows explicitly reports that one is required. The UI must surface that state rather than silently assuming success.

## Multi-monitor policy

The public driver target supports up to four logical VQM monitors.

The app asks the user how many eligible physical displays should receive independent QHD workspaces.

For `N` selected physical FHD targets:

- desired logical VQM monitor count = `N`,
- each virtual monitor has a stable independent identity,
- DisplayDiscovery forms explicit 1:1 pairs,
- duplicate-one-source-to-many-physical mode, if added later, is a separate feature.

The installer itself does not guess this preference; normal app configuration owns it.

## Privilege boundary

The ordinary app stays non-elevated.

Elevation is limited to:

- driver package install/upgrade/remove,
- device create/restart/remove,
- protected VQM driver configuration writes.

Rendering, input forwarding, pairing, diagnostics viewing and ordinary startup stay non-elevated.

## Failure states exposed to the user

The app/installer must distinguish at least:

- driver package missing,
- driver installed but device missing,
- device present but disabled,
- device present but no virtual monitor,
- virtual monitor active but QHD mode unavailable,
- insufficient virtual monitor count,
- topology did not converge after lifecycle operation,
- restart required,
- UAC cancelled,
- signing/package validation failure,
- repair required.

Do not collapse these into a generic "driver error."

## Fresh-PC release gate

Before public alpha/beta, run this exact lifecycle on a clean Windows 11 machine:

`install → first run → 1 monitor → 2 monitors → sleep/resume → reboot → upgrade → repair → uninstall → reinstall`

The machine must finish uninstall with no VQM display adapter/monitor residue and no requirement for manual Device Manager cleanup.
