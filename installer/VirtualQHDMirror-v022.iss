; App-only per-user installer for VirtualQHDMirror v0.22
; IMPORTANT: AppId is stable across EVERY version. Do not include a version in AppId,
; AppName, install directory, main app EXE, or shortcut target.
; Only AppVersion, installer distribution filename and package paths are versioned.
; Keep PrivilegesRequired=lowest and architecture/install mode consistent for upgrades.
#define SourceDir "..\artifacts\release\v0.22\win-x64"
[Setup]
AppId={{C3E58A21-1EC0-4B96-A841-17CC6679F664}
AppName=VirtualQHDMirror
AppVersion=0.22
AppPublisher=Team Burning Arktis
AppPublisherURL=https://github.com/Arktis-P/VirtualQHDMirror
DefaultDirName={localappdata}\Programs\VirtualQHDMirror
DefaultGroupName=VirtualQHDMirror
UninstallDisplayIcon={app}\VirtualQHDMirror.exe
OutputDir=..\artifacts\release\v0.22
OutputBaseFilename=VirtualQHDMirror-v0.22-win-x64-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
MinVersion=10.0.22000
WizardStyle=modern
CloseApplications=yes
[Files]
Source: "{#SourceDir}\VirtualQHDMirror.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\VirtualQHDMirror.Session.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\VirtualQHDMirror.ElevatedHelper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\README.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{autoprograms}\VirtualQHDMirror"; Filename: "{app}\VirtualQHDMirror.exe"
[Run]
Filename: "{app}\VirtualQHDMirror.exe"; Description: "Launch VirtualQHDMirror"; Flags: nowait postinstall skipifsilent
