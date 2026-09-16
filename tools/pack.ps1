# Builds one mod in Release and produces upload-ready zips in <Mod>\Publish\out.
#   Nexus Mods:   <Mod>-<version>-Nexus.zip        (Mods\<Mod>.dll)
#   Thunderstore: <Mod>-<version>-Thunderstore.zip (only when <Mod>\Publish\icon.png exists)
#
# Usage: powershell -ExecutionPolicy Bypass -File tools\pack.ps1 -Mod SmartRestockEmployees
param(
    [Parameter(Mandatory = $true)]
    [string]$Mod
)
$ErrorActionPreference = 'Stop'

$root     = Split-Path -Parent $PSScriptRoot
$modDir   = Join-Path $root $Mod
$project  = Join-Path $modDir "$Mod.csproj"
$publish  = Join-Path $modDir 'Publish'
$dll      = Join-Path $modDir "bin\Release\$Mod.dll"
$outDir   = Join-Path $publish 'out'

if (-not (Test-Path $project)) { throw "Project not found: $project" }
$manifest = Get-Content (Join-Path $publish 'manifest.json') -Raw | ConvertFrom-Json
$version  = $manifest.version_number

$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild not found.' }

& $msbuild $project /restore /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$built = (Get-Item $dll).VersionInfo.FileVersion
if (-not $built.StartsWith($version)) {
    throw "Version mismatch: manifest.json is $version but the DLL is $built. Update ModInfo.Version or manifest.json."
}

New-Item -ItemType Directory -Force $outDir | Out-Null
$staging = Join-Path $outDir 'staging'
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }

# Nexus Mods: extract-into-game-folder layout.
$nexus = Join-Path $staging 'nexus'
New-Item -ItemType Directory -Force (Join-Path $nexus 'Mods') | Out-Null
Copy-Item $dll (Join-Path $nexus 'Mods')
$nexusZip = Join-Path $outDir "$Mod-$version-Nexus.zip"
Compress-Archive -Path (Join-Path $nexus '*') -DestinationPath $nexusZip -Force
Write-Host "Created $nexusZip"

# Thunderstore: manifest.json, README.md, icon.png (256x256) at the zip root.
$icon = Join-Path $publish 'icon.png'
if (Test-Path $icon) {
    $ts = Join-Path $staging 'thunderstore'
    New-Item -ItemType Directory -Force (Join-Path $ts 'Mods') | Out-Null
    Copy-Item $dll (Join-Path $ts 'Mods')
    Copy-Item (Join-Path $publish 'manifest.json'), (Join-Path $publish 'README.md'), $icon $ts
    $tsZip = Join-Path $outDir "$Mod-$version-Thunderstore.zip"
    Compress-Archive -Path (Join-Path $ts '*') -DestinationPath $tsZip -Force
    Write-Host "Created $tsZip"
} else {
    Write-Host "Skipped Thunderstore zip: $Mod\Publish\icon.png (256x256) not found."
}

Remove-Item -Recurse -Force $staging
