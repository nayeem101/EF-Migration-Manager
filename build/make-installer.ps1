<#
.SYNOPSIS
  End-to-end: publishes the app then compiles a Windows installer using Inno Setup.

.DESCRIPTION
  Requires Inno Setup 6 (ISCC.exe) on PATH or in the default install location.
  Install via: winget install -e --id JRSoftware.InnoSetup

  Produces installer-output/EfMigrationManager-Setup-<version>.exe
#>
param(
    [string]$Configuration = 'Release',
    [string]$Runtime       = 'win-x64',
    [ValidateSet('none','patch','major')]
    [string]$VersionBump   = 'none',
    [switch]$Republish
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = Resolve-Path (Join-Path $here '..')
$publishedExe = Join-Path $root 'publish\EfMigrationManager.exe'

if ($Republish -or -not (Test-Path $publishedExe)) {
    & (Join-Path $here 'publish.ps1') -Configuration $Configuration -Runtime $Runtime -VersionBump $VersionBump
}
else {
    Write-Host "Reusing existing publish output at: $publishedExe" -ForegroundColor Cyan
    Write-Host "No extra version bump applied." -ForegroundColor Cyan
}

$iscc = $null
$candidates = @(
    'ISCC.exe',
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
foreach ($c in $candidates) {
    $cmd = Get-Command $c -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source; break }
    if (Test-Path $c) { $iscc = $c; break }
}

if (-not $iscc) {
    Write-Host ""
    Write-Host "Inno Setup not found. Install it with:" -ForegroundColor Yellow
    Write-Host "    winget install -e --id JRSoftware.InnoSetup" -ForegroundColor Yellow
    Write-Host "Then re-run this script." -ForegroundColor Yellow
    exit 1
}

$iss = Join-Path $here 'installer.iss'
Write-Host ""
Write-Host "Compiling installer with $iscc ..." -ForegroundColor Cyan

& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }

$out = Resolve-Path (Join-Path $here '..\installer-output')
Write-Host ""
Write-Host "Installer(s) in: $out" -ForegroundColor Green
Get-ChildItem $out -Filter *.exe | Select-Object Name, Length | Format-Table
