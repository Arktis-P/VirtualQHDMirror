@echo off
setlocal
set "APP=%~dp0artifacts\release\v0.21.2\win-x64\VirtualQHDMirror.exe"

if exist "%APP%" (
    start "" "%APP%"
    exit /b 0
)

echo VirtualQHDMirror v0.21.2 release executable was not found.
echo.
echo Build it first by running:
echo   build-release.cmd
echo.
echo Expected path:
echo   %APP%
pause
exit /b 1
