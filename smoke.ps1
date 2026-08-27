$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.2.12f1\Editor\Unity.exe'
$logs = Join-Path $projectPath 'Logs'
$buildLog = Join-Path $logs 'release-build.log'
$smokeLog = Join-Path $logs 'release-player-smoke.log'
$gamePath = Join-Path $projectPath 'Build\Windows\HarpoonCaptainsEdition.exe'

New-Item -ItemType Directory -Path $logs -Force | Out-Null
if (-not (Test-Path -LiteralPath $unityPath)) { throw "Unity was not found at $unityPath" }

$buildArgs = "-batchmode -nographics -quit -projectPath `"$projectPath`" -executeMethod Harpoon.Editor.ProjectSetup.BuildWindowsPlayer -logFile `"$buildLog`""
$unity = Start-Process -FilePath $unityPath -ArgumentList $buildArgs -Wait -PassThru -WindowStyle Hidden
if ($unity.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $gamePath)) {
    if (Test-Path -LiteralPath $buildLog) { Get-Content -LiteralPath $buildLog -Tail 80 }
    throw 'Windows player build failed.'
}

$playerArgs = @('-batchmode', '-nographics', ('-logFile "' + $smokeLog + '"'))
$player = Start-Process -FilePath $gamePath -ArgumentList $playerArgs -WorkingDirectory (Split-Path $gamePath) -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 10
if (-not $player.HasExited) { Stop-Process -Id $player.Id }
Start-Sleep -Seconds 2
if (-not (Test-Path -LiteralPath $smokeLog)) { throw 'Player smoke log was not created.' }
$fatal = Select-String -LiteralPath $smokeLog -Pattern 'NullReferenceException|Unhandled Exception|Crash!!!|PlayerLoop.*Exception'
if ($fatal) { $fatal | ForEach-Object { Write-Error $_.Line }; throw 'Player smoke test found a runtime exception.' }
Write-Host "HARPOON WINDOWS BUILD + PLAYER SMOKE PASSED: $gamePath" -ForegroundColor Green
