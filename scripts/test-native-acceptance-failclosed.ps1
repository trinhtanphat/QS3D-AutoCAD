param(
    [Parameter(Mandatory = $true)][string]$Version
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$provenancePath = Join-Path $repo 'artifacts\RELEASE-PROVENANCE.json'
$checksPath = Join-Path $repo 'native-acceptance\required-checks.json'
$validator = Join-Path $repo 'scripts\validate-native-acceptance.ps1'

if (-not (Test-Path -LiteralPath $provenancePath -PathType Leaf)) {
    throw 'Package/provenance must be generated before the native fail-closed smoke.'
}

$provenance = Get-Content -Raw -LiteralPath $provenancePath | ConvertFrom-Json
$contract = Get-Content -Raw -LiteralPath $checksPath | ConvertFrom-Json
$requiredMepIds = @(
    'mep_takeoff_recognition_quantity',
    'mep_clash_classification',
    'mep_clash_locate_selection_safety',
    'mep_exact_clash_review',
    'mep_zoom_selection_review',
    'mep_review_palette_lifecycle',
    'mep_recognition_profile_persistence',
    'mep_multidwg_document_affinity',
    'mep_readonly_nonmutation'
)
$contractIds = @($contract.checks | ForEach-Object { [string]$_.id })
foreach ($requiredMepId in $requiredMepIds) {
    if ($contractIds -notcontains $requiredMepId) {
        throw "Native acceptance contract is missing required MEP check '$requiredMepId'."
    }
}
$tempRoot = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { [IO.Path]::GetTempPath() } else { $env:RUNNER_TEMP }
$root = Join-Path $tempRoot ("qs3d-native-rejection-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$paths = @()

function New-SyntheticPendingEvidence([string]$Generation, [string]$Runtime, [string]$Clr) {
    $now = [DateTimeOffset]::UtcNow.ToString('O')
    $checks = foreach ($check in @($contract.checks)) {
        [ordered]@{
            id = [string]$check.id
            status = 'pending'
            notes = ''
            recordedAtUtc = $null
        }
    }
    $artifacts = foreach ($artifact in @($provenance.artifacts)) {
        [ordered]@{
            file = [string]$artifact.file
            sha256 = ([string]$artifact.sha256).ToLowerInvariant()
            bytes = [int64]$artifact.bytes
        }
    }
    return [ordered]@{
        schemaVersion = 1
        product = 'QS3D AutoCAD'
        sessionId = [Guid]::NewGuid().ToString('N')
        createdAtUtc = $now
        updatedAtUtc = $now
        candidate = [ordered]@{
            version = [string]$provenance.version
            sourceCommit = ([string]$provenance.sourceCommit).ToLowerInvariant()
            signed = [bool]$provenance.signed
            artifacts = @($artifacts)
        }
        host = [ordered]@{
            generation = $Generation
            expectedRuntimeFamily = $Runtime
            observedClrVersion = $Clr
            acadExe = "C:\CI-SYNTHETIC\AutoCAD-$Generation\acad.exe"
            productName = "AutoCAD $Generation CI Synthetic Rejection Test"
            fileVersion = '0.0.0.0'
            productVersion = 'CI-SYNTHETIC-NOT-NATIVE-EVIDENCE'
        }
        operator = [ordered]@{
            name = 'HOSTED-CI-SYNTHETIC'
            machine = 'HOSTED-CI-SYNTHETIC'
            notes = 'This evidence is intentionally synthetic and pending; validator must reject it.'
        }
        checks = @($checks)
    }
}

function Assert-PendingRejected([string[]]$EvidencePaths, [string[]]$RequiredGenerations) {
    $rejected = $false
    try {
        & $validator `
            -Version $Version `
            -EvidencePaths $EvidencePaths `
            -ExpectedCommit ([string]$provenance.sourceCommit) `
            -RequiredGenerations $RequiredGenerations
    }
    catch {
        if ($_.Exception.Message -match "native check '.+' is 'pending', not pass") {
            $rejected = $true
            Write-Host "Expected fail-closed rejection observed: $($_.Exception.Message)"
        }
        else {
            throw
        }
    }

    if (-not $rejected) {
        throw "CRITICAL: synthetic pending native evidence was not rejected for generations $($RequiredGenerations -join ', ')."
    }
}

try {
    foreach ($generation in @('2025','2026','2027')) {
        $runtime = switch ($generation) {
            '2025' { '.NET 8' }
            '2026' { '.NET 8 or .NET 10' }
            '2027' { '.NET 10' }
        }
        $clr = switch ($generation) {
            '2025' { '8.0.0' }
            '2026' { '10.0.0' }
            '2027' { '10.0.0' }
        }
        $evidence = New-SyntheticPendingEvidence -Generation $generation -Runtime $runtime -Clr $clr
        $path = Join-Path $root "AutoCAD-$generation.json"
        $evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding utf8NoBOM
        $paths += $path
    }

    Assert-PendingRejected -EvidencePaths $paths -RequiredGenerations @('2025','2026','2027')

    $legacyPaths = @()
    foreach ($generation in @('2021','2022','2023','2024')) {
        $legacyPath = Join-Path $root "AutoCAD-$generation.json"
        $legacyEvidence = New-SyntheticPendingEvidence -Generation $generation -Runtime '.NET Framework 4.8' -Clr '4.0.30319.42000'
        $legacyEvidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $legacyPath -Encoding utf8NoBOM
        $legacyPaths += $legacyPath
    }
    Assert-PendingRejected -EvidencePaths $legacyPaths -RequiredGenerations @('2021','2022','2023','2024')

    Write-Host 'Native acceptance fail-closed smoke passed for the default 2025/2026/2027 matrix and the separate AutoCAD 2021-2024 legacy matrix. Hosted CI cannot turn pending synthetic evidence into native PASS.'
}
finally {
    Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue
}
