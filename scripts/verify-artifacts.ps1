param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$ExpectedCommit = ''
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repo 'artifacts'
$zipName = "QS3D-AutoCAD-$Version.zip"
$setupName = "QS3D-AutoCAD-$Version-Setup.exe"
$provenanceName = 'RELEASE-PROVENANCE.json'
$checksumsName = 'SHA256SUMS.txt'
$zip = Join-Path $artifacts $zipName
$setup = Join-Path $artifacts $setupName
$provenance = Join-Path $artifacts $provenanceName
$checksums = Join-Path $artifacts $checksumsName

foreach ($path in @($zip, $setup, $provenance, $checksums)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Release verification input is missing: $path"
    }
}

if ([string]::IsNullOrWhiteSpace($ExpectedCommit)) {
    Push-Location $repo
    try {
        $ExpectedCommit = (& git rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0) {
            throw 'Unable to resolve expected git commit.'
        }
    }
    finally {
        Pop-Location
    }
}

if ($ExpectedCommit -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Expected commit must be a full 40-character SHA: $ExpectedCommit"
}
$ExpectedCommit = $ExpectedCommit.ToLowerInvariant()

$manifest = Get-Content -Raw -LiteralPath $provenance | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1) { throw "Unsupported release provenance schema: $($manifest.schemaVersion)" }
if ($manifest.product -ne 'QS3D AutoCAD') { throw "Unexpected release product: $($manifest.product)" }
if ($manifest.version -ne $Version) { throw "Provenance version mismatch: expected $Version, got $($manifest.version)" }
if ([string]$manifest.sourceCommit -ne $ExpectedCommit) { throw "Provenance source commit mismatch: expected $ExpectedCommit, got $($manifest.sourceCommit)" }
if ($manifest.sourceDirty -ne $false) { throw 'Release provenance reports a dirty source tree.' }

$matrix = @($manifest.runtimeMatrix)
if ($matrix.Count -ne 2) { throw 'Release provenance must contain exactly two AutoCAD runtime families.' }
$net8 = $matrix | Where-Object { $_.targetFramework -eq 'net8.0-windows' -and $_.autoCAD -eq '2025-2026' }
$net10 = $matrix | Where-Object { $_.targetFramework -eq 'net10.0-windows' -and $_.autoCAD -eq '2027' }
if ($null -eq $net8) { throw 'Release provenance is missing the AutoCAD 2025-2026 / .NET 8 payload.' }
if ($null -eq $net10) { throw 'Release provenance is missing the AutoCAD 2027 / .NET 10 payload.' }

$records = @($manifest.artifacts)
foreach ($fileName in @($zipName, $setupName)) {
    $record = $records | Where-Object { $_.file -eq $fileName } | Select-Object -First 1
    if ($null -eq $record) { throw "Provenance is missing artifact record: $fileName" }
    $path = Join-Path $artifacts $fileName
    $item = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    if ([string]$record.sha256 -ne $hash) { throw "Provenance SHA-256 mismatch for $fileName" }
    if ([int64]$record.bytes -ne $item.Length) { throw "Provenance byte-length mismatch for $fileName" }
}

$checksumMap = @{}
foreach ($line in Get-Content -LiteralPath $checksums) {
    if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') {
        throw "Malformed SHA256SUMS line: $line"
    }
    $checksumMap[$Matches[2]] = $Matches[1].ToLowerInvariant()
}

foreach ($fileName in @($zipName, $setupName, $provenanceName)) {
    if (-not $checksumMap.ContainsKey($fileName)) { throw "SHA256SUMS is missing $fileName" }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $artifacts $fileName)).Hash.ToLowerInvariant()
    if ($checksumMap[$fileName] -ne $actual) { throw "SHA256SUMS mismatch for $fileName" }
}

Write-Host "Release artifacts verified for version $Version at commit $ExpectedCommit."
Write-Host "Authenticode signing recorded by provenance: $($manifest.signed)"
