<#
.SYNOPSIS
  Publishes EF Migration Manager as a self-contained single-file Windows executable.

.DESCRIPTION
  Produces a single .exe with the .NET runtime embedded — the target machine does
  NOT need the .NET SDK or runtime installed. Output is written to ./publish.

.PARAMETER Configuration
  Build configuration. Default: Release.

.PARAMETER Runtime
  Target RID. Default: win-x64.

.EXAMPLE
  pwsh ./build/publish.ps1
#>
param(
    [string]$Configuration = 'Release',
    [string]$Runtime       = 'win-x64',
    [ValidateSet('none','patch','major')]
    [string]$VersionBump   = 'patch'
)

$ErrorActionPreference = 'Stop'

$root       = Resolve-Path (Join-Path $PSScriptRoot '..')
$project    = Join-Path $root 'src/EfMigrationManager.App/EfMigrationManager.App.csproj'
$outputDir  = Join-Path $root 'publish'
$issPath    = Join-Path $root 'build/installer.iss'

if (-not (Test-Path $issPath)) {
    throw "Version source not found: $issPath"
}

$issContent = Get-Content $issPath -Raw
$versionPattern = '(?m)^#define\s+MyAppVersion\s+"(?<v>\d+)\.(?<m>\d+)\.(?<p>\d+)"\s*$'
$match = [regex]::Match($issContent, $versionPattern)
if (-not $match.Success) {
    throw "Could not parse MyAppVersion from installer.iss"
}

$major = [int]$match.Groups['v'].Value
$minor = [int]$match.Groups['m'].Value
$patch = [int]$match.Groups['p'].Value

if ($VersionBump -eq 'major') {
    $major += 1
    $minor = 0
    $patch = 0
}
elseif ($VersionBump -eq 'none') {
    # keep version as-is from installer.iss
}
else {
    $patch += 1
}

$newVersion = "$major.$minor.$patch"
if ($VersionBump -eq 'none') {
    Write-Host "Version kept at $newVersion (no bump)" -ForegroundColor Cyan
}
else {
    $newIssContent = [regex]::Replace($issContent, $versionPattern, "#define MyAppVersion    `"$newVersion`"")
    Set-Content -Path $issPath -Value $newIssContent -NoNewline
    Write-Host "Version bumped to $newVersion ($VersionBump release)" -ForegroundColor Cyan
}

if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force }

Write-Host "Publishing $project -> $outputDir" -ForegroundColor Cyan

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=embedded `
    -p:Version=$newVersion `
    -p:AssemblyVersion=$newVersion `
    -p:FileVersion=$newVersion `
    -o $outputDir

if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }

$exe = Join-Path $outputDir 'EfMigrationManager.exe'
if (-not (Test-Path $exe)) { throw "Expected $exe not found." }

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ""
Write-Host "Published OK: $exe (${size} MB)" -ForegroundColor Green
