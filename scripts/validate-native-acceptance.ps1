param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string[]]$EvidencePaths = @(),
    [string]$EvidenceDirectory = '',
    [string]$ExpectedCommit = '',
    [string[]]$RequiredGenerations = @('2025','2026','2027'),
    [switch]$RequireSignedCandidate
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$verifyArtifacts = Join-Path $repo 'scripts\verify-artifacts.ps1'
$provenancePath = Join-Path $repo 'artifacts\RELEASE-PROVENANCE.json'
$requiredChecksPath = Join-Path $repo 'native-acceptance\required-checks.json'
$supportedGenerations = @('2021','2025','2026','2027')

$RequiredGenerations = @($RequiredGenerations | ForEach-Object { [string]$_ } | Select-Object -Unique)
if ($RequiredGenerations.Count -eq 0) { throw 'At least one required AutoCAD generation must be specified.' }
foreach ($requiredGeneration in $RequiredGenerations) {
    if ($requiredGeneration -notin $supportedGenerations) {
        throw "Unsupported required AutoCAD generation '$requiredGeneration'."
    }
}

& $verifyArtifacts -Version $Version -ExpectedCommit $ExpectedCommit -RequireSigned:$RequireSignedCandidate

$provenance = Get-Content -Raw -LiteralPath $provenancePath | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($ExpectedCommit)) {
    $ExpectedCommit = [string]$provenance.sourceCommit
}
$ExpectedCommit = $ExpectedCommit.ToLowerInvariant()

$contract = Get-Content -Raw -LiteralPath $requiredChecksPath | ConvertFrom-Json
$requiredIds = @($contract.checks | ForEach-Object { [string]$_.id })
if ($contract.schemaVersion -ne 1 -or $requiredIds.Count -eq 0 -or @($requiredIds | Select-Object -Unique).Count -ne $requiredIds.Count) {
    throw 'Native acceptance required-check contract is invalid or contains duplicate ids.'
}

if ($EvidencePaths.Count -eq 0) {
    if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
        $EvidenceDirectory = Join-Path $repo 'artifacts\native-acceptance'
    }
    elseif (-not [IO.Path]::IsPathRooted($EvidenceDirectory)) {
        $EvidenceDirectory = Join-Path $repo $EvidenceDirectory
    }

    if (-not (Test-Path -LiteralPath $EvidenceDirectory -PathType Container)) {
        throw "Native acceptance evidence directory not found: $EvidenceDirectory"
    }
    $EvidencePaths = @($RequiredGenerations | ForEach-Object {
        Join-Path $EvidenceDirectory "AutoCAD-$_.json"
    })
}
else {
    $EvidencePaths = @($EvidencePaths | ForEach-Object {
        if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $repo $_ }
    })
}

if ($EvidencePaths.Count -ne $RequiredGenerations.Count) {
    throw "Exactly $($RequiredGenerations.Count) native acceptance evidence file(s) are required for generations $($RequiredGenerations -join ', '); found $($EvidencePaths.Count)."
}

$provenanceArtifacts = @{}
foreach ($artifact in @($provenance.artifacts)) {
    $name = [string]$artifact.file
    if ($provenanceArtifacts.ContainsKey($name)) { throw "Duplicate provenance artifact: $name" }
    $provenanceArtifacts[$name] = [ordered]@{
        sha256 = ([string]$artifact.sha256).ToLowerInvariant()
        bytes = [int64]$artifact.bytes
    }
}

$evidenceRecords = @()
foreach ($path in $EvidencePaths) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Native acceptance evidence file not found: $path"
    }

    $raw = Get-Content -Raw -LiteralPath $path
    $evidence = $raw | ConvertFrom-Json
    $fileName = Split-Path -Leaf $path

    if ($evidence.schemaVersion -ne 1 -or $evidence.product -ne 'QS3D AutoCAD') {
        throw "${fileName}: unsupported evidence schema/product."
    }
    if ([string]$evidence.sessionId -notmatch '^[0-9a-f]{32}$') {
        throw "${fileName}: invalid sessionId."
    }
    foreach ($timestampName in @('createdAtUtc','updatedAtUtc')) {
        $parsed = [DateTimeOffset]::MinValue
        if (-not [DateTimeOffset]::TryParse([string]$evidence.$timestampName, [ref]$parsed)) {
            throw "${fileName}: invalid $timestampName."
        }
    }

    if ([string]$evidence.candidate.version -ne $Version) {
        throw "${fileName}: candidate version mismatch; expected $Version, got $($evidence.candidate.version)."
    }
    if (([string]$evidence.candidate.sourceCommit).ToLowerInvariant() -ne $ExpectedCommit) {
        throw "${fileName}: candidate source SHA mismatch; expected $ExpectedCommit, got $($evidence.candidate.sourceCommit)."
    }
    if ([bool]$evidence.candidate.signed -ne [bool]$provenance.signed) {
        throw "${fileName}: candidate signing state does not match release provenance."
    }
    if ($RequireSignedCandidate -and -not [bool]$evidence.candidate.signed) {
        throw "${fileName}: signed native acceptance evidence was required."
    }

    $candidateArtifacts = @($evidence.candidate.artifacts)
    if ($candidateArtifacts.Count -ne $provenanceArtifacts.Count) {
        throw "${fileName}: artifact record count does not match release provenance."
    }
    $seenArtifactNames = @{}
    foreach ($artifact in $candidateArtifacts) {
        $name = [string]$artifact.file
        if ($seenArtifactNames.ContainsKey($name)) { throw "${fileName}: duplicate candidate artifact '$name'." }
        $seenArtifactNames[$name] = $true
        if (-not $provenanceArtifacts.ContainsKey($name)) { throw "${fileName}: unknown candidate artifact '$name'." }
        $expectedArtifact = $provenanceArtifacts[$name]
        if (([string]$artifact.sha256).ToLowerInvariant() -ne $expectedArtifact.sha256 -or [int64]$artifact.bytes -ne $expectedArtifact.bytes) {
            throw "${fileName}: candidate artifact hash/size mismatch for '$name'."
        }
    }

    $generation = [string]$evidence.host.generation
    if ($generation -notin $supportedGenerations) {
        throw "${fileName}: invalid AutoCAD generation '$generation'."
    }
    if ($generation -notin $RequiredGenerations) {
        throw "${fileName}: AutoCAD generation '$generation' was not requested for this validation."
    }
    $expectedRuntime = switch ($generation) {
        '2021' { '.NET Framework 4.8' }
        '2027' { '.NET 10' }
        default { '.NET 8' }
    }
    if ([string]$evidence.host.expectedRuntimeFamily -ne $expectedRuntime) {
        throw "${fileName}: expected runtime family must be $expectedRuntime for AutoCAD $generation."
    }
    if ([string]::IsNullOrWhiteSpace([string]$evidence.host.observedClrVersion)) {
        throw "${fileName}: observedClrVersion is missing. Record it with record-native-runtime.ps1 after loading QS3D in AutoCAD."
    }
    if ([string]$evidence.host.observedClrVersion -notmatch '^(?<major>\d+)\.') {
        throw "${fileName}: observed CLR version is malformed: $($evidence.host.observedClrVersion)."
    }
    $expectedMajor = switch ($generation) {
        '2021' { 4 }
        '2027' { 10 }
        default { 8 }
    }
    if ([int]$Matches.major -ne $expectedMajor) {
        throw "${fileName}: observed CLR $($evidence.host.observedClrVersion) does not match expected $expectedRuntime."
    }
    if ([string]::IsNullOrWhiteSpace([string]$evidence.host.productName) -or [string]$evidence.host.productName -notmatch 'AutoCAD') {
        throw "${fileName}: host productName does not identify AutoCAD."
    }
    foreach ($field in @('acadExe','fileVersion','productVersion')) {
        if ([string]::IsNullOrWhiteSpace([string]$evidence.host.$field)) {
            throw "${fileName}: host $field is missing."
        }
    }
    foreach ($field in @('name','machine')) {
        if ([string]::IsNullOrWhiteSpace([string]$evidence.operator.$field)) {
            throw "${fileName}: operator $field is missing."
        }
    }

    $checks = @($evidence.checks)
    $ids = @($checks | ForEach-Object { [string]$_.id })
    if ($checks.Count -ne $requiredIds.Count -or @($ids | Select-Object -Unique).Count -ne $ids.Count) {
        throw "${fileName}: evidence must contain every required check exactly once."
    }
    foreach ($requiredId in $requiredIds) {
        if ($ids -notcontains $requiredId) {
            throw "${fileName}: missing required check '$requiredId'."
        }
    }
    foreach ($id in $ids) {
        if ($requiredIds -notcontains $id) {
            throw "${fileName}: unknown check '$id'."
        }
    }

    foreach ($check in $checks) {
        if ([string]$check.status -ne 'pass') {
            throw "${fileName}: native check '$($check.id)' is '$($check.status)', not pass. Hosted CI must not override this."
        }
        if ([string]::IsNullOrWhiteSpace([string]$check.notes)) {
            throw "${fileName}: passing check '$($check.id)' must include evidence notes."
        }
        $recorded = [DateTimeOffset]::MinValue
        if (-not [DateTimeOffset]::TryParse([string]$check.recordedAtUtc, [ref]$recorded)) {
            throw "${fileName}: passing check '$($check.id)' has no valid recordedAtUtc timestamp."
        }
    }

    $fileHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    $evidenceRecords += [ordered]@{
        generation = $generation
        sessionId = [string]$evidence.sessionId
        evidenceFile = $fileName
        evidenceSha256 = $fileHash
        autoCADProductName = [string]$evidence.host.productName
        autoCADProductVersion = [string]$evidence.host.productVersion
        autoCADFileVersion = [string]$evidence.host.fileVersion
        observedClrVersion = [string]$evidence.host.observedClrVersion
        operator = [string]$evidence.operator.name
        machine = [string]$evidence.operator.machine
    }
}

$generations = @($evidenceRecords | ForEach-Object { $_.generation })
foreach ($requiredGeneration in $RequiredGenerations) {
    if (@($generations | Where-Object { $_ -eq $requiredGeneration }).Count -ne 1) {
        throw "Native acceptance requires exactly one passing AutoCAD $requiredGeneration session."
    }
}
$sessionIds = @($evidenceRecords | ForEach-Object { $_.sessionId } | Select-Object -Unique)
if ($sessionIds.Count -ne $RequiredGenerations.Count) {
    throw 'Native acceptance evidence sessions must have distinct sessionId values.'
}

$outputDirectory = Join-Path $repo 'artifacts\native-acceptance'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$summaryPath = Join-Path $outputDirectory 'NATIVE-ACCEPTANCE-SUMMARY.json'
$shaPath = Join-Path $outputDirectory 'NATIVE-ACCEPTED-SHA.txt'
$summary = [ordered]@{
    schemaVersion = 1
    product = 'QS3D AutoCAD'
    version = $Version
    sourceCommit = $ExpectedCommit
    candidateSigned = [bool]$provenance.signed
    requiredGenerations = @($RequiredGenerations)
    validatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    hosts = @($evidenceRecords | Sort-Object generation)
}
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath -Encoding utf8NoBOM
Set-Content -LiteralPath $shaPath -Value $ExpectedCommit -Encoding ascii

Write-Host "NATIVE ACCEPTANCE EVIDENCE VALIDATED for exact source SHA $ExpectedCommit"
Write-Host "Generations: $($RequiredGenerations -join ', ')"
Write-Host "Summary: $summaryPath"
Write-Host "Accepted SHA candidate file: $shaPath"
Write-Host 'This validator does not modify GitHub variables or publish a release. A human/release owner must review the evidence before setting QS3D_NATIVE_ACCEPTED_SHA.'
