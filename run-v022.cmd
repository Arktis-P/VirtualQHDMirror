@echo off
setlocal
set "APP=%~dp0artifacts\release\v0.22\win-x64\VirtualQHDMirror.exe"
set "SESSION=%~dp0artifacts\release\v0.22\win-x64\VirtualQHDMirror.Session.exe"
set "HELPER=%~dp0artifacts\release\v0.22\win-x64\VirtualQHDMirror.ElevatedHelper.exe"

if not exist "%APP%" goto :build
if not exist "%SESSION%" goto :build
if not exist "%HELPER%" goto :build
goto :launch

:build
echo [INFO] v0.22 release package is missing or incomplete. Building...
call "%~dp0build-v022.cmd"
if errorlevel 1 exit /b 1

:launch
if not exist "%APP%" goto :missing
if not exist "%SESSION%" goto :missing
if not exist "%HELPER%" goto :missing
echo [INFO] Launching VirtualQHDMirror v0.22...
start "" "%APP%"
exit /b 0

:missing
echo [ERROR] v0.22 package is incomplete. Run build-v022.cmd.
pause
exit /b 1
