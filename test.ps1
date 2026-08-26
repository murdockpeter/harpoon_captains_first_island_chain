$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$runner = Join-Path $projectPath 'Tools\Harpoon.Core.Validation\Harpoon.Core.Validation.csproj'

dotnet run --project $runner --configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
