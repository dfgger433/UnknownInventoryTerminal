param([string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceDir = Join-Path $projectRoot 'dist'
$destinationDir = Join-Path $GameRoot 'BepInEx\plugins\UnknownInventoryTerminal'
if (!(Test-Path (Join-Path $GameRoot 'CasualtiesUnknown_Data\Managed\Assembly-CSharp.dll'))) { throw 'Invalid game directory.' }
if (!(Test-Path (Join-Path $GameRoot 'BepInEx\plugins\CUCoreLib.dll'))) { throw 'CUCoreLib is required.' }
if (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue) { throw 'Close the game before installation.' }
$assetSubdir = 'Assets\AE2'
$langSubdir = 'Lang'
$files = @(
    'UnknownInventoryTerminal.dll',
    'UnknownInventoryTerminal.Core.dll',
    (Join-Path $langSubdir 'en.json'),
    (Join-Path $langSubdir 'zh-CN.json'),
    (Join-Path $assetSubdir 'NOTICE.txt'),
    (Join-Path $assetSubdir 'screens\terminals\terminal.json'),
    (Join-Path $assetSubdir 'textures\guis\terminal.png'),
    (Join-Path $assetSubdir 'textures\guis\states.png'),
    (Join-Path $assetSubdir 'textures\guis\text_field.png'),
    (Join-Path $assetSubdir 'textures\gui\sprites\big_scroller.png'),
    (Join-Path $assetSubdir 'textures\gui\sprites\big_scroller_disabled.png'),
    (Join-Path $assetSubdir 'textures\gui\sprites\transparent.png')
)
foreach ($file in $files) {
    if (!(Test-Path (Join-Path $sourceDir $file) -PathType Leaf)) { throw "Missing build artifact: $file. Run Build.ps1 first." }
}
# No deletions; only this plugin's dedicated subdirectory is written.
$directories = @(
    $destinationDir,
    (Join-Path $destinationDir $langSubdir),
    (Join-Path $destinationDir $assetSubdir),
    (Join-Path $destinationDir "$assetSubdir\screens"),
    (Join-Path $destinationDir "$assetSubdir\screens\terminals"),
    (Join-Path $destinationDir "$assetSubdir\textures"),
    (Join-Path $destinationDir "$assetSubdir\textures\guis"),
    (Join-Path $destinationDir "$assetSubdir\textures\gui"),
    (Join-Path $destinationDir "$assetSubdir\textures\gui\sprites")
)
foreach ($directory in $directories) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $sourceDir $file) -Destination (Join-Path $destinationDir $file) -Force }
Write-Output "Installed in $destinationDir"
