# Builds one mod in Release and produces an upload-ready zip in <Mod>\Publish\out.
#
#   <Mod>-<version>-Nexus.zip     Mods\<Mod>.dll, so it extracts straight into the game folder.
#
# The version comes from the built DLL, which gets it from ModInfo.Version through AssemblyInfo.
# That makes ModInfo.Version the only place a version number is written, so there is nothing for it
# to drift out of step with.
#
# Usage: powershell -ExecutionPolicy Bypass -File tools\pack.ps1 -Mod SmartRestockEmployees
param(
    [Parameter(Mandatory = $true)]
    [string]$Mod
)
$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $PSScriptRoot
$modDir  = Join-Path $root $Mod
$project = Join-Path $modDir "$Mod.csproj"
$dll     = Join-Path $modDir "bin\Release\$Mod.dll"
$outDir  = Join-Path $modDir 'Publish\out'

if (-not (Test-Path $project)) { throw "Project not found: $project" }

$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild not found.' }

& $msbuild $project /restore /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# FileVersion comes back as "1.4.0" here, but a four-part "1.4.0.0" is just as valid an answer from
# Windows, so trim a trailing zero revision only when there is a fourth component to trim.
$version = (Get-Item $dll).VersionInfo.FileVersion -replace '^(\d+\.\d+\.\d+)\.0$', '$1'
if (-not $version) { throw "Could not read a version from $dll." }

New-Item -ItemType Directory -Force $outDir | Out-Null
$staging = Join-Path $outDir 'staging'
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }

New-Item -ItemType Directory -Force (Join-Path $staging 'Mods') | Out-Null
Copy-Item $dll (Join-Path $staging 'Mods')

$zip = Join-Path $outDir "$Mod-$version-Nexus.zip"
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -Force
Write-Host "Created $zip"

Remove-Item -Recurse -Force $staging
