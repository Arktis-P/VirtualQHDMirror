@echo off
cd /d "%~dp0\.."
title VirtualQHDMirror v0.14.1 - Portal - 120 FPS Experimental
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%CD%\VirtualQHDMirror.ps1" -Mode 120 -InputMode portal -CursorHz 120
echo.
pause
