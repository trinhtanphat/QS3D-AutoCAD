param(
    [Parameter(Mandatory = $true)][string]$EvidencePath,
    [Parameter(Mandatory = $true)][string]$ObservedClrVersion,
    [string]$Notes = ''
)

$ErrorActionPreference = 'Stop'
$legacyGenerations = @('2021','2022','2023','2024')
$supportedGenerations = $legacyGenerations + @('2025','2026','2027')

if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) {
    throw "Native acceptance evidence file not found: $EvidencePath"
}
if ($ObservedClrVersion -notmatch '^(?<major>\d+)(?:\.\d+){1,3}(?:[-+].+)?$') {
    throw "Observed CLR version must be a concrete version such as 4.0.30319.42000, 8.0.22 or 10.0.0: $ObservedClrVersion"
}

$evidence = Get-Content -Raw -LiteralPath $EvidencePath | ConvertFrom-Json
if ($evidence.schemaVersion -ne 1 -or $evidence.product -ne 'QS3D AutoCAD') {
    throw 'Evidence file is not a supported QS3D AutoCAD native acceptance session.'
}
$generation = [string]$evidence.host.generation
if ($generation -notin $supportedGenerations) {
    throw "Evidence contains unsupported AutoCAD generation '$generation'."
}

$allowedMajors = switch ($generation) {
    '2021' { @(4) }
    '2022' { @(4) }
    '2023' { @(4) }
    '2024' { @(4) }
    '2025' { @(8) }
    '2026' { @(8, 10) }
    '2027' { @(10) }
    default { @() }
}
$actualMajor = [int]$Matches.major
$status = if ($actualMajor -in $allowedMajors) { 'pass' } else { 'fail' }
$now = [DateTimeOffset]::UtcNow.ToString('O')
$evidence.host.observedClrVersion = $ObservedClrVersion
$evidence.updatedAtUtc = $now

$runtimeCheck = @($evidence.checks | Where-Object { $_.id -eq 'runtime_identity' })
if ($runtimeCheck.Count -ne 1) {
    throw 'Evidence contract must contain exactly one runtime_identity check.'
}
$allowedMajorText = $allowedMajors -join ' or '
$runtimeCheck[0].status = $status
$runtimeCheck[0].recordedAtUtc = $now
$runtimeCheck[0].notes = if ([string]::IsNullOrWhiteSpace($Notes)) {
    "Observed CLR $ObservedClrVersion; allowed major $allowedMajorText for AutoCAD $generation ($($evidence.host.expectedRuntimeFamily))."
}
else {
    $Notes
}

$temp = "$EvidencePath.tmp-$([Guid]::NewGuid().ToString('N'))"
try {
    $evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $temp -Encoding utf8NoBOM
    Move-Item -Force -LiteralPath $temp -Destination $EvidencePath
}
finally {
    Remove-Item -Force -LiteralPath $temp -ErrorAction SilentlyContinue
}

Write-Host "Recorded CLR $ObservedClrVersion for AutoCAD ${generation}: $status"
if ($status -ne 'pass') {
    throw "Observed CLR $ObservedClrVersion does not match the allowed runtime family $($evidence.host.expectedRuntimeFamily)."
}
