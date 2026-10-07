# Production Requirements

## Identity

- [ ] Unique VQM hardware ID
- [ ] Unique UMDF DeviceGroupId
- [ ] Production DriverVer
- [ ] Accurate endpoint telemetry strings
- [ ] Stable per-monitor container identity
- [ ] No Microsoft sample placeholder strings

## Monitor lifecycle

- [ ] One monitor
- [ ] Two independent monitors
- [ ] Three independent monitors
- [ ] Four independent monitors
- [ ] Runtime arrival/departure or a documented bounded restart fallback
- [ ] Stable identity across reboot
- [ ] Stable identity across driver update

## Modes

Initial mandatory profile:

- [ ] 2560×1440 @ 60 Hz

Additional modes are opt-in features and require separate scaling/input QA.

## Configuration

- [ ] No public dependency on `option.txt`
- [ ] VQM-specific configuration namespace/API
- [ ] Normal configuration does not require editing global display registry keys
- [ ] Exact-device targeting
- [ ] Rollback on failed privileged operation

## Reliability

- [ ] Device start/stop
- [ ] sleep/resume
- [ ] display topology change
- [ ] driver update
- [ ] repair
- [ ] uninstall/reinstall
- [ ] multi-GPU
- [ ] repeated logical monitor add/remove
- [ ] no stale virtual monitor after clean uninstall

## Security / privilege

- [ ] ordinary app remains non-elevated
- [ ] narrow privileged lifecycle helper/installer only
- [ ] no test mode in public package
- [ ] no Secure Boot disable requirement
- [ ] no arbitrary command execution interface
- [ ] no broad Display-class device operations

## Packaging / signing

- [ ] upstream MS-PL notices retained
- [ ] package catalog generated
- [ ] production publisher identity finalized
- [ ] EV/Hardware Dev Center prerequisites complete
- [ ] chosen Microsoft signing submission passes
- [ ] clean Windows 11 install accepted
- [ ] installer verifies the signed package before lifecycle actions

## Release

The driver does not become a bundled public dependency until every P0 item in `docs/DRIVER_RELEASE_STRATEGY.md` is closed.
