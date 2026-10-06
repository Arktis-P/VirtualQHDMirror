@echo off
cd /d "%~dp0"
title VirtualQHDMirror v0.13 - 30 FPS
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VirtualQHDMirror.ps1" -Mode 30
echo.
pause
