param([string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceDir = Join-Path $projectRoot 'dist'
$webSourceDir = Join-Path $projectRoot 'web\dist'
$destinationDir = Join-Path $GameRoot 'BepInEx\plugins\UnknownInventoryTerminal'
$webDestinationDir = Join-Path $destinationDir 'Web'
if (!(Test-Path (Join-Path $GameRoot 'CasualtiesUnknown_Data\Managed\Assembly-CSharp.dll'))) { throw 'Invalid game directory.' }
if (!(Test-Path (Join-Path $GameRoot 'BepInEx\plugins\CUCoreLib.dll'))) { throw 'CUCoreLib is required.' }
if (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue) { throw 'Close the game before installation.' }
$files = @('UnknownInventoryTerminal.dll', 'UnknownInventoryTerminal.Core.dll')
foreach ($file in $files) {
    if (!(Test-Path (Join-Path $sourceDir $file))) { throw "Missing build artifact: $file. Run Build.ps1 first." }
}
if (!(Test-Path (Join-Path $webSourceDir 'index.html'))) { throw 'Missing WebUI frontend artifact. Run Build.ps1 first.' }
# No deletions; only this plugin's dedicated subdirectory is written.
New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $sourceDir $file) -Destination (Join-Path $destinationDir $file) -Force }
New-Item -ItemType Directory -Path $webDestinationDir -Force | Out-Null
Copy-Item -Path (Join-Path $webSourceDir '*') -Destination $webDestinationDir -Recurse -Force
Write-Output "Installed in $destinationDir"
