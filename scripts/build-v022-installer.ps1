[CmdletBinding()]
param([switch]$SkipAppBuild)
$ErrorActionPreference = 'Stop'
function Get-VqmSha256 {
 param([string]$Path)
 $stream = [System.IO.File]::OpenRead($Path)
 $sha = [System.Security.Cryptography.SHA256]::Create()
 try { return [System.BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
 finally { $sha.Dispose(); $stream.Dispose() }
}

$root = Split-Path -Parent $PSScriptRoot
if (-not $SkipAppBuild) {
  & (Join-Path $root 'build-v022.cmd')
  if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
}
$release = Join-Path $root 'artifacts\release\v0.22'
foreach ($name in @('VirtualQHDMirror.exe','VirtualQHDMirror.Session.exe','VirtualQHDMirror.ElevatedHelper.exe','README.txt','THIRD_PARTY_NOTICES.md')) {
  if (-not (Test-Path -LiteralPath (Join-Path (Join-Path $release 'win-x64') $name) -PathType Leaf)) { throw "Missing application file: $name" }
}
$found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
$compiler = if ($found) { $found.Source } else { '' }
if (-not $compiler) {
  foreach ($candidate in @('C:\Program Files (x86)\Inno Setup 6\ISCC.exe','C:\Program Files\Inno Setup 6\ISCC.exe')) {
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { $compiler = $candidate; break }
  }
}
if (-not $compiler) { throw 'Inno Setup 6 missing. Install from https://jrsoftware.org/isinfo.php' }
& $compiler (Join-Path $root 'installer\VirtualQHDMirror-v022.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed: $LASTEXITCODE" }
$setup = Join-Path $release 'VirtualQHDMirror-v0.22-win-x64-Setup.exe'
if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw 'Missing Setup EXE.' }
$hash = (Get-VqmSha256 -Path $setup)
($hash + '  ' + [IO.Path]::GetFileName($setup)) | Set-Content -LiteralPath ($setup + '.sha256') -Encoding ASCII
Write-Host "[READY] Setup EXE and SHA256 created: $setup"
Write-Host '[HOLD] Installer unsigned. Verify install/uninstall before publication.'
