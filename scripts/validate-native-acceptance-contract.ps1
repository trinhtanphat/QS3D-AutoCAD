$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$schemaPath = Join-Path $repo 'native-acceptance\evidence.schema.json'
$checksPath = Join-Path $repo 'native-acceptance\required-checks.json'
$scriptPaths = @(
    (Join-Path $repo 'scripts\new-native-acceptance.ps1')
    (Join-Path $repo 'scripts\record-native-runtime.ps1')
    (Join-Path $repo 'scripts\record-native-result.ps1')
    (Join-Path $repo 'scripts\validate-native-acceptance.ps1')
)

foreach ($path in @($schemaPath, $checksPath) + $scriptPaths) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Native acceptance contract file is missing: $path"
    }
}

$schema = Get-Content -Raw -LiteralPath $schemaPath | ConvertFrom-Json
$contract = Get-Content -Raw -LiteralPath $checksPath | ConvertFrom-Json
if ($schema.title -ne 'QS3D AutoCAD native acceptance evidence') {
    throw 'Native acceptance evidence schema identity changed unexpectedly.'
}
if ($contract.schemaVersion -ne 1) {
    throw "Unsupported native acceptance check contract schema: $($contract.schemaVersion)"
}

$ids = @($contract.checks | ForEach-Object { [string]$_.id })
if ($ids.Count -lt 20 -or @($ids | Select-Object -Unique).Count -ne $ids.Count) {
    throw 'Native acceptance check ids must be unique and must not silently lose acceptance coverage.'
}

$criticalChecks = @(
    'installer_exact_candidate',
    'bundle_command_autoload',
    'palette_startup',
    'runtime_identity',
    'boq_semantics',
    'browser_selection_sync',
    'level_dependency_propagation',
    'grid_dependency_safety',
    'undo_redo',
    'save_reopen_persistence',
    'restart_lazy_load',
    'upgrade_transaction',
    'uninstall',
    'ribbon_surface',
    'ribbon_visual_qa',
    'artifact_provenance_match'
)
foreach ($id in $criticalChecks) {
    if ($ids -notcontains $id) {
        throw "Native acceptance regression: required gate '$id' is missing."
    }
}

$parser = [System.Management.Automation.Language.Parser]
foreach ($path in $scriptPaths) {
    $tokens = $null
    $parseErrors = $null
    [void]$parser::ParseFile($path, [ref]$tokens, [ref]$parseErrors)
    if (@($parseErrors).Count -gt 0) {
        $messages = @($parseErrors | ForEach-Object { $_.Message }) -join '; '
        throw "PowerShell parse failure in $(Split-Path -Leaf $path): $messages"
    }
}

$newSource = Get-Content -Raw -LiteralPath (Join-Path $repo 'scripts\new-native-acceptance.ps1')
$recordSource = Get-Content -Raw -LiteralPath (Join-Path $repo 'scripts\record-native-result.ps1')
$runtimeSource = Get-Content -Raw -LiteralPath (Join-Path $repo 'scripts\record-native-runtime.ps1')
$validateSource = Get-Content -Raw -LiteralPath (Join-Path $repo 'scripts\validate-native-acceptance.ps1')

foreach ($requiredSnippet in @('All acceptance checks start as pending', "status = 'pending'", 'verify-artifacts.ps1', 'GetVersionInfo')) {
    if (-not $newSource.Contains($requiredSnippet, [StringComparison]::Ordinal)) {
        throw "Native session creation regression: missing '$requiredSnippet'."
    }
}
foreach ($requiredSnippet in @("ValidateSet('pending','pass','fail','blocked')", 'Evidence notes are required', 'runtime_identity')) {
    if (-not $recordSource.Contains($requiredSnippet, [StringComparison]::Ordinal)) {
        throw "Native result recorder regression: missing '$requiredSnippet'."
    }
}
foreach ($requiredSnippet in @('observedClrVersion', "generation -eq '2027'", "{ 10 } else { 8 }")) {
    if (-not $runtimeSource.Contains($requiredSnippet, [StringComparison]::Ordinal)) {
        throw "Native runtime recorder regression: missing '$requiredSnippet'."
    }
}
foreach ($requiredSnippet in @(
    'Exactly three native acceptance evidence files are required',
    "@('2025','2026','2027')",
    "status -ne 'pass'",
    'distinct sessionId values',
    'does not modify GitHub variables or publish a release'
)) {
    if (-not $validateSource.Contains($requiredSnippet, [StringComparison]::Ordinal)) {
        throw "Native validator regression: missing '$requiredSnippet'."
    }
}
if (-not ($ids -contains 'ribbon_surface')) {
    throw 'Native validator contract must keep Ribbon as a required native gate.'
}

$combinedScripts = ($scriptPaths | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
foreach ($forbidden in @('gh variable set', 'gh release create', 'git tag', 'SetEnvironmentVariable(')) {
    if ($combinedScripts.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Native acceptance tooling must not mutate release state automatically: found '$forbidden'."
    }
}

Write-Host "Native acceptance contract guards passed with $($ids.Count) required native checks."
Write-Host 'Hosted CI validates tooling only; it does not create native PASS evidence.'
