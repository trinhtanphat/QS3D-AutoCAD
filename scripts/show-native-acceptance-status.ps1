[CmdletBinding()]
param(
    [string]$ArtifactsDirectory = '',
    [string]$EvidenceDirectory = '',
    [string[]]$Generations = @('2025','2026','2027'),
    [switch]$NoHostDiscovery,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$supportedGenerations = @('2021','2025','2026','2027')
$Generations = @($Generations | ForEach-Object { [string]$_ } | Select-Object -Unique)
if ($Generations.Count -eq 0) { throw 'At least one AutoCAD generation must be requested.' }
foreach ($generation in $Generations) {
    if ($generation -notin $supportedGenerations) { throw "Unsupported AutoCAD generation '$generation'." }
}
$artifacts = if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    Join-Path $repo 'artifacts'
}
else {
    [IO.Path]::GetFullPath($ArtifactsDirectory)
}
$evidenceRoot = if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    Join-Path $artifacts 'native-acceptance'
}
else {
    [IO.Path]::GetFullPath($EvidenceDirectory)
}
$requiredPath = Join-Path $repo 'native-acceptance\required-checks.json'
$requiredContract = Get-Content -Raw -LiteralPath $requiredPath | ConvertFrom-Json
$requiredChecks = @($requiredContract.checks)
if ($requiredContract.schemaVersion -ne 1 -or $requiredChecks.Count -eq 0) {
    throw 'Native acceptance required-check contract is invalid.'
}

$currentCandidate = $null
$provenancePath = Join-Path $artifacts 'RELEASE-PROVENANCE.json'
if (Test-Path -LiteralPath $provenancePath -PathType Leaf) {
    try {
        $provenance = Get-Content -Raw -LiteralPath $provenancePath | ConvertFrom-Json
        $currentCandidate = [ordered]@{
            version = [string]$provenance.version
            sourceCommit = ([string]$provenance.sourceCommit).ToLowerInvariant()
            signed = [bool]$provenance.signed
        }
    }
    catch {
        $currentCandidate = [ordered]@{
            version = $null
            sourceCommit = $null
            signed = $null
            error = "Invalid RELEASE-PROVENANCE.json: $($_.Exception.Message)"
        }
    }
}

function Find-AcadExe([string]$Generation, $Evidence) {
    if ($NoHostDiscovery) { return $null }

    if ($null -ne $Evidence -and $null -ne $Evidence.host -and -not [string]::IsNullOrWhiteSpace([string]$Evidence.host.acadExe)) {
        $recorded = [string]$Evidence.host.acadExe
        if (Test-Path -LiteralPath $recorded -PathType Leaf) { return $recorded }
    }

    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    if (-not [string]::IsNullOrWhiteSpace($programFiles)) {
        $common = Join-Path $programFiles "Autodesk\AutoCAD $Generation\acad.exe"
        if (Test-Path -LiteralPath $common -PathType Leaf) { return $common }
    }
    return $null
}

$rows = foreach ($generation in $Generations) {
    $evidencePath = Join-Path $evidenceRoot "AutoCAD-$generation.json"
    $evidence = $null
    $invalid = @()
    if (Test-Path -LiteralPath $evidencePath -PathType Leaf) {
        try {
            $evidence = Get-Content -Raw -LiteralPath $evidencePath | ConvertFrom-Json
            if ([string]$evidence.host.generation -ne $generation) {
                $invalid += "host.generation is '$($evidence.host.generation)'"
            }
        }
        catch {
            $invalid += "invalid JSON: $($_.Exception.Message)"
        }
    }

    $pass = 0
    $fail = 0
    $blocked = 0
    $pending = 0
    $seen = @{}
    if ($null -ne $evidence) {
        foreach ($check in @($evidence.checks)) {
            $id = [string]$check.id
            if ($seen.ContainsKey($id)) {
                $invalid += "duplicate check '$id'"
                continue
            }
            $seen[$id] = $check
        }
    }

    foreach ($required in $requiredChecks) {
        $id = [string]$required.id
        if (-not $seen.ContainsKey($id)) {
            $pending++
            continue
        }
        $status = ([string]$seen[$id].status).ToLowerInvariant()
        switch ($status) {
            'pass' { $pass++ }
            'fail' { $fail++ }
            'blocked' { $blocked++ }
            'pending' { $pending++ }
            default {
                $pending++
                $invalid += "invalid status '$status' for '$id'"
            }
        }
    }

    $candidateSha = if ($null -ne $evidence) { ([string]$evidence.candidate.sourceCommit).ToLowerInvariant() } else { $null }
    $candidateMatch = if ($null -ne $currentCandidate -and -not [string]::IsNullOrWhiteSpace([string]$currentCandidate.sourceCommit) -and -not [string]::IsNullOrWhiteSpace($candidateSha)) {
        $candidateSha -eq [string]$currentCandidate.sourceCommit
    }
    else {
        $null
    }
    $acadExe = Find-AcadExe $generation $evidence
    $ready = $null -ne $evidence -and $invalid.Count -eq 0 -and $pass -eq $requiredChecks.Count -and $fail -eq 0 -and $blocked -eq 0 -and $pending -eq 0 -and ($candidateMatch -ne $false)
    $statusLabel = if ($null -eq $evidence) {
        'missing'
    }
    elseif ($invalid.Count -gt 0) {
        'invalid'
    }
    elseif ($ready) {
        'pass'
    }
    else {
        'in-progress'
    }

    [pscustomobject][ordered]@{
        generation = $generation
        acadDetected = -not [string]::IsNullOrWhiteSpace($acadExe)
        acadExe = $acadExe
        evidenceStatus = $statusLabel
        evidencePath = if (Test-Path -LiteralPath $evidencePath -PathType Leaf) { $evidencePath } else { $null }
        candidateSha = $candidateSha
        currentCandidateMatch = $candidateMatch
        pass = $pass
        fail = $fail
        blocked = $blocked
        pending = $pending
        required = $requiredChecks.Count
        ready = $ready
        problems = @($invalid)
    }
}

$result = [ordered]@{
    schemaVersion = 1
    currentCandidate = $currentCandidate
    evidenceDirectory = $evidenceRoot
    requestedGenerations = @($Generations)
    generations = @($rows)
    allReady = (@($rows | Where-Object { -not $_.ready }).Count -eq 0)
}

if ($Json) {
    $result | ConvertTo-Json -Depth 8
    return
}

if ($null -eq $currentCandidate) {
    Write-Host 'Current candidate: none prepared under artifacts/.'
}
elseif ($currentCandidate.Contains('error')) {
    Write-Host "Current candidate: INVALID - $($currentCandidate.error)"
}
else {
    Write-Host "Current candidate: $($currentCandidate.version) / $($currentCandidate.sourceCommit) / signed=$($currentCandidate.signed)"
}
$rows | Select-Object generation, acadDetected, evidenceStatus, currentCandidateMatch, pass, fail, blocked, pending, required, ready | Format-Table -AutoSize
if (-not $result.allReady) {
    Write-Host 'Native acceptance is not complete. Only real licensed AutoCAD evidence may move pending/blocked/fail checks to pass.'
}
else {
    Write-Host 'All requested native evidence files report complete passing coverage for the prepared candidate. Run validate-native-acceptance.ps1 with the same generation set for fail-closed final validation.'
}
