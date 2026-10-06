@echo off
cd /d "%~dp0\.."
title VirtualQHDMirror v0.13.1 - 30 FPS
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%CD%\VirtualQHDMirror.ps1" -Mode 30
echo.
pause
