param([string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
& dotnet build (Join-Path $projectRoot 'src\UnknownInventoryTerminal\UnknownInventoryTerminal.csproj') -c Release "-p:GameRoot=$GameRoot" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
& dotnet test (Join-Path $projectRoot 'tests\UnknownInventoryTerminal.Tests\UnknownInventoryTerminal.Tests.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
