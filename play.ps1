$ErrorActionPreference = 'Stop'

$projectPath = $PSScriptRoot
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.2.12f1\Editor\Unity.exe'
$logPath = Join-Path $projectPath 'Logs\play-build.log'
$gamePath = Join-Path $projectPath 'Build\Windows\HarpoonFirstIslandChain.exe'

if (-not (Test-Path -LiteralPath $unityPath)) {
    throw "Unity 6000.2.12f1 was not found at: $unityPath"
}

New-Item -ItemType Directory -Path (Join-Path $projectPath 'Logs') -Force | Out-Null
$arguments = "-batchmode -quit -projectPath `"$projectPath`" -executeMethod Harpoon.Editor.ProjectSetup.BuildWindowsPlayer -logFile `"$logPath`""
$unity = Start-Process -FilePath $unityPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden

if ($unity.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $gamePath)) {
    Write-Host "Build failed. See $logPath" -ForegroundColor Red
    if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Tail 40 }
    exit 1
}

Write-Host 'Build passed. Launching Harpoon: First Island Chain...' -ForegroundColor Green
Start-Process -FilePath $gamePath -WorkingDirectory (Split-Path -Parent $gamePath)
