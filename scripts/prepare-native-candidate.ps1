[CmdletBinding()]
param(
    [string]$Tag = '',
    [string]$Repository = 'trinhtanphat/QS3D-AutoCAD',
    [string]$SourceDirectory = '',
    [string]$ExpectedCommit = '',
    [string]$ArtifactsDirectory = ''
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$verifyScript = Join-Path $repo 'scripts\verify-artifacts.ps1'
$destination = if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    Join-Path $repo 'artifacts'
}
else {
    [IO.Path]::GetFullPath($ArtifactsDirectory)
}
$tempRoot = $null

function Assert-FullSha([string]$Value, [string]$Label) {
    if ($Value -notmatch '^[0-9a-fA-F]{40}$') {
        throw "$Label must resolve to a full 40-character commit SHA: $Value"
    }
    return $Value.ToLowerInvariant()
}

function Invoke-Gh([string[]]$Arguments) {
    $output = @(& gh @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($Arguments -join ' ') failed:`n$($output -join [Environment]::NewLine)"
    }
    return ($output -join [Environment]::NewLine)
}

function Read-Provenance([string]$Directory) {
    $path = Join-Path $Directory 'RELEASE-PROVENANCE.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Candidate provenance is missing: $path"
    }
    try {
        return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    }
    catch {
        throw "Candidate provenance is not valid JSON: $path. $($_.Exception.Message)"
    }
}

$usingFixture = -not [string]::IsNullOrWhiteSpace($SourceDirectory)
try {
    if ($usingFixture) {
        if ([string]::IsNullOrWhiteSpace($ExpectedCommit)) {
            throw '-ExpectedCommit is required with -SourceDirectory.'
        }
        $resolvedCommit = Assert-FullSha $ExpectedCommit 'Expected commit'
        $sourceRoot = [IO.Path]::GetFullPath($SourceDirectory)
        if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
            throw "Candidate source directory does not exist: $sourceRoot"
        }
        $sourceKind = 'predownloaded'
    }
    else {
        if ([string]::IsNullOrWhiteSpace($Tag)) {
            throw '-Tag is required when -SourceDirectory is not supplied.'
        }
        if ($Tag -notmatch '^test-v\d+\.\d+\.\d+-ci\.\d+$') {
            throw "Engineering candidate tag must use test-v<semver>-ci.<run>: $Tag"
        }
        if ($Repository -notmatch '^[^/]+/[^/]+$') {
            throw "Repository must be in owner/name form: $Repository"
        }
        if ($null -eq (Get-Command gh -ErrorAction SilentlyContinue)) {
            throw 'GitHub CLI (gh) is required to download an engineering prerelease.'
        }

        $release = (Invoke-Gh @('release', 'view', $Tag, '--repo', $Repository, '--json', 'tagName,isPrerelease,targetCommitish')) | ConvertFrom-Json
        if ([string]$release.tagName -ne $Tag) {
            throw "GitHub release tag mismatch: requested $Tag, got $($release.tagName)"
        }
        if ($release.isPrerelease -ne $true) {
            throw "Refusing non-prerelease candidate handoff: $Tag"
        }

        $tagObject = (Invoke-Gh @('api', "repos/$Repository/git/ref/tags/$Tag")) | ConvertFrom-Json
        $tagSha = [string]$tagObject.object.sha
        $tagType = [string]$tagObject.object.type
        if ($tagType -eq 'tag') {
            $annotated = (Invoke-Gh @('api', "repos/$Repository/git/tags/$tagSha")) | ConvertFrom-Json
            if ([string]$annotated.object.type -ne 'commit') {
                throw "Engineering tag $Tag does not ultimately reference a commit."
            }
            $tagSha = [string]$annotated.object.sha
        }
        elseif ($tagType -ne 'commit') {
            throw "Engineering tag $Tag references unsupported object type '$tagType'."
        }
        $resolvedCommit = Assert-FullSha $tagSha 'Engineering tag target'

        if ([string]$release.targetCommitish -match '^[0-9a-fA-F]{40}$') {
            $releaseTarget = Assert-FullSha ([string]$release.targetCommitish) 'Release targetCommitish'
            if ($releaseTarget -ne $resolvedCommit) {
                throw "Release/tag identity mismatch: release target is $releaseTarget but tag resolves to $resolvedCommit."
            }
        }
        if (-not [string]::IsNullOrWhiteSpace($ExpectedCommit)) {
            $requestedCommit = Assert-FullSha $ExpectedCommit 'Expected commit'
            if ($resolvedCommit -ne $requestedCommit) {
                throw "Release tag mismatch: expected $requestedCommit, tag resolves to $resolvedCommit"
            }
        }

        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("QS3D-NativeCandidate-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
        $sourceRoot = $tempRoot
        foreach ($asset in @('RELEASE-PROVENANCE.json', 'SHA256SUMS.txt')) {
            [void](Invoke-Gh @('release', 'download', $Tag, '--repo', $Repository, '--dir', $sourceRoot, '--pattern', $asset))
        }
        $sourceKind = 'github-prerelease'
    }

    $provenance = Read-Provenance $sourceRoot
    $version = [string]$provenance.version
    if ($version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
        throw "Candidate provenance contains an invalid version: $version"
    }
    if ((Assert-FullSha ([string]$provenance.sourceCommit) 'Provenance source commit') -ne $resolvedCommit) {
        throw "Candidate provenance source SHA $($provenance.sourceCommit) does not match expected release SHA $resolvedCommit."
    }
    if ($provenance.sourceDirty -ne $false) {
        throw 'Refusing a candidate whose provenance reports sourceDirty=true.'
    }

    $zipName = "QS3D-AutoCAD-$version.zip"
    $setupName = "QS3D-AutoCAD-$version-Setup.exe"
    $requiredFiles = @($zipName, $setupName, 'RELEASE-PROVENANCE.json', 'SHA256SUMS.txt')

    if (-not $usingFixture) {
        foreach ($asset in @($zipName, $setupName)) {
            [void](Invoke-Gh @('release', 'download', $Tag, '--repo', $Repository, '--dir', $sourceRoot, '--pattern', $asset))
        }
    }

    foreach ($name in $requiredFiles) {
        $path = Join-Path $sourceRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Engineering release is missing required candidate asset: $name"
        }
    }

    & $verifyScript -Version $version -ExpectedCommit $resolvedCommit -ArtifactsDirectory $sourceRoot

    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    $existingProvenancePath = Join-Path $destination 'RELEASE-PROVENANCE.json'
    $oldVersion = $null
    if (Test-Path -LiteralPath $existingProvenancePath -PathType Leaf) {
        try {
            $oldVersion = [string]((Get-Content -Raw -LiteralPath $existingProvenancePath | ConvertFrom-Json).version)
        }
        catch {
            throw "Existing artifacts provenance is invalid; refusing to mix candidates: $existingProvenancePath"
        }
        if ($oldVersion -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
            throw "Existing artifacts provenance has an invalid version; refusing to mix candidates: $oldVersion"
        }
    }

    $managedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @($requiredFiles + 'NATIVE-CANDIDATE.json')) { [void]$managedNames.Add($name) }
    if (-not [string]::IsNullOrWhiteSpace($oldVersion)) {
        [void]$managedNames.Add("QS3D-AutoCAD-$oldVersion.zip")
        [void]$managedNames.Add("QS3D-AutoCAD-$oldVersion-Setup.exe")
    }

    $unexpectedCandidateFiles = @(
        Get-ChildItem -LiteralPath $destination -File -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Name -match '^QS3D-AutoCAD-.+\.(zip|exe)$' -and -not $managedNames.Contains($_.Name)
            }
    )
    if ($unexpectedCandidateFiles.Count -gt 0) {
        throw "Artifacts directory already contains mixed candidate files. Remove/archive them before preparation: $($unexpectedCandidateFiles.Name -join ', ')"
    }

    $backup = Join-Path $destination ('.native-candidate-backup-' + [Guid]::NewGuid().ToString('N'))
    $moved = @()
    try {
        foreach ($name in $managedNames) {
            $path = Join-Path $destination $name
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                if (-not (Test-Path -LiteralPath $backup -PathType Container)) {
                    New-Item -ItemType Directory -Force -Path $backup | Out-Null
                }
                Move-Item -LiteralPath $path -Destination (Join-Path $backup $name)
                $moved += $name
            }
        }

        foreach ($name in $requiredFiles) {
            Copy-Item -LiteralPath (Join-Path $sourceRoot $name) -Destination (Join-Path $destination $name)
        }
        & $verifyScript -Version $version -ExpectedCommit $resolvedCommit -ArtifactsDirectory $destination

        $metadata = [ordered]@{
            schemaVersion = 1
            product = 'QS3D AutoCAD'
            sourceKind = $sourceKind
            repository = if ($usingFixture) { $null } else { $Repository }
            tag = if ($usingFixture) { $null } else { $Tag }
            version = $version
            sourceCommit = $resolvedCommit
            preparedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        }
        $metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'NATIVE-CANDIDATE.json') -Encoding utf8NoBOM
        Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue
    }
    catch {
        foreach ($name in @($requiredFiles + 'NATIVE-CANDIDATE.json')) {
            Remove-Item -LiteralPath (Join-Path $destination $name) -Force -ErrorAction SilentlyContinue
        }
        foreach ($name in $moved) {
            $backupPath = Join-Path $backup $name
            if (Test-Path -LiteralPath $backupPath -PathType Leaf) {
                Move-Item -LiteralPath $backupPath -Destination (Join-Path $destination $name) -Force
            }
        }
        Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue
        throw
    }

    Write-Host "Prepared exact native candidate $version at $resolvedCommit."
    if (-not $usingFixture) { Write-Host "Engineering prerelease: $Tag" }
    Write-Host "Artifacts directory: $destination"
    Write-Host 'Candidate verified before and after transactional placement; mixed/stale assets were not accepted.'
}
finally {
    if ($null -ne $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
