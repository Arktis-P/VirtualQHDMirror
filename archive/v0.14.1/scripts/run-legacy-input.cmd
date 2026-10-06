@echo off
cd /d "%~dp0\.."
title VirtualQHDMirror v0.14 - Legacy Input
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%CD%\VirtualQHDMirror.ps1" -Mode adaptive -InputMode legacy
echo.
pause
