@echo off
cd /d "%~dp0"
title VirtualQHDMirror v0.12
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VirtualQHDMirror.ps1" -Mode adaptive
echo.
echo Exit code: %ERRORLEVEL%
pause
