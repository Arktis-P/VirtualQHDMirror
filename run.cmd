@echo off
cd /d "%~dp0"
start "" wscript.exe "%~dp0VirtualQHDMirror.vbs"
exit /b 0
