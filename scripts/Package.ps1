# SPDX-License-Identifier: GPL-3.0-only

<#
.SYNOPSIS
Builds a local runtime ZIP without installing the mod or running the game.

.DESCRIPTION
Runs a Release build and writes dist/UnknownInventoryTerminal-<Version>.zip.
Use Build.ps1 separately to run the test suite. Before public distribution,
review docs/LICENSE-AUDIT.md and provide the corresponding source separately.

.PARAMETER GameRoot
The game installation containing the Managed, BepInEx, and CUCoreLib references.
Defaults to the game root when this repo is under its mods directory.

.EXAMPLE
.\scripts\Package.ps1 -GameRoot 'D:\Steam Library\steamapps\common\Casualties Unknown'
#>
[CmdletBinding()]
param([string]$GameRoot = (Join-Path $PSScriptRoot '..\..\..'))

$ErrorActionPreference = 'Stop'
$modName = 'UnknownInventoryTerminal'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$gameDir = Get-Item -LiteralPath $GameRoot -ErrorAction SilentlyContinue
if (!$gameDir -or !$gameDir.PSIsContainer -or !(Test-Path -LiteralPath (Join-Path $gameDir.FullName 'CasualtiesUnknown_Data\Managed\Assembly-CSharp.dll') -PathType Leaf)) {
    throw 'Invalid game directory. Pass -GameRoot pointing to the Casualties Unknown installation.'
}

$versionFile = Join-Path $projectRoot 'src\Directory.Build.props'
[xml]$buildProps = Get-Content -LiteralPath $versionFile -Raw
$version = ([string]$buildProps.SelectSingleNode('/Project/PropertyGroup/Version').InnerText).Trim()
if ($version -cnotmatch '\A[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z') {
    throw "Missing or invalid release version in $versionFile."
}

# Match the mandatory runtime files in Install.ps1. Do not include optional
# language packs, debug files, or other files that merely happen to be in dist.
$assets = 'assets/unknowninventoryterminal'
$files = @(
    "$modName.dll"
    "$modName.Core.dll"
    'LICENSE'
    'Lang/en_us.json'
    'Lang/zh_cn.json'
    "$assets/NOTICE.txt"
    "$assets/licenses/modernity/LICENSE.txt"
    "$assets/screens/terminals/terminal.json"
    "$assets/textures/gui/terminal/terminal_background.png"
    "$assets/textures/gui/terminal/terminal_controls.png"
    "$assets/textures/gui/widgets/text_field.png"
    "$assets/textures/gui/settings/settings_background.png"
    "$assets/textures/gui/settings/toggle.png"
    "$assets/textures/gui/settings/cpu_selector.png"
    "$assets/textures/gui/widgets/button.png"
    "$assets/textures/gui/widgets/button_highlighted.png"
    "$assets/textures/gui/widgets/drop_down_list.png"
    "$assets/textures/gui/storage/scroll_thumb.png"
    "$assets/textures/gui/storage/scroll_thumb_disabled.png"
    "$assets/textures/gui/widgets/transparent_button.png"
    "$assets/textures/gui/widgets/vertical_button_bar.png"
    "$assets/textures/gui/widgets/vertical_button_bar_variant.png"
    "$assets/textures/gui/widgets/vertical_button_bar_bottom.png"
    "$assets/textures/gui/fluid/fluid_slider_rail.png"
    "$assets/textures/gui/fluid/fluid_slider_thumb.png"
)
$dependencyLicense = Join-Path $PSScriptRoot 'licenses\LGPL-3.0.txt'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$dist = Join-Path $projectRoot 'dist'
$distDir = New-Item -ItemType Directory -Path $dist -Force
if ($distDir.Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw "Release output directory must not be a link: $dist"
}
$zipName = "$modName-$version.zip"
$zipPath = Join-Path $distDir.FullName $zipName
$stagingPath = Join-Path $distDir.FullName ('.package-' + [guid]::NewGuid().ToString('N'))
$stagingDir = New-Item -ItemType Directory -Path $stagingPath
try {
    $buildDir = Join-Path $stagingDir.FullName 'build'
    $project = Join-Path $projectRoot "src\$modName\$modName.csproj"
    Push-Location -LiteralPath $projectRoot
    try {
        & dotnet build $project -c Release --output $buildDir "-p:GameRoot=$($gameDir.FullName)" "-p:Version=$version" --nologo
        if ($LASTEXITCODE -ne 0) { throw "Release build failed (exit code $LASTEXITCODE)." }
    } finally {
        Pop-Location
    }

    $pendingZip = Join-Path $stagingDir.FullName $zipName
    $archive = [IO.Compression.ZipFile]::Open($pendingZip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) {
            $source = Join-Path $buildDir $file
            if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing release artifact: $file" }
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $source, "$modName/$file", 'Optimal') | Out-Null
        }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $dependencyLicense, "$modName/LGPL-3.0.txt", 'Optimal') | Out-Null
    } finally {
        $archive.Dispose()
    }

    # Publish only a completed archive. A build/ZIP error leaves any earlier ZIP intact.
    if ([IO.File]::Exists($zipPath)) {
        [IO.File]::Replace($pendingZip, $zipPath, [NullString]::Value)
    } else {
        [IO.File]::Move($pendingZip, $zipPath)
    }
} finally {
    $resolvedStaging = Get-Item -LiteralPath $stagingPath -Force
    if ($resolvedStaging.FullName -ne $stagingPath -or $resolvedStaging.Parent.FullName -ne $distDir.FullName -or
        ($resolvedStaging.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing to clean up unexpected staging directory: $($resolvedStaging.FullName)"
    }
    Remove-Item -LiteralPath $resolvedStaging.FullName -Recurse -Force
}

Write-Output "Created $zipPath"
Write-Warning 'Local runtime ZIP only. Review docs/LICENSE-AUDIT.md before public distribution.'
