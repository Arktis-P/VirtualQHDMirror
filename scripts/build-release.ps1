param(
    [string]$Version = "0.21.2",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue

if (-not $dotnet) {
    throw ".NET 8 SDK was not found. Install the .NET 8 SDK before building the release package."
}

$releaseRoot = Join-Path $root ("artifacts\release\v" + $Version)
$outputDirectory = Join-Path $releaseRoot $Runtime
$stagingRoot = Join-Path $releaseRoot ".publish"
$zipPath = Join-Path $releaseRoot ("VirtualQHDMirror-v" + $Version + "-" + $Runtime + ".zip")
$checksumPath = $zipPath + ".sha256"

if (Test-Path -LiteralPath $outputDirectory) {
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

function Publish-VqmExecutable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$ExecutableName,

        [Parameter(Mandatory = $true)]
        [string]$StageName
    )

    $stage = Join-Path $stagingRoot $StageName
    New-Item -ItemType Directory -Path $stage -Force | Out-Null

    $arguments = @(
        "publish",
        $ProjectPath,
        "--configuration", "Release",
        "--runtime", $Runtime,
        "--self-contained", "true",
        "--output", $stage,
        "--nologo",
        "-p:Version=$Version",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true",
        "-p:PublishReadyToRun=false",
        "-p:DebugType=None",
        "-p:DebugSymbols=false"
    )

    Write-Host ""
    Write-Host ("Publishing " + $ExecutableName + "...")

    & $dotnet.Source @arguments

    if ($LASTEXITCODE -ne 0) {
        throw ("dotnet publish failed for " + $ProjectPath + " with exit code " + $LASTEXITCODE + ".")
    }

    $publishedExecutable = Join-Path $stage $ExecutableName

    if (-not (Test-Path -LiteralPath $publishedExecutable)) {
        throw ("Expected published executable was not produced: " + $publishedExecutable)
    }

    Copy-Item -LiteralPath $publishedExecutable -Destination (Join-Path $outputDirectory $ExecutableName) -Force
}

Publish-VqmExecutable -ProjectPath (Join-Path $root "src\VirtualQHDMirror.App\VirtualQHDMirror.App.csproj") -ExecutableName "VirtualQHDMirror.exe" -StageName "app"
Publish-VqmExecutable -ProjectPath (Join-Path $root "src\VirtualQHDMirror.Session\VirtualQHDMirror.Session.csproj") -ExecutableName "VirtualQHDMirror.Session.exe" -StageName "session"
Publish-VqmExecutable -ProjectPath (Join-Path $root "src\VirtualQHDMirror.ElevatedHelper\VirtualQHDMirror.ElevatedHelper.csproj") -ExecutableName "VirtualQHDMirror.ElevatedHelper.exe" -StageName "helper"

$notices = Join-Path $root "THIRD_PARTY_NOTICES.md"
if (Test-Path -LiteralPath $notices) {
    Copy-Item -LiteralPath $notices -Destination (Join-Path $outputDirectory "THIRD_PARTY_NOTICES.md") -Force
}

$packageReadme = @"
VirtualQHDMirror v$Version ($Runtime)

Run:
  VirtualQHDMirror.exe

Included sidecar executables:
  VirtualQHDMirror.Session.exe
  VirtualQHDMirror.ElevatedHelper.exe

All three executables must remain in the same folder.

The build is self-contained for .NET and does not require the .NET Desktop Runtime to be installed separately.

The virtual display driver is not bundled by this package yet.
"@

[System.IO.File]::WriteAllText(
    (Join-Path $outputDirectory "README.txt"),
    $packageReadme,
    [System.Text.Encoding]::UTF8)

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $outputDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
($hash.Hash.ToLowerInvariant() + "  " + [System.IO.Path]::GetFileName($zipPath)) | Set-Content -LiteralPath $checksumPath -Encoding ASCII

Remove-Item -LiteralPath $stagingRoot -Recurse -Force

Write-Host ""
Write-Host "Release package created:"
Write-Host ("  " + $outputDirectory)
Write-Host ("  " + $zipPath)
Write-Host ("  " + $checksumPath)
