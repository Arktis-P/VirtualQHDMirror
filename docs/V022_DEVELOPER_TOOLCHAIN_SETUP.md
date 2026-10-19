# v0.22 — developer environment bootstrap

This is for **driver developers only**, never for ordinary VirtualQHDMirror users.

Run in a new PowerShell session from the repository root:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup-v022-driver-toolchain.ps1
~~~

Default behavior: **read-only** detection of Visual Studio 2026+, VC++ MSBuild targets, Windows SDK 28000, WDK 28000; missing components return [NEEDS_USER_TOOLCHAIN]. Detection is heuristic; successful **real WDK compilation** is the ultimate gate.

If anything is missing, start the approved auto installer:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup-v022-driver-toolchain.ps1 -Install
~~~

- Requires WinGet and online package retrieval.
- **Explicitly asks for INSTALL** before making any machine changes.
- Applies the official Microsoft Windows-driver-samples WinGet configuration at pinned commit 2dc3fd3a0cc84a2933f2194e7ec0871584979071; this may install/modify VS Community, C++ components, Windows SDK 28000, WDK 28000.
- No attempt to uninstall/replace existing IDEs or disable driver signing. An existing VS 2026 Community installation should be modified by Microsoft's configuration provider when supported.
- Re-detects the machine after installation and starts the existing driver-only WDK compile gate. A reboot, shell restart or user approval might still be required.
- To check installation readiness but defer the compiler: append -SkipBuild.

The official WinGet instructions and package identities are documented at:
https://learn.microsoft.com/en-us/windows-hardware/drivers/install-the-wdk-using-winget

**Important:** A driver successful build is not a signed or installable public driver. The compatibility driver remains untouched. The application release build never includes or invokes the developer toolchain bootstrap, WDK, or test-signed IDD.

### User gate

1. Pull branch gpt/v0.22.
2. Run the read-only command.
3. If toolchain missing, run the -Install command and type INSTALL when explicitly prompted.
4. Send final [READY] or first [BLOCKED] line with artifacts/driver/v022-b2-wdk-build.log if compile fails.

No hosted CI or unattended installations.

### WinGet Configure initially disabled

If output says `Extended features are not enabled. Run winget configure --enable`, the prior script could not reach installation. The updated installer now automatically runs `winget configure --enable` **after explicit INSTALL consent**, then downloads the pinned Microsoft DSC file locally before applying it. Users do not need a separate manual enable command. This changes only the developer PC WinGet Configuration feature setting when the user elects `-Install`.

If enabling fails, check App Installer version, Store access and WinGet policy; the script stops instead of claiming VS/SDK/WDK installed.
