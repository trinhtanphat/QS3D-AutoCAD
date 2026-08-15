$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$verifyScript = Join-Path $repo 'scripts\verify-artifacts.ps1'
$prepareScript = Join-Path $repo 'scripts\prepare-native-candidate.ps1'
$statusScript = Join-Path $repo 'scripts\show-native-acceptance-status.ps1'
$protectionScript = Join-Path $repo 'scripts\configure-main-protection.ps1'
$readinessScript = Join-Path $repo 'scripts\test-commercial-release-readiness.ps1'

foreach ($path in @($verifyScript, $prepareScript, $statusScript, $protectionScript, $readinessScript)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Operational tooling file is missing: $path"
    }
    [void][scriptblock]::Create((Get-Content -Raw -LiteralPath $path))
}

$protectionSource = Get-Content -Raw -LiteralPath $protectionScript
foreach ($required in @(
    "ValidateSet('Verify', 'Apply')",
    'required_status_checks',
    'required_pull_request_reviews',
    'enforce_admins',
    'allow_force_pushes',
    'allow_deletions',
    'required_conversation_resolution',
    '-ConfirmApply',
    'gh'
)) {
    if (-not $protectionSource.Contains($required, [StringComparison]::Ordinal)) {
        throw "Branch-protection helper regression: missing '$required'."
    }
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ("QS3D-Ops-Smoke-" + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $temp 'source'
$destination = Join-Path $temp 'destination'
$evidence = Join-Path $temp 'evidence'
$version = '0.0.0-ci'
$commit = '0123456789abcdef0123456789abcdef01234567'
$zipName = "QS3D-AutoCAD-$version.zip"
$setupName = "QS3D-AutoCAD-$version-Setup.exe"

function Get-Record([string]$Path) {
    $item = Get-Item -LiteralPath $Path
    return [ordered]@{
        file = $item.Name
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
        bytes = $item.Length
    }
}

try {
    New-Item -ItemType Directory -Force -Path $source, $destination, $evidence | Out-Null
    [IO.File]::WriteAllText((Join-Path $source $zipName), 'fixture-zip-payload')
    [IO.File]::WriteAllText((Join-Path $source $setupName), 'fixture-setup-payload')

    $provenance = [ordered]@{
        schemaVersion = 1
        product = 'QS3D AutoCAD'
        version = $version
        sourceCommit = $commit
        sourceDirty = $false
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        signed = $false
        runtimeMatrix = @(
            [ordered]@{ autoCAD = '2025-2026'; targetFramework = 'net8.0-windows'; managedRuntime = '.NET 8'; apiPackage = 'AutoCAD.NET 25.0.1' },
            [ordered]@{ autoCAD = '2027'; targetFramework = 'net10.0-windows'; managedRuntime = '.NET 10'; apiPackage = 'AutoCAD.NET 26.0.0' }
        )
        artifacts = @(
            (Get-Record (Join-Path $source $zipName)),
            (Get-Record (Join-Path $source $setupName))
        )
    }
    $provenancePath = Join-Path $source 'RELEASE-PROVENANCE.json'
    $provenance | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $provenancePath -Encoding utf8NoBOM
    $checksumLines = foreach ($name in @($zipName, $setupName, 'RELEASE-PROVENANCE.json')) {
        $path = Join-Path $source $name
        "{0}  {1}" -f (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant(), $name
    }
    Set-Content -LiteralPath (Join-Path $source 'SHA256SUMS.txt') -Value $checksumLines -Encoding ascii

    & $verifyScript -Version $version -ExpectedCommit $commit -ArtifactsDirectory $source
    & $prepareScript -SourceDirectory $source -ExpectedCommit $commit -ArtifactsDirectory $destination
    & $verifyScript -Version $version -ExpectedCommit $commit -ArtifactsDirectory $destination

    $metadataPath = Join-Path $destination 'NATIVE-CANDIDATE.json'
    if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
        throw 'Native candidate preparation did not create NATIVE-CANDIDATE.json.'
    }
    $metadata = Get-Content -Raw -LiteralPath $metadataPath | ConvertFrom-Json
    if ([string]$metadata.sourceCommit -ne $commit -or [string]$metadata.version -ne $version) {
        throw 'Native candidate preparation metadata does not bind the expected version/SHA.'
    }

    $stale = Join-Path $destination 'QS3D-AutoCAD-stale.zip'
    Set-Content -LiteralPath $stale -Value 'stale' -Encoding ascii
    $mixedRejected = $false
    try {
        & $prepareScript -SourceDirectory $source -ExpectedCommit $commit -ArtifactsDirectory $destination
    }
    catch {
        if ($_.Exception.Message -match 'mixed candidate files') {
            $mixedRejected = $true
        }
        else {
            throw
        }
    }
    if (-not $mixedRejected) {
        throw 'Native candidate preparation accepted a mixed/stale artifacts directory.'
    }
    Remove-Item -LiteralPath $stale -Force

    $statusJson = & $statusScript -ArtifactsDirectory $destination -EvidenceDirectory $evidence -NoHostDiscovery -Json
    $status = ($statusJson -join [Environment]::NewLine) | ConvertFrom-Json
    if (@($status.generations).Count -ne 3 -or $status.allReady -ne $false) {
        throw 'Native acceptance status must report exactly three incomplete host generations for an empty evidence directory.'
    }
    foreach ($row in @($status.generations)) {
        if ($row.evidenceStatus -ne 'missing' -or $row.pending -ne $row.required -or $row.ready -ne $false) {
            throw "Unexpected empty-evidence status for AutoCAD $($row.generation)."
        }
    }

    $readinessJson = & $readinessScript `
        -ExpectedCommit $commit `
        -ArtifactsDirectory $destination `
        -NativeAcceptedSha '' `
        -SigningPfxBase64 '' `
        -SigningPassword '' `
        -LicenseApiUrl '' `
        -UpdateManifestUrl '' `
        -UpdatePublicKeyPem '' `
        -TelemetryMode 'disabled' `
        -Json
    $readiness = ($readinessJson -join [Environment]::NewLine) | ConvertFrom-Json
    if ($readiness.productionReleaseReady -ne $false) {
        throw 'Commercial readiness preflight must fail closed when native/signing/service prerequisites are absent.'
    }
    $blockedNames = @($readiness.checks | Where-Object { $_.status -eq 'blocked' } | ForEach-Object { $_.name })
    foreach ($expectedBlocker in @('native-acceptance', 'authenticode-signing', 'licensing-service', 'update-service')) {
        if ($blockedNames -notcontains $expectedBlocker) {
            throw "Commercial readiness preflight did not report expected blocker '$expectedBlocker'."
        }
    }

    Write-Host 'Operational readiness tooling smoke passed.'
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
