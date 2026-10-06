@echo off
cd /d "%~dp0\.."
title VirtualQHDMirror v0.14.1 - Console Diagnostics
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%CD%\VirtualQHDMirror.ps1" -Mode adaptive -InputMode portal -CursorHz 120
echo.
echo Exit code: %ERRORLEVEL%
pause
