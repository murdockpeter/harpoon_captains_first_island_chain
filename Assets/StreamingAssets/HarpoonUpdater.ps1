param(
    [Parameter(Mandatory = $true)][int]$GamePid,
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$InstallDirectory,
    [Parameter(Mandatory = $true)][string]$ExecutableName,
    [Parameter(Mandatory = $true)][string]$BackupDirectory,
    [Parameter(Mandatory = $true)][string]$TargetVersion,
    [switch]$SkipRelaunch
)

$ErrorActionPreference = 'Stop'
$logPath = Join-Path ([IO.Path]::GetTempPath()) 'HarpoonUpdater.log'

try {
    $installRoot = [IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')
    $package = [IO.Path]::GetFullPath($PackagePath)
    $backupRoot = [IO.Path]::GetFullPath($BackupDirectory).TrimEnd('\')
    $driveRoot = [IO.Path]::GetPathRoot($installRoot).TrimEnd('\')
    if ($installRoot -eq $driveRoot -or -not (Test-Path -LiteralPath (Join-Path $installRoot $ExecutableName))) {
        throw "Refusing to update an invalid installation directory: $installRoot"
    }
    if (-not (Test-Path -LiteralPath $package)) { throw "Verified package is missing: $package" }

    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    $stillRunning = Get-Process -Id $GamePid -ErrorAction SilentlyContinue
    while ($stillRunning -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $stillRunning = Get-Process -Id $GamePid -ErrorAction SilentlyContinue
    }
    if ($stillRunning) { throw 'The game did not close within two minutes; update cancelled.' }

    $stageRoot = Join-Path ([IO.Path]::GetTempPath()) ("HarpoonUpdateStage-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
    Expand-Archive -LiteralPath $package -DestinationPath $stageRoot -Force
    if (-not (Test-Path -LiteralPath (Join-Path $stageRoot $ExecutableName))) {
        throw "The release archive does not contain $ExecutableName at its root."
    }
    $versionFile = Join-Path $stageRoot 'harpoon-version.txt'
    if (-not (Test-Path -LiteralPath $versionFile) -or
        (Get-Content -LiteralPath $versionFile -Raw).Trim() -ne $TargetVersion.TrimStart('v', 'V')) {
        throw "The release archive version does not match requested version $TargetVersion."
    }

    if (Test-Path -LiteralPath $backupRoot) {
        $backupParent = Split-Path -Parent $backupRoot
        $resolvedBackupParent = [IO.Path]::GetFullPath($backupParent)
        if (-not $backupRoot.StartsWith($resolvedBackupParent, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Invalid backup path.'
        }
        Remove-Item -LiteralPath $backupRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null

    $releaseItems = Get-ChildItem -LiteralPath $stageRoot -Force
    foreach ($source in $releaseItems) {
        $destination = Join-Path $installRoot $source.Name
        $resolvedDestination = [IO.Path]::GetFullPath($destination)
        if (-not $resolvedDestination.StartsWith($installRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Archive item escaped the installation directory: $($source.Name)"
        }
        if (Test-Path -LiteralPath $destination) {
            Copy-Item -LiteralPath $destination -Destination (Join-Path $backupRoot $source.Name) -Recurse -Force
            Remove-Item -LiteralPath $destination -Recurse -Force
        }
        Copy-Item -LiteralPath $source.FullName -Destination $destination -Recurse -Force
    }

    "[$(Get-Date -Format o)] Installed Harpoon $TargetVersion to $installRoot; backup: $backupRoot" |
        Set-Content -LiteralPath $logPath
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
    if (-not $SkipRelaunch) {
        Start-Process -FilePath (Join-Path $installRoot $ExecutableName) -WorkingDirectory $installRoot
    }
    exit 0
}
catch {
    "[$(Get-Date -Format o)] UPDATE FAILED: $($_.Exception.Message)`n$($_.ScriptStackTrace)" |
        Set-Content -LiteralPath $logPath
    Add-Type -AssemblyName PresentationFramework -ErrorAction SilentlyContinue
    [System.Windows.MessageBox]::Show("Harpoon update failed.`n`n$($_.Exception.Message)`n`nLog: $logPath",
        'Harpoon Updater', 'OK', 'Error') | Out-Null
    exit 1
}
