$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$buildRoot = Join-Path $projectPath 'Build\Windows'
$updater = Join-Path $projectPath 'Assets\StreamingAssets\HarpoonUpdater.ps1'
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$testRoot = Join-Path $tempRoot ('HarpoonUpdaterIntegration-' + [Guid]::NewGuid().ToString('N'))
$installRoot = Join-Path $testRoot 'Install'
$backupRoot = Join-Path $testRoot 'Backup'
$packagePath = Join-Path $testRoot 'Harpoon-First-Island-Chain-Windows.zip'

try {
    if (-not (Test-Path -LiteralPath (Join-Path $buildRoot 'HarpoonFirstIslandChain.exe'))) {
        throw 'Build the Windows player before running updater-test.ps1.'
    }
    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $buildRoot 'HarpoonFirstIslandChain.exe') -Destination $installRoot
    Compress-Archive -Path (Join-Path $buildRoot '*') -DestinationPath $packagePath -CompressionLevel Fastest

    $buildVersionFile = Join-Path $buildRoot 'harpoon-version.txt'
    if (-not (Test-Path -LiteralPath $buildVersionFile)) { throw 'Built player version marker is missing.' }
    $targetVersion = (Get-Content -LiteralPath $buildVersionFile -Raw).Trim()

    $arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$updater`" " +
        "-GamePid 999999 -PackagePath `"$packagePath`" -InstallDirectory `"$installRoot`" " +
        "-ExecutableName `"HarpoonFirstIslandChain.exe`" -BackupDirectory `"$backupRoot`" " +
        "-TargetVersion `"$targetVersion`" -SkipRelaunch"
    $process = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    $versionFile = Join-Path $installRoot 'harpoon-version.txt'
    $installedVersion = if (Test-Path -LiteralPath $versionFile) {
        (Get-Content -LiteralPath $versionFile -Raw).Trim()
    } else { 'MISSING' }
    $backupExists = Test-Path -LiteralPath (Join-Path $backupRoot 'HarpoonFirstIslandChain.exe')
    if ($process.ExitCode -ne 0 -or $installedVersion -ne $targetVersion -or -not $backupExists) {
        throw "Updater integration failed: exit=$($process.ExitCode), version=$installedVersion, backup=$backupExists"
    }
    Write-Host 'HARPOON UPDATER INTEGRATION PASSED: version contract, replacement, backup, and no-relaunch test.' -ForegroundColor Green
}
finally {
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTestRoot).StartsWith('HarpoonUpdaterIntegration-')) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
