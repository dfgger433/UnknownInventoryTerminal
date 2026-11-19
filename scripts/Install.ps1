param([string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceDir = Join-Path $projectRoot 'dist'
$destinationDir = Join-Path $GameRoot 'BepInEx\plugins\UnknownInventoryTerminal'
if (!(Test-Path (Join-Path $GameRoot 'CasualtiesUnknown_Data\Managed\Assembly-CSharp.dll'))) { throw 'Invalid game directory.' }
if (!(Test-Path (Join-Path $GameRoot 'BepInEx\plugins\CUCoreLib.dll'))) { throw 'CUCoreLib is required.' }
if (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue) { throw 'Close the game before installation.' }
$files = @('UnknownInventoryTerminal.dll', 'UnknownInventoryTerminal.Core.dll')
$assetSubdir = 'Assets\AE2'
$assetFiles = @('terminal.png', 'states.png', 'text_field.png', 'big_scroller.png', 'big_scroller_disabled.png', 'NOTICE.txt')
foreach ($asset in $assetFiles) { $files += Join-Path $assetSubdir $asset }
$langSubdir = 'Lang'
foreach ($language in @('en.json', 'zh-CN.json')) { $files += Join-Path $langSubdir $language }
foreach ($file in $files) {
    if (!(Test-Path (Join-Path $sourceDir $file))) { throw "Missing build artifact: $file. Run Build.ps1 first." }
}
# No deletions; only this plugin's dedicated subdirectory is written.
New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $destinationDir $assetSubdir) -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $destinationDir $langSubdir) -Force | Out-Null
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $sourceDir $file) -Destination (Join-Path $destinationDir $file) -Force }
Write-Output "Installed in $destinationDir"
