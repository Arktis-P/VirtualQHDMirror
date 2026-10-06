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
