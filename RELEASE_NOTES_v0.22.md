# VirtualQHDMirror v0.22

Windows 11 x64 release: guided external virtual display driver setup.

## Changes
- Added Virtual Display Setup with missing/ready/QHD-mode/restart status and Recheck.
- Added Get driver link to the official ge9/IddSampleDriver GitHub release page, with an explicit third-party warning.
- Preserved the v0.21.2 D3D11 + Portal mirror engine, mouse/drag bridging and Start with Windows option.

## Downloads
- VirtualQHDMirror-v0.22-win-x64-Setup.exe: per-user Windows APP installer.
- VirtualQHDMirror-v0.22-win-x64.zip: portable distribution.
- SHA-256 files for both.

The virtual display driver is NOT bundled, downloaded or installed. ge9 is third-party software. The APP installer is unsigned, so Windows may display an unknown publisher warning. No Secure Boot or certificate changes are performed.

## QA
Owner confirmed new Get driver link, Recheck/Ready, mirroring, v0.22 build and run all pass on existing ge9 system; build/run/Missing detection passed on a clean no-driver desktop. Newly added Setup.exe installer still needs its own build/install/uninstall smoke test before publication.
