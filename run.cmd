@echo off
cd /d "%~dp0"
title VirtualQHDMirror v0.14.1 - Portal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0VirtualQHDMirror.ps1" -Mode adaptive -InputMode portal -CursorHz 120
echo.
echo Exit code: %ERRORLEVEL%
pause
