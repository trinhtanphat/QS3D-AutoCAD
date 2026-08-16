$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$ciPath = Join-Path $repo '.github\workflows\ci.yml'
$engineeringPath = Join-Path $repo '.github\workflows\engineering-release.yml'

$ci = Get-Content -Raw -LiteralPath $ciPath
$engineering = Get-Content -Raw -LiteralPath $engineeringPath

$pushStart = $ci.IndexOf("  push:", [StringComparison]::Ordinal)
$prStart = $ci.IndexOf("  pull_request:", [StringComparison]::Ordinal)
if ($pushStart -lt 0 -or $prStart -le $pushStart) {
    throw 'CI trigger regression: unable to isolate push and pull_request trigger blocks.'
}
$pushBlock = $ci.Substring($pushStart, $prStart - $pushStart)
if ($pushBlock.Contains('paths-ignore:', [StringComparison]::Ordinal)) {
    throw 'Main release-chain regression: push trigger must not use paths-ignore because every main SHA requires full CI.'
}
foreach ($required in @(
    '- main',
    '- "agent/**"',
    '- "recovery/**"',
    '- "integration/**"'
)) {
    if (-not $pushBlock.Contains($required, [StringComparison]::Ordinal)) {
        throw "CI push trigger regression: missing '$required'."
    }
}

$concurrencyStart = $ci.IndexOf("concurrency:", [StringComparison]::Ordinal)
$jobsStart = $ci.IndexOf("jobs:", [StringComparison]::Ordinal)
if ($concurrencyStart -lt 0 -or $jobsStart -le $concurrencyStart) {
    throw 'CI concurrency regression: unable to isolate concurrency block.'
}
$concurrencyBlock = $ci.Substring($concurrencyStart, $jobsStart - $concurrencyStart)
$mainSafeCancellation = 'cancel-in-progress: ${{ !(github.event_name == ''push'' && github.ref_name == ''main'') }}'
if (-not $concurrencyBlock.Contains($mainSafeCancellation, [StringComparison]::Ordinal)) {
    throw 'Main CI concurrency regression: task/PR runs may be canceled when stale, but push runs on main must never be canceled by a newer main SHA.'
}
if ($concurrencyBlock.Contains('cancel-in-progress: true', [StringComparison]::Ordinal)) {
    throw 'Main CI concurrency regression: unconditional cancel-in-progress would drop earlier landed main CI/release evidence.'
}

foreach ($required in @(
    'Classify heavy CI requirement',
    'if [[ "$branch" == "main" ]]; then',
    'run_heavy=true',
    'needs: classify',
    "if: needs.classify.outputs.run_heavy == 'true'",
    'Upload verified engineering native candidate',
    'QS3D-AutoCAD-native-candidate-${{ github.sha }}'
)) {
    if (-not $ci.Contains($required, [StringComparison]::Ordinal)) {
        throw "Main exact-CI regression: missing '$required'."
    }
}

foreach ($required in @(
    'workflows: ["CI"]',
    "github.event.workflow_run.conclusion == 'success'",
    "github.event.workflow_run.event == 'push'",
    "github.event.workflow_run.head_branch == 'main'",
    'No successful push CI exists for current main SHA',
    'QS3D-AutoCAD-native-candidate-$env:SOURCE_SHA',
    'Create git tag and GitHub prerelease',
    '$tagRefsJson = gh api "repos/$env:GITHUB_REPOSITORY/git/matching-refs/tags/$tag"',
    '$exactTagRef = @($tagRefs | Where-Object { $_.ref -eq "refs/tags/$tag" }) | Select-Object -First 1',
    'Existing engineering tag target mismatch before release mutation',
    '$releaseTags = @(gh api --paginate "repos/$env:GITHUB_REPOSITORY/releases?per_page=100" --jq ''.[].tag_name'')',
    '$releaseLookupExitCode = $LASTEXITCODE',
    'if ($releaseLookupExitCode -ne 0) {',
    'Unable to inspect engineering releases before mutation',
    '$releaseExists = @($releaseTags | Where-Object { ([string]$_).Trim() -eq $tag }).Count -gt 0',
    '$releaseMetadataJson = gh api "repos/$env:GITHUB_REPOSITORY/releases/tags/$tag"',
    '$releaseMetadataExitCode = $LASTEXITCODE',
    'if ($releaseMetadataExitCode -ne 0) {',
    'Unable to inspect existing engineering release metadata before asset refresh',
    'if (([string]$releaseMetadata.tag_name).Trim() -ne $tag) {',
    'if ($releaseMetadata.prerelease -ne $true -or $releaseMetadata.draft -ne $false) {',
    'Refusing to refresh engineering release'
)) {
    if (-not $engineering.Contains($required, [StringComparison]::Ordinal)) {
        throw "Engineering release-chain regression: missing '$required'."
    }
}

$universalTagValidation = '$tagRefsJson = gh api "repos/$env:GITHUB_REPOSITORY/git/matching-refs/tags/$tag"'
$releaseLookup = '$releaseTags = @(gh api --paginate "repos/$env:GITHUB_REPOSITORY/releases?per_page=100" --jq ''.[].tag_name'')'
$releaseLookupExitCapture = '$releaseLookupExitCode = $LASTEXITCODE'
$releaseLookupFailClosed = 'if ($releaseLookupExitCode -ne 0) {'
$releaseExistsDecision = '$releaseExists = @($releaseTags | Where-Object { ([string]$_).Trim() -eq $tag }).Count -gt 0'
$releaseExistsBranch = 'if ($releaseExists) {'
$releaseMetadataLookup = '$releaseMetadataJson = gh api "repos/$env:GITHUB_REPOSITORY/releases/tags/$tag"'
$releaseMetadataExitCapture = '$releaseMetadataExitCode = $LASTEXITCODE'
$releaseMetadataFailClosed = 'if ($releaseMetadataExitCode -ne 0) {'
$releaseMetadataTagGuard = 'if (([string]$releaseMetadata.tag_name).Trim() -ne $tag) {'
$releaseMetadataPrereleaseGuard = 'if ($releaseMetadata.prerelease -ne $true -or $releaseMetadata.draft -ne $false) {'
$clobberUpload = 'gh release upload $tag @assets --clobber --repo $env:GITHUB_REPOSITORY'
$releaseCreate = 'gh release create $tag @assets'
$universalTagValidationIndex = $engineering.IndexOf($universalTagValidation, [StringComparison]::Ordinal)
$releaseLookupIndex = $engineering.IndexOf($releaseLookup, [StringComparison]::Ordinal)
$releaseLookupExitCaptureIndex = $engineering.IndexOf($releaseLookupExitCapture, [StringComparison]::Ordinal)
$releaseLookupFailClosedIndex = $engineering.IndexOf($releaseLookupFailClosed, [StringComparison]::Ordinal)
$releaseExistsDecisionIndex = $engineering.IndexOf($releaseExistsDecision, [StringComparison]::Ordinal)
$releaseExistsBranchIndex = $engineering.IndexOf($releaseExistsBranch, [StringComparison]::Ordinal)
$releaseMetadataLookupIndex = $engineering.IndexOf($releaseMetadataLookup, [StringComparison]::Ordinal)
$releaseMetadataExitCaptureIndex = $engineering.IndexOf($releaseMetadataExitCapture, [StringComparison]::Ordinal)
$releaseMetadataFailClosedIndex = $engineering.IndexOf($releaseMetadataFailClosed, [StringComparison]::Ordinal)
$releaseMetadataTagGuardIndex = $engineering.IndexOf($releaseMetadataTagGuard, [StringComparison]::Ordinal)
$releaseMetadataPrereleaseGuardIndex = $engineering.IndexOf($releaseMetadataPrereleaseGuard, [StringComparison]::Ordinal)
$clobberUploadIndex = $engineering.IndexOf($clobberUpload, [StringComparison]::Ordinal)
$releaseCreateIndex = $engineering.IndexOf($releaseCreate, [StringComparison]::Ordinal)
if (
    $universalTagValidationIndex -lt 0 -or
    $releaseLookupIndex -lt 0 -or
    $releaseLookupExitCaptureIndex -lt 0 -or
    $releaseLookupFailClosedIndex -lt 0 -or
    $releaseExistsDecisionIndex -lt 0 -or
    $releaseExistsBranchIndex -lt 0 -or
    $releaseMetadataLookupIndex -lt 0 -or
    $releaseMetadataExitCaptureIndex -lt 0 -or
    $releaseMetadataFailClosedIndex -lt 0 -or
    $releaseMetadataTagGuardIndex -lt 0 -or
    $releaseMetadataPrereleaseGuardIndex -lt 0 -or
    $clobberUploadIndex -lt 0 -or
    $releaseCreateIndex -lt 0 -or
    $universalTagValidationIndex -ge $releaseLookupIndex -or
    $releaseLookupIndex -ge $releaseLookupExitCaptureIndex -or
    $releaseLookupExitCaptureIndex -ge $releaseLookupFailClosedIndex -or
    $releaseLookupFailClosedIndex -ge $releaseExistsDecisionIndex -or
    $releaseExistsDecisionIndex -ge $releaseExistsBranchIndex -or
    $releaseExistsBranchIndex -ge $releaseMetadataLookupIndex -or
    $releaseMetadataLookupIndex -ge $releaseMetadataExitCaptureIndex -or
    $releaseMetadataExitCaptureIndex -ge $releaseMetadataFailClosedIndex -or
    $releaseMetadataFailClosedIndex -ge $releaseMetadataTagGuardIndex -or
    $releaseMetadataTagGuardIndex -ge $releaseMetadataPrereleaseGuardIndex -or
    $releaseMetadataPrereleaseGuardIndex -ge $clobberUploadIndex -or
    $releaseExistsDecisionIndex -ge $releaseCreateIndex
) {
    throw 'Engineering release integrity regression: tag validation, successful release lookup and existing-release prerelease metadata validation must precede release mutation.'
}
if ($engineering.Contains('gh release view $tag', [StringComparison]::Ordinal)) {
    throw 'Engineering release lookup regression: gh release view exit status must not be used to infer release absence because lookup failures must fail closed.'
}

Write-Host 'Main exact-CI, non-canceling concurrency and fail-closed engineering prerelease chain guards passed.'
