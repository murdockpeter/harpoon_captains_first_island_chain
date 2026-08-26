$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'test.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.2.12f1\Editor\Unity.exe'
$validationLog = Join-Path $PSScriptRoot 'Logs\release-unity-validation.log'
$validationArgs = "-batchmode -nographics -quit -projectPath `"$PSScriptRoot`" -executeMethod Harpoon.Editor.ProjectSetup.ValidateRules -logFile `"$validationLog`""
$unity = Start-Process -FilePath $unityPath -ArgumentList $validationArgs -Wait -PassThru -WindowStyle Hidden
if ($unity.ExitCode -ne 0 -or -not (Select-String -LiteralPath $validationLog -Quiet -Pattern 'HARPOON RULE VALIDATION PASSED')) {
    if (Test-Path -LiteralPath $validationLog) { Get-Content -LiteralPath $validationLog -Tail 80 }
    throw 'Extended Unity rules validation failed.'
}
& (Join-Path $PSScriptRoot 'smoke.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& (Join-Path $PSScriptRoot 'updater-test.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host 'MVP 0.1 RELEASE CHECK PASSED' -ForegroundColor Green
