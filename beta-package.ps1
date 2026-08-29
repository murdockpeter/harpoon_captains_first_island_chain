$ErrorActionPreference = 'Stop'

$projectPath = $PSScriptRoot
$windowsBuild = Join-Path $projectPath 'Build\Windows'
$artifactFolder = Join-Path $projectPath 'Artifacts'
$archiveName = 'Harpoon-Captains-Edition-Windows.zip'
$archivePath = Join-Path $artifactFolder $archiveName
$checksumPath = "$archivePath.sha256"
$expectedVersion = '1.0.2'

& (Join-Path $projectPath 'release-check.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$requiredFiles = @(
    (Join-Path $windowsBuild 'HarpoonCaptainsEdition.exe'),
    (Join-Path $windowsBuild 'HarpoonAccessibilitySpeech.exe'),
    (Join-Path $windowsBuild 'HarpoonCaptainsEdition_Data'),
    (Join-Path $windowsBuild 'UnityPlayer.dll'),
    (Join-Path $windowsBuild 'harpoon-version.txt')
)
foreach ($required in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required player content is missing: $required" }
}

$actualVersion = (Get-Content -LiteralPath (Join-Path $windowsBuild 'harpoon-version.txt') -Raw).Trim()
if ($actualVersion -ne $expectedVersion) {
    throw "Player version $actualVersion does not match MVP beta version $expectedVersion."
}

$commit = (git -C $projectPath rev-parse HEAD).Trim()
$buildInfo = @(
    "Harpoon Captain's Edition: First Island Chain",
    "Version: $actualVersion",
    "Commit: $commit",
    "Certified UTC: $([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))",
    'Release gate: core + Unity rules + Windows player smoke + updater integration passed'
)
$buildInfo | Set-Content -LiteralPath (Join-Path $windowsBuild 'harpoon-build-info.txt') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $projectPath 'docs\BETA_PLAYTEST.md') `
    -Destination (Join-Path $windowsBuild 'BETA_PLAYTEST.md') -Force

New-Item -ItemType Directory -Path $artifactFolder -Force | Out-Null
$obsoleteArtifacts = @(
    (Join-Path $artifactFolder 'Harpoon-First-Island-Chain-Windows.zip'),
    (Join-Path $artifactFolder 'Harpoon-First-Island-Chain-Windows.zip.sha256')
)
$resolvedArtifactFolder = [IO.Path]::GetFullPath($artifactFolder).TrimEnd('\')
foreach ($obsoleteArtifact in $obsoleteArtifacts) {
    $resolvedObsoleteArtifact = [IO.Path]::GetFullPath($obsoleteArtifact)
    if (-not $resolvedObsoleteArtifact.StartsWith($resolvedArtifactFolder + '\',
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Obsolete artifact path escaped the artifact folder: $resolvedObsoleteArtifact"
    }
    if (Test-Path -LiteralPath $resolvedObsoleteArtifact) {
        Remove-Item -LiteralPath $resolvedObsoleteArtifact -Force
    }
}
Compress-Archive -Path (Join-Path $windowsBuild '*') -DestinationPath $archivePath `
    -CompressionLevel Optimal -Force
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
"$hash  $archiveName" | Set-Content -LiteralPath $checksumPath -Encoding ascii

Write-Host "MVP 1.0 BETA PACKAGE PASSED: $archivePath" -ForegroundColor Green
Write-Host "SHA-256: $hash" -ForegroundColor Green
