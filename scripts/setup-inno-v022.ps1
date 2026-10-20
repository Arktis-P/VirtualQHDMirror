# One-time, explicit Inno Setup 6 installer via the official winget package.
# Does not modify project dependencies or bundle third-party executables.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$existing = @(Get-Command ISCC.exe -ErrorAction SilentlyContinue)
$locations = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Inno Setup 6\ISCC.exe')
)
foreach ($path in $locations) {
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Write-Host "[READY] Inno Setup 6 already installed: $path"
        exit 0
    }
}
if ($existing.Count -gt 0) {
    Write-Host "[READY] ISCC.exe found on PATH: $($existing[0].Source)"
    exit 0
}
$winget = Get-Command winget.exe -ErrorAction SilentlyContinue
if (-not $winget) {
    Write-Host '[NEEDS_USER_TOOLCHAIN] winget unavailable. Use the official Inno Setup download:'
    Write-Host 'https://jrsoftware.org/isdl.php'
    exit 2
}
Write-Host '[INFO] This installs third-party Inno Setup 6 on this Windows PC (NOT in the Git repository).'
Write-Host '[INFO] Official Windows Package Manager ID: JRSoftware.InnoSetup'
Write-Host '[INFO] Package publisher/terms are shown by winget; you will approve installation separately.'
$answer = Read-Host "Type INSTALL to continue, anything else to cancel"
if ($answer -cne 'INSTALL') {
    Write-Host '[CANCELLED] Nothing installed.'
    exit 0
}
& $winget.Source install --id JRSoftware.InnoSetup --exact --source winget --interactive
if ($LASTEXITCODE -ne 0) {
    Write-Host "[BLOCKED] winget returned $LASTEXITCODE. See https://jrsoftware.org/isdl.php"
    exit 1
}
Write-Host '[READY] Inno Setup install command completed. Next: build-v022-installer.cmd'
