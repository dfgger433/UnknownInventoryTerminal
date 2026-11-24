param([string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceDir = Join-Path $projectRoot 'dist'
$destinationDir = Join-Path $GameRoot 'BepInEx\plugins\UnknownInventoryTerminal'
if (!(Test-Path (Join-Path $GameRoot 'CasualtiesUnknown_Data\Managed\Assembly-CSharp.dll'))) { throw 'Invalid game directory.' }
if (!(Test-Path (Join-Path $GameRoot 'BepInEx\plugins\CUCoreLib.dll'))) { throw 'CUCoreLib is required.' }
if (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue) { throw 'Close the game before installation.' }
$assetSubdir = 'assets\unknowninventoryterminal'
$langSubdir = 'Lang'
$files = @(
    'UnknownInventoryTerminal.dll',
    'UnknownInventoryTerminal.Core.dll',
    (Join-Path $langSubdir 'en.json'),
    (Join-Path $langSubdir 'zh-CN.json'),
    (Join-Path $assetSubdir 'NOTICE.txt'),
    (Join-Path $assetSubdir 'licenses\modernity\LICENSE.txt'),
    (Join-Path $assetSubdir 'screens\terminals\terminal.json'),
    (Join-Path $assetSubdir 'textures\gui\terminal\terminal_background.png'),
    (Join-Path $assetSubdir 'textures\gui\terminal\terminal_controls.png'),
    (Join-Path $assetSubdir 'textures\gui\widgets\text_field.png'),
    (Join-Path $assetSubdir 'textures\gui\settings\settings_background.png'),
    (Join-Path $assetSubdir 'textures\gui\settings\toggle.png'),
    (Join-Path $assetSubdir 'textures\gui\settings\cpu_selector.png'),
    (Join-Path $assetSubdir 'textures\gui\widgets\button.png'),
    (Join-Path $assetSubdir 'textures\gui\widgets\button_highlighted.png'),
    (Join-Path $assetSubdir 'textures\gui\widgets\drop_down_list.png'),
    (Join-Path $assetSubdir 'textures\gui\storage\scroll_thumb.png'),
    (Join-Path $assetSubdir 'textures\gui\storage\scroll_thumb_disabled.png'),
    (Join-Path $assetSubdir 'textures\gui\widgets\transparent_button.png'),
    (Join-Path $assetSubdir 'textures\gui\widgets\vertical_button_bar.png'),
    (Join-Path $assetSubdir 'textures\gui\widgets\vertical_button_bar_variant.png'),
    (Join-Path $assetSubdir 'textures\gui\widgets\vertical_button_bar_bottom.png'),
    (Join-Path $assetSubdir 'textures\gui\fluid\fluid_slider_rail.png'),
    (Join-Path $assetSubdir 'textures\gui\fluid\fluid_slider_thumb.png')
)
foreach ($file in $files) {
    if (!(Test-Path (Join-Path $sourceDir $file) -PathType Leaf)) { throw "Missing build artifact: $file. Run Build.ps1 first." }
}
# No deletions; only this plugin's dedicated subdirectory is written.
foreach ($file in $files) {
    $destination = Join-Path $destinationDir $file
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceDir $file) -Destination $destination -Force
}
# Install optional language packs from the build output only when the user has not already supplied one.
$sourceLanguages = Join-Path $sourceDir $langSubdir
$destinationLanguages = Join-Path $destinationDir $langSubdir
foreach ($language in Get-ChildItem -LiteralPath $sourceLanguages -Filter '*.json' -File) {
    if ($language.Name -in @('en.json', 'zh-CN.json')) { continue }
    $destination = Join-Path $destinationLanguages $language.Name
    if (Test-Path -LiteralPath $destination) { continue }
    New-Item -ItemType Directory -Path $destinationLanguages -Force | Out-Null
    Copy-Item -LiteralPath $language.FullName -Destination $destination
}
Write-Output "Installed in $destinationDir"
