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
    [string]$Runtime       = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$root       = Resolve-Path (Join-Path $PSScriptRoot '..')
$project    = Join-Path $root 'src/EfMigrationManager.App/EfMigrationManager.App.csproj'
$outputDir  = Join-Path $root 'publish'

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
    -o $outputDir

if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }

$exe = Join-Path $outputDir 'EfMigrationManager.exe'
if (-not (Test-Path $exe)) { throw "Expected $exe not found." }

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ""
Write-Host "Published OK: $exe (${size} MB)" -ForegroundColor Green
