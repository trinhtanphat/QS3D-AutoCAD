$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$ciWorkflow = Get-Content -Raw (Join-Path $repo '.github\workflows\ci.yml')
$releaseWorkflow = Get-Content -Raw (Join-Path $repo '.github\workflows\release.yml')
$packageWorkflow = Get-Content -Raw (Join-Path $repo '.github\workflows\package-native.yml')
$packageScript = Get-Content -Raw (Join-Path $repo 'scripts\package.ps1')
$verifyScript = Get-Content -Raw (Join-Path $repo 'scripts\verify-artifacts.ps1')
$setupSource = Get-Content -Raw (Join-Path $repo 'installer\QS3D.Setup\Program.cs')

foreach ($requirement in @(
    'github.event.pull_request.head.sha',
    'Verify exact checkout SHA',
    'git rev-parse HEAD',
    'cancel-in-progress: true'
)) {
    if (-not $ciWorkflow.Contains($requirement, [StringComparison]::Ordinal)) {
        throw "Exact-SHA CI regression: CI workflow is missing '$requirement'."
    }
}

$releaseRequirements = @(
    'QS3D_NATIVE_ACCEPTED_SHA',
    'QS3D_SIGNING_PFX_BASE64',
    'QS3D_SIGNING_PFX_PASSWORD',
    'merge-base --is-ancestor',
    '-RequireSigned',
    'RELEASE-PROVENANCE.json',
    'SHA256SUMS.txt'
)
foreach ($requirement in $releaseRequirements) {
    if (-not $releaseWorkflow.Contains($requirement, [StringComparison]::Ordinal)) {
        throw "Release policy regression: release workflow is missing '$requirement'."
    }
}

foreach ($requirement in @('RELEASE-PROVENANCE.json', 'SHA256SUMS.txt', 'verify-artifacts.ps1')) {
    if (-not $packageWorkflow.Contains($requirement, [StringComparison]::Ordinal)) {
        throw "Package workflow regression: missing '$requirement'."
    }
}

foreach ($requirement in @('git status --porcelain --untracked-files=no', 'RELEASE-PROVENANCE.json', 'SigningPfxPath', 'sign-file.ps1')) {
    if (-not $packageScript.Contains($requirement, [StringComparison]::Ordinal)) {
        throw "Package policy regression: package.ps1 is missing '$requirement'."
    }
}

foreach ($requirement in @('sourceCommit', 'sourceDirty', 'RequireSigned', 'SHA256SUMS')) {
    if (-not $verifyScript.Contains($requirement, [StringComparison]::Ordinal)) {
        throw "Release verification regression: verify-artifacts.ps1 is missing '$requirement'."
    }
}

foreach ($requirement in @('GetProcessesByName("acad")', '.QS3D.bundle.install-', '.QS3D.bundle.backup-', 'ValidateBundle(candidate)')) {
    if (-not $setupSource.Contains($requirement, [StringComparison]::Ordinal)) {
        throw "Installer safety regression: Setup is missing '$requirement'."
    }
}

foreach ($requiredDoc in @('docs\RELEASE-SECURITY.md', 'docs\PRIVACY.md')) {
    if (-not (Test-Path (Join-Path $repo $requiredDoc) -PathType Leaf)) {
        throw "Release documentation regression: missing $requiredDoc"
    }
}

Write-Host 'Release security policy guards passed.'
