[CmdletBinding()]
param([switch]$Publish)
$ErrorActionPreference = 'Stop'
function Get-VqmSha256 {
 param([string]$Path)
 $stream = [System.IO.File]::OpenRead($Path)
 $sha = [System.Security.Cryptography.SHA256]::Create()
 try { return [System.BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
 finally { $sha.Dispose(); $stream.Dispose() }
}

$root = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $root 'artifacts\release\v0.22'
$assets = @()
foreach ($name in @('VirtualQHDMirror-v0.22-win-x64-Setup.exe','VirtualQHDMirror-v0.22-win-x64.zip')) {
  $f = Join-Path $dir $name
  if (-not (Test-Path -LiteralPath $f) -or -not (Test-Path -LiteralPath ($f + '.sha256'))) { throw "Release asset missing: $name" }
  $expected = ((Get-Content -LiteralPath ($f + '.sha256') -Raw).Trim() -split '\s+')[0]
  if ($expected -ine (Get-VqmSha256 -Path $f)) { throw "SHA mismatch: $name" }
  Write-Host "[PASS] $name"; $assets += $f; $assets += ($f + '.sha256')
}
if (-not $Publish) { Write-Host '[READY] Release assets verified. Smoke-test installer, then use -Publish on synced main.'; exit 0 }
if (-not (Get-Command gh.exe -ErrorAction SilentlyContinue)) { throw 'GitHub CLI gh.exe not available.' }
Push-Location $root
try {
  if ((& git branch --show-current).Trim() -ne 'main') { throw 'Must publish from main.' }
  & git fetch origin main
  if ($LASTEXITCODE -ne 0) { throw 'Failed to fetch main.' }
  $head = (& git rev-parse HEAD).Trim()
  if ($head -ne (& git rev-parse origin/main).Trim()) { throw 'main is not synced with origin.' }
  if (& git status --porcelain) { throw 'Working tree not clean.' }
  & gh auth status
  if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI authentication required.' }
  & gh release view v0.22.0 --repo Arktis-P/VirtualQHDMirror 2>$null
  if ($LASTEXITCODE -eq 0) { throw 'v0.22.0 already exists; refusing overwrite.' }
  & gh release create v0.22.0 @assets --repo Arktis-P/VirtualQHDMirror --target $head --title 'VirtualQHDMirror v0.22' --notes-file (Join-Path $root 'RELEASE_NOTES_v0.22.md') --latest
  if ($LASTEXITCODE -ne 0) { throw 'GitHub Release creation failed.' }
  Write-Host '[READY] GitHub v0.22.0 release published.'
} finally { Pop-Location }
