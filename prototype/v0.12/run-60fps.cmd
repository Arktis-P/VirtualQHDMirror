@echo off
cd /d "%~dp0"
title VirtualQHDMirror v0.12 - 60 FPS
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VirtualQHDMirror.ps1" -Mode 60
echo.
pause
