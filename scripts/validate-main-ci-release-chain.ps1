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
    'Create git tag and GitHub prerelease'
)) {
    if (-not $engineering.Contains($required, [StringComparison]::Ordinal)) {
        throw "Engineering release-chain regression: missing '$required'."
    }
}

Write-Host 'Main exact-CI and engineering prerelease chain guards passed.'
