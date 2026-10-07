# Virtual Display Driver Strategy Assessment

## Decision at this checkpoint

Use the currently installed **IddSampleDriver family as the development compatibility baseline**, but do not bundle its existing third-party binary as the public VQM driver yet.

Preferred public-release direction:

1. keep compatibility with existing IddSampleDriver-style installations during development,
2. implement read-only detection and configuration inspection first,
3. decide signing/distribution before any automatic installation,
4. strongly prefer a VQM-owned IDD identity for the eventual bundled driver so monitor count, device identity, upgrade and uninstall behavior are under project control.

## Why

### Microsoft Indirect Display sample

Microsoft provides the official IddCx sample and licenses the Windows driver samples repository under MS-PL.

However, Microsoft explicitly describes the IDD implementation as a sample and calls out production work that must be completed before shipping a real driver.

Therefore it is a suitable technical foundation, not a drop-in public release binary.

### ge9/IddSampleDriver

Useful development properties:

- simple IddSampleDriver identity,
- configurable virtual-monitor count,
- `2560,1440,60` is part of its sample option list,
- recent releases allow `IDD_SAMPLE_DRIVER_CONFIG` to override the hard-coded config path,
- configuration recommends a small monitor count.

This matches VQM's future multi-monitor model well: one virtual QHD source can be created per physical FHD target when independent workspaces are desired.

Open questions before redistribution:

- exact inherited-license obligations across Microsoft + fork changes,
- whether the binary/certificate distributed by the fork is appropriate for a general public VQM installer,
- production signing ownership,
- clean upgrade/uninstall guarantees,
- support responsibility if the bundled driver changes Windows display topology.

### Installer-oriented forks

Installer-focused Virtual Display Driver forks are useful references for user experience and unattended setup, but some use stronger copyleft licenses and/or still surface SmartScreen caveats. They are not automatically a better bundling choice.

## Current policy

Development:

- support the installed IddSampleDriver-compatible environment,
- detect it without requiring Device Manager,
- read its configuration without modifying it,
- allow one or more active virtual displays to be paired by the VQM UI.

Public release:

- no test-signing requirement,
- no Secure Boot disabling,
- no automatic certificate trust workaround,
- no third-party driver binary bundled until license + signing + update/uninstall gates are explicitly passed.

## Multi-monitor implication

If the machine has `P` eligible physical FHD displays and the user chooses independent virtual workspaces, VQM should eventually request `P` virtual QHD monitors from the approved driver.

If the driver currently exposes fewer than `P`, DriverManager should report the deficit rather than silently duplicating one virtual source.

The present checkpoint only reads this state. It does not rewrite `option.txt` or restart/reinstall the driver.


## Configuration reload finding

Inspection of the current ge9 compatibility source shows that `loadOptions(...option.txt)` is called from the driver's device-add initialization path. The driver then uses that loaded monitor count as `MaxMonitorsSupported` and creates that number of virtual monitors during adapter initialization.

Implication:

1. write a validated new monitor count,
2. preserve a rollback copy,
3. restart only the recognized IDD device,
4. wait for display topology to settle,
5. re-run VQM display discovery,
6. verify the expected number of QHD virtual sources,
7. restore the previous config/restart again if verification fails.

For the Windows-side restart primitive, prefer Microsoft's built-in PnPUtil `/restart-device` command with the exact detected device instance ID. Do not disable/enable arbitrary Display-class devices and do not restart by broad class match.


## Final public-release direction

The P0 strategy decision is now fixed:

- **Development compatibility:** existing ge9/IddSampleDriver family.
- **Public production driver:** VQM-owned IDD derived directly from Microsoft's official IddSample source.
- **Source license foundation:** Microsoft Public License for imported Microsoft sample portions.
- **Public signing target:** Microsoft production signing through the Hardware Dev Center / Windows Hardware Compatibility Program path.
- **Attestation:** controlled testing option only, not the general-public target.
- **Public configuration:** VQM-owned configuration/control; do not carry forward `C:\IddSampleDriver\option.txt`.
- **Public device identity:** `ROOT\VirtualQHDMirrorDisplay`.
- **Public logical monitor target:** 1–4 independent monitors, initially 2560×1440 @ 60 Hz.

Detailed requirements:

- `docs/DRIVER_RELEASE_STRATEGY.md`
- `docs/PUBLIC_DRIVER_INSTALL_CONTRACT.md`
- `driver/VirtualDisplay/PRODUCTION_REQUIREMENTS.md`
