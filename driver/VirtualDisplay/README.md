# VQM Virtual Display Driver

## Reserved identity

- Hardware ID: `ROOT\VirtualQHDMirrorDisplay`
- Device name: `VirtualQHDMirror Virtual Display Adapter`
- Endpoint friendly name: `VirtualQHDMirror Virtual Display`
- DeviceGroupId: `VirtualQHDMirrorIddGroup`
- Initial monitor limit: 4
- Initial required mode: 2560×1440 @ 60 Hz

Legal Provider/Manufacturer strings are deliberately deferred until the production signing publisher is known.

## Design target

The VQM IDD is not a renamed ge9 binary.

It will be a productized IddCx driver with:

- stable monitor identities,
- independent 1–4 monitor lifecycle,
- VQM-owned runtime configuration/control,
- no `C:\IddSampleDriver\option.txt`,
- no manual root-certificate installation,
- no test-signing requirement for release,
- no broad display-class restart,
- clean upgrade/repair/uninstall.

## Development rule

Do not copy a third-party prebuilt IDD into this directory.

When the Microsoft sample source is imported, record its exact upstream commit and preserve licensing notices before making product changes.
