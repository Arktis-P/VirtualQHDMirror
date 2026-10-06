@echo off
cd /d "%~dp0"
start "" wscript.exe "%~dp0archive\v0.14.1\VirtualQHDMirror.vbs"
exit /b 0
