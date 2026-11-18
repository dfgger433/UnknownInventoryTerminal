param([string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$webRoot = Join-Path $projectRoot 'web'
if (!(Test-Path (Join-Path $webRoot 'package.json'))) { throw 'WebUI frontend package is missing.' }
if (!(Test-Path (Join-Path $webRoot 'node_modules'))) {
    & npm ci --prefix $webRoot
    if ($LASTEXITCODE -ne 0) { throw 'WebUI frontend dependency install failed.' }
}
Push-Location $webRoot
try {
    & npm run build
    if ($LASTEXITCODE -ne 0) { throw 'WebUI frontend build failed.' }
} finally { Pop-Location }
$webDist = Join-Path $webRoot 'dist'
$oreUiNotice = Join-Path $webDist 'THIRD-PARTY-OreUI'
New-Item -ItemType Directory -Path $oreUiNotice -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $webRoot 'vendor\OreUI\LICENSE') -Destination (Join-Path $oreUiNotice 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $webRoot 'vendor\OreUI\README.md') -Destination (Join-Path $oreUiNotice 'README.md') -Force
& dotnet build (Join-Path $projectRoot 'src\UnknownInventoryTerminal\UnknownInventoryTerminal.csproj') -c Release "-p:GameRoot=$GameRoot" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
& dotnet test (Join-Path $projectRoot 'tests\UnknownInventoryTerminal.Tests\UnknownInventoryTerminal.Tests.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
