$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'TexturePreviewer.csproj'
& dotnet run --project $project -c Release
if ($LASTEXITCODE -ne 0) { throw 'Texture previewer exited with an error.' }
