$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.2.12f1\Editor\Unity.exe'
$logs = Join-Path $projectPath 'Logs'
$buildLog = Join-Path $logs 'release-build.log'
$smokeLog = Join-Path $logs 'release-player-smoke.log'
$gamePath = Join-Path $projectPath 'Build\Windows\HarpoonCaptainsEdition.exe'
$speechHelperPath = Join-Path $projectPath 'Build\Windows\HarpoonAccessibilitySpeech.exe'

New-Item -ItemType Directory -Path $logs -Force | Out-Null
if (-not (Test-Path -LiteralPath $unityPath)) { throw "Unity was not found at $unityPath" }

$buildArgs = "-batchmode -nographics -quit -projectPath `"$projectPath`" -executeMethod Harpoon.Editor.ProjectSetup.BuildWindowsPlayer -logFile `"$buildLog`""
$unity = Start-Process -FilePath $unityPath -ArgumentList $buildArgs -Wait -PassThru -WindowStyle Hidden
if ($unity.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $gamePath)) {
    if (Test-Path -LiteralPath $buildLog) { Get-Content -LiteralPath $buildLog -Tail 80 }
    throw 'Windows player build failed.'
}
if (-not (Test-Path -LiteralPath $speechHelperPath)) {
    throw 'Accessibility speech helper was not produced by the Windows build.'
}
$speechStart = [Diagnostics.ProcessStartInfo]::new($speechHelperPath)
$speechStart.UseShellExecute = $false
$speechStart.RedirectStandardInput = $true
$speechStart.RedirectStandardOutput = $true
$speechStart.CreateNoWindow = $true
$speech = [Diagnostics.Process]::Start($speechStart)
$speechReady = $speech.StandardOutput.ReadLine()
$speech.StandardInput.WriteLine('QUIT')
$speech.StandardInput.Flush()
if (-not $speech.WaitForExit(5000)) { $speech.Kill(); throw 'Accessibility speech helper did not exit.' }
if ($speechReady -ne "READY`t1" -or $speech.ExitCode -ne 0) {
    throw "Accessibility speech helper contract failed: $speechReady"
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
