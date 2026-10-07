@echo off
setlocal
cd /d "%~dp0"
set "VQM_REPO_ROOT=%~dp0"
set "VQM_BUILD_CONFIGURATION=Debug"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [VirtualQHDMirror v0.20] .NET SDK was not found.
    echo Install the .NET 8 SDK, then run this launcher again.
    echo.
    pause
    exit /b 1
)

echo [VirtualQHDMirror v0.20] Building solution...
dotnet build "%~dp0VirtualQHDMirror.sln" --configuration Debug
if errorlevel 1 (
    echo.
    echo [VirtualQHDMirror v0.20] Build failed.
    pause
    exit /b 1
)

dotnet run --project "%~dp0src\VirtualQHDMirror.App\VirtualQHDMirror.App.csproj" --configuration Debug --no-build
set "VQM_EXIT=%ERRORLEVEL%"

if not "%VQM_EXIT%"=="0" (
    echo.
    echo VirtualQHDMirror v0.20 exited with code %VQM_EXIT%.
    pause
)

exit /b %VQM_EXIT%
