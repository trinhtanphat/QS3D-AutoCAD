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
$tempRoot = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { [IO.Path]::GetTempPath() } else { $env:RUNNER_TEMP }
$root = Join-Path $tempRoot ("qs3d-native-rejection-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$paths = @()

try {
    foreach ($generation in @('2025','2026','2027')) {
        $now = [DateTimeOffset]::UtcNow.ToString('O')
        $runtime = if ($generation -eq '2027') { '.NET 10' } else { '.NET 8' }
        $clr = if ($generation -eq '2027') { '10.0.0' } else { '8.0.0' }
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
                artifacts = @($artifacts)
            }
            host = [ordered]@{
                generation = $generation
                expectedRuntimeFamily = $runtime
                observedClrVersion = $clr
                acadExe = "C:\CI-SYNTHETIC\AutoCAD-$generation\acad.exe"
                productName = "AutoCAD $generation CI Synthetic Rejection Test"
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
        $path = Join-Path $root "AutoCAD-$generation.json"
        $evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding utf8NoBOM
        $paths += $path
    }

    $rejected = $false
    try {
        & $validator -Version $Version -EvidencePaths $paths -ExpectedCommit ([string]$provenance.sourceCommit)
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
        throw 'CRITICAL: synthetic pending native evidence was not rejected by the final validator.'
    }

    Write-Host 'Native acceptance fail-closed smoke passed. Hosted CI cannot turn pending synthetic evidence into native PASS.'
}
finally {
    Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue
}
