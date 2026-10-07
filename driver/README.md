# VirtualQHDMirror Driver

This directory is reserved for the **VQM-owned production Indirect Display Driver**.

The current development machine uses an external IddSampleDriver/ge9-compatible driver. That compatibility binary is intentionally **not copied here** and is not the public release driver.

## Source foundation

Production source will be derived directly from Microsoft's official Windows Driver Samples IddSample under the Microsoft Public License (MS-PL).

Before importing source:

1. record the exact upstream commit,
2. preserve upstream copyright/license notices,
3. include the applicable MS-PL license text/notices,
4. import only the required IDD sample pieces,
5. immediately replace sample identity/placeholders with VQM-specific production identity,
6. track deviations in this directory.

## Layout

```text
driver/
├─ README.md
├─ THIRD_PARTY_NOTICES.md
└─ VirtualDisplay/
   ├─ README.md
   └─ PRODUCTION_REQUIREMENTS.md
```

Actual WDK source/projects will be added only when the import checkpoint begins.

See:

- `docs/DRIVER_RELEASE_STRATEGY.md`
- `docs/PUBLIC_DRIVER_INSTALL_CONTRACT.md`
