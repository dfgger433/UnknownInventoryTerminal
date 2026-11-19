$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'TexturePreviewer.csproj'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$output = Join-Path $projectRoot 'dist\TexturePreviewer'
& dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $output --nologo
if ($LASTEXITCODE -ne 0) { throw 'Texture previewer publish failed.' }
Write-Host "Published: $(Join-Path $output 'TexturePreviewer.exe')"
