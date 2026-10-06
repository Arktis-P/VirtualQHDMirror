@echo off
cd /d "%~dp0\.."
title VirtualQHDMirror v0.13.1 - 60 FPS
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%CD%\VirtualQHDMirror.ps1" -Mode 60
echo.
pause
