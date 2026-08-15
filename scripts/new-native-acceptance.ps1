param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][ValidateSet('2021','2025','2026','2027')][string]$HostGeneration,
    [Parameter(Mandatory = $true)][string]$AcadExe,
    [string]$Operator = $env:USERNAME,
    [string]$Notes = '',
    [string]$EvidencePath = '',
    [switch]$RequireSignedCandidate,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$verifyScript = Join-Path $repo 'scripts\verify-artifacts.ps1'
$requiredChecksPath = Join-Path $repo 'native-acceptance\required-checks.json'
$provenancePath = Join-Path $repo 'artifacts\RELEASE-PROVENANCE.json'

if ([string]::IsNullOrWhiteSpace($Operator)) {
    throw 'Operator name is required for native acceptance evidence.'
}
if (-not (Test-Path -LiteralPath $AcadExe -PathType Leaf)) {
    throw "acad.exe was not found: $AcadExe"
}
$acadItem = Get-Item -LiteralPath $AcadExe
if ($acadItem.Name -ne 'acad.exe') {
    throw "Native acceptance must point at a real AutoCAD acad.exe, not '$($acadItem.Name)'."
}

& $verifyScript -Version $Version -RequireSigned:$RequireSignedCandidate

$provenance = Get-Content -Raw -LiteralPath $provenancePath | ConvertFrom-Json
$requiredChecks = Get-Content -Raw -LiteralPath $requiredChecksPath | ConvertFrom-Json
if ($requiredChecks.schemaVersion -ne 1 -or @($requiredChecks.checks).Count -eq 0) {
    throw 'Native acceptance required-check contract is invalid.'
}

$expectedRuntime = switch ($HostGeneration) {
    '2021' { '.NET Framework 4.8' }
    '2027' { '.NET 10' }
    default { '.NET 8' }
}
$matrixMatch = @($provenance.runtimeMatrix) | Where-Object {
    if ($HostGeneration -eq '2021') {
        $_.autoCAD -eq '2021' -and $_.managedRuntime -eq $expectedRuntime
    }
    elseif ($HostGeneration -eq '2027') {
        $_.autoCAD -eq '2027' -and $_.managedRuntime -eq $expectedRuntime
    }
    else {
        $_.autoCAD -eq '2025-2026' -and $_.managedRuntime -eq $expectedRuntime
    }
} | Select-Object -First 1
if ($null -eq $matrixMatch) {
    throw "Release provenance does not contain the expected runtime payload for AutoCAD $HostGeneration / $expectedRuntime."
}

$versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($acadItem.FullName)
if ([string]::IsNullOrWhiteSpace($versionInfo.FileVersion) -or [string]::IsNullOrWhiteSpace($versionInfo.ProductVersion)) {
    throw "Unable to read AutoCAD file/product version from $($acadItem.FullName)."
}
if ([string]::IsNullOrWhiteSpace($versionInfo.ProductName) -or $versionInfo.ProductName -notmatch 'AutoCAD') {
    throw "The selected executable does not identify itself as AutoCAD. ProductName='$($versionInfo.ProductName)'."
}

if ([string]::IsNullOrWhiteSpace($EvidencePath)) {
    $evidenceDirectory = Join-Path $repo 'artifacts\native-acceptance'
    New-Item -ItemType Directory -Force -Path $evidenceDirectory | Out-Null
    $EvidencePath = Join-Path $evidenceDirectory "AutoCAD-$HostGeneration.json"
}
elseif (-not [IO.Path]::IsPathRooted($EvidencePath)) {
    $EvidencePath = Join-Path $repo $EvidencePath
}

if ((Test-Path -LiteralPath $EvidencePath) -and -not $Force) {
    throw "Evidence file already exists: $EvidencePath. Use -Force only when intentionally replacing that session."
}

$now = [DateTimeOffset]::UtcNow.ToString('O')
$checks = foreach ($check in @($requiredChecks.checks)) {
    [ordered]@{
        id = [string]$check.id
        status = 'pending'
        notes = ''
        recordedAtUtc = $null
    }
}

$artifactRecords = foreach ($artifact in @($provenance.artifacts)) {
    [ordered]@{
        file = [string]$artifact.file
        sha256 = ([string]$artifact.sha256).ToLowerInvariant()
        bytes = [int64]$artifact.bytes
    }
}

$evidence = [ordered]@{
    schemaVersion = 1
    product = 'QS3D AutoCAD'
    sessionId = [Guid]::NewGuid().ToString('N')
    createdAtUtc = $now
    updatedAtUtc = $now
    candidate = [ordered]@{
        version = [string]$provenance.version
        sourceCommit = ([string]$provenance.sourceCommit).ToLowerInvariant()
        signed = [bool]$provenance.signed
        artifacts = @($artifactRecords)
    }
    host = [ordered]@{
        generation = $HostGeneration
        expectedRuntimeFamily = $expectedRuntime
        observedClrVersion = $null
        acadExe = $acadItem.FullName
        productName = [string]$versionInfo.ProductName
        fileVersion = [string]$versionInfo.FileVersion
        productVersion = [string]$versionInfo.ProductVersion
    }
    operator = [ordered]@{
        name = $Operator.Trim()
        machine = [Environment]::MachineName
        notes = $Notes
    }
    checks = @($checks)
}

$parent = Split-Path -Parent $EvidencePath
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
$temp = "$EvidencePath.tmp-$([Guid]::NewGuid().ToString('N'))"
try {
    $evidence | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $temp -Encoding utf8NoBOM
    Move-Item -Force -LiteralPath $temp -Destination $EvidencePath
}
finally {
    Remove-Item -Force -LiteralPath $temp -ErrorAction SilentlyContinue
}

Write-Host "Created native acceptance session: $EvidencePath"
Write-Host "Candidate SHA: $($evidence.candidate.sourceCommit)"
Write-Host "AutoCAD ${HostGeneration}: $($versionInfo.ProductVersion)"
Write-Host 'All acceptance checks start as pending. Hosted/source CI does not set native PASS results.'
