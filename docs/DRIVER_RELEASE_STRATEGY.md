# Public Virtual Display Driver Release Strategy

## Decision

The public VirtualQHDMirror release will **not bundle the current ge9/IddSampleDriver binary**.

The production driver will be a **VirtualQHDMirror-owned Indirect Display Driver derived directly from Microsoft's official IddSample / Windows Driver Samples codebase**.

The currently installed IddSampleDriver/ge9 family remains supported as a **development compatibility driver** until the VQM-owned driver reaches feature parity and passes installation/recovery QA.

## Why this strategy

### Official upstream and license

Microsoft's Windows Driver Samples repository is distributed under the **Microsoft Public License (MS-PL)**.

The license permits commercial use, modification and distribution, subject to its notice and source-distribution conditions. The VQM repository must preserve applicable upstream notices and the MS-PL text for source derived from Microsoft's sample.

Microsoft also explicitly describes IddSample as sample code and documents production changes that must be made before release. The sample is therefore a suitable upstream foundation, not a shippable production binary.

Primary references:

- Microsoft Windows Driver Samples: https://github.com/microsoft/Windows-driver-samples
- MS-PL license: https://github.com/microsoft/Windows-driver-samples/blob/main/LICENSE
- IddSample documentation: https://learn.microsoft.com/windows-hardware/drivers/samples/indirect-display-driver-sample/

### Why not ship ge9 directly

ge9 is useful for current development because it already provides configurable monitor count and QHD modes.

However, its repository explicitly mixes its own permissive changes with inherited Microsoft code and instructs users to check Microsoft's license for inherited portions. Its historical installation path also depends on certificate trust/manual legacy-hardware setup patterns that are not appropriate as the permanent VQM public install UX.

Therefore:

- ge9 compatibility support stays in v0.20 development,
- ge9 binaries/certificates are not copied into the public package,
- VQM does not take ownership of ge9's signing/update lifecycle,
- implementation ideas may be studied, but production source lineage must remain explicit.

## Stable technical identity

The VQM-owned driver reserves:

| Field | Value |
| --- | --- |
| Hardware ID | `ROOT\VirtualQHDMirrorDisplay` |
| Device name | `VirtualQHDMirror Virtual Display Adapter` |
| Endpoint friendly name | `VirtualQHDMirror Virtual Display` |
| UMDF DeviceGroupId | `VirtualQHDMirrorIddGroup` |
| Initial maximum monitor count | 4 |
| Initial required mode | 2560×1440 @ 60 Hz |

The legal Publisher / Manufacturer strings are intentionally **not** frozen yet. They must match the organization/certificate identity used when the public driver is signed.

These technical constants also live in:

`src/VirtualQHDMirror.DriverManager/PublicVirtualDisplayIdentity.cs`

## Production changes required from IddSample

The production driver must not retain sample placeholders.

Required work includes:

1. Unique INF identity
   - VQM hardware ID
   - unique DeviceGroupId
   - production DriverVer
   - real catalog generation
   - real provider/manufacturer strings at signing time

2. Stable monitor identity
   - do not generate a random container ID on every initialization
   - each logical VQM monitor gets a deterministic/stable identity
   - Windows must be able to distinguish monitor A/B/C/D across restart/update

3. Multiple-monitor abstraction
   - support up to four independent VQM logical monitors
   - monitor objects must be tracked independently
   - arrival/departure must be explicit rather than one shared monitor handle

4. Runtime configuration
   - do not use `C:\IddSampleDriver\option.txt` in the public driver
   - do not require driver restart merely to change normal monitor count if avoidable
   - preferred design: a narrow authenticated device-control contract that lets DriverManager request logical monitor arrival/departure
   - if v0.1 of the production IDD still requires restart, use driver-owned registry/config state under a VQM-specific key and a bounded/recoverable restart path

5. Mode policy
   - first public profile only requires 2560×1440 @ 60 Hz
   - additional modes must be added intentionally and tested
   - do not expose a large arbitrary mode list merely because the compatibility driver does

6. Frame processing
   - retain correct IddCx/DXGI processing behavior
   - resolve all production-relevant TODOs from Microsoft's sample
   - report/handle swap-chain loss correctly
   - no unnecessary CPU copy path inside the driver

7. Diagnostics
   - accurate endpoint/manufacturer/model metadata
   - local event/error diagnostics sufficient for VQM support
   - no user-content/frame logging

## Signing and distribution gate

### Development

Local development may use the existing compatibility driver.

The VQM production driver may use development/test signing only on dedicated development machines.

### Alpha

An attestation-signed package may be considered for controlled testing if the project has the required Hardware Dev Center account and EV certificate.

This is **not** the target state for a general public release.

### General public release

Target:

- Hardware Dev Center account
- EV certificate associated with the account
- current WDK/HLK test matrix
- Windows Hardware Compatibility Program submission
- Microsoft-signed production package
- no user-installed root certificate
- no test-signing mode
- no Secure Boot disable instructions

The app installer should consume only a release-approved signed driver package.

## Windows Update

Windows Update publication is not required for the first public release.

The initial supported channel can be a signed installer/release package, provided install/update/uninstall are reliable.

Windows Update distribution can be evaluated later after the driver passes the same production quality gates.

## Release blockers

The production driver cannot be bundled until all are true:

- [ ] VQM-owned source tree exists under `driver/VirtualDisplay/`
- [ ] upstream MS-PL notices preserved
- [ ] unique INF/device/group identity complete
- [ ] stable per-monitor identity implemented
- [ ] 1–4 monitor lifecycle implemented
- [ ] 2560×1440 profile verified
- [ ] install/upgrade/repair/uninstall scripted without Device Manager
- [ ] sleep/resume and topology recovery tested
- [ ] development certificate/trust hacks removed
- [ ] EV/Hardware Dev Center enrollment complete
- [ ] chosen signing submission path completed
- [ ] clean Windows 11 install QA passed
- [ ] rollback/uninstall restores a clean display topology

## Compatibility driver retirement

The ge9 compatibility path can be removed only after the VQM-owned driver passes:

- single physical FHD ↔ single virtual QHD,
- two physical FHD ↔ two independent virtual QHD,
- reboot,
- sleep/resume,
- driver update,
- driver repair,
- uninstall/reinstall,
- v0.20 DisplayDiscovery stable-ID regression.

Until then, the compatibility path remains useful for mirror-engine development but is explicitly not the public distribution contract.
