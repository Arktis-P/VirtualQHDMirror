@echo off
cd /d "%~dp0\.."
title VirtualQHDMirror v0.14 - Captured Input Experiment
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%CD%\VirtualQHDMirror.ps1" -Mode adaptive -InputMode captured
echo.
pause
