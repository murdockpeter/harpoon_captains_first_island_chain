$ErrorActionPreference = 'Stop'

$projectPath = $PSScriptRoot
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.2.12f1\Editor\Unity.exe'
$logPath = Join-Path $projectPath 'Logs\multiplayer-local-build.log'
$gamePath = Join-Path $projectPath 'Build\Windows\HarpoonCaptainsEdition.exe'

New-Item -ItemType Directory -Path (Join-Path $projectPath 'Logs') -Force | Out-Null
$arguments = "-batchmode -quit -projectPath `"$projectPath`" -executeMethod Harpoon.Editor.ProjectSetup.BuildWindowsPlayer -logFile `"$logPath`""
$unity = Start-Process -FilePath $unityPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden

if ($unity.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $gamePath)) {
    Write-Host "Build failed. See $logPath" -ForegroundColor Red
    if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Tail 40 }
    exit 1
}

Write-Host 'Launching two local multiplayer instances...' -ForegroundColor Green
$gameDirectory = Split-Path -Parent $gamePath
Start-Process -FilePath $gamePath -WorkingDirectory $gameDirectory -ArgumentList '-screen-width 1280 -screen-height 720 -windowed'
Start-Process -FilePath $gamePath -WorkingDirectory $gameDirectory -ArgumentList '-screen-width 1280 -screen-height 720 -windowed'
