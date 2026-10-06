@echo off
cd /d "%~dp0\.."
title VirtualQHDMirror v0.14 - Portal - 60 FPS
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%CD%\VirtualQHDMirror.ps1" -Mode 60 -InputMode portal
echo.
pause
