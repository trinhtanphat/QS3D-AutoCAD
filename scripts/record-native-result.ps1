param(
    [Parameter(Mandatory = $true)][string]$EvidencePath,
    [Parameter(Mandatory = $true)][string]$CheckId,
    [Parameter(Mandatory = $true)][ValidateSet('pending','pass','fail','blocked')][string]$Status,
    [string]$Notes = ''
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$requiredChecksPath = Join-Path $repo 'native-acceptance\required-checks.json'

if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) {
    throw "Native acceptance evidence file not found: $EvidencePath"
}

$contract = Get-Content -Raw -LiteralPath $requiredChecksPath | ConvertFrom-Json
$knownCheck = @($contract.checks | Where-Object { $_.id -eq $CheckId })
if ($knownCheck.Count -ne 1) {
    throw "Unknown or duplicate native acceptance check id: $CheckId"
}
if ($CheckId -eq 'runtime_identity') {
    throw 'Use scripts/record-native-runtime.ps1 for runtime_identity so observed CLR data is recorded structurally.'
}
if ($Status -ne 'pending' -and [string]::IsNullOrWhiteSpace($Notes)) {
    throw "Evidence notes are required when recording '$Status' for $CheckId. Include what was tested and the observed result."
}

$evidence = Get-Content -Raw -LiteralPath $EvidencePath | ConvertFrom-Json
if ($evidence.schemaVersion -ne 1 -or $evidence.product -ne 'QS3D AutoCAD') {
    throw 'Evidence file is not a supported QS3D AutoCAD native acceptance session.'
}

$matches = @($evidence.checks | Where-Object { $_.id -eq $CheckId })
if ($matches.Count -ne 1) {
    throw "Evidence must contain exactly one '$CheckId' check."
}

$now = [DateTimeOffset]::UtcNow.ToString('O')
$matches[0].status = $Status
$matches[0].notes = if ($Status -eq 'pending') { '' } else { $Notes.Trim() }
$matches[0].recordedAtUtc = if ($Status -eq 'pending') { $null } else { $now }
$evidence.updatedAtUtc = $now

$temp = "$EvidencePath.tmp-$([Guid]::NewGuid().ToString('N'))"
try {
    $evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $temp -Encoding utf8NoBOM
    Move-Item -Force -LiteralPath $temp -Destination $EvidencePath
}
finally {
    Remove-Item -Force -LiteralPath $temp -ErrorAction SilentlyContinue
}

Write-Host "Recorded $CheckId = $Status for AutoCAD $($evidence.host.generation)."
if ($Status -eq 'fail') {
    Write-Warning 'A native acceptance failure is recorded. The final validator will refuse this candidate until the check is re-tested and explicitly recorded as pass.'
}
