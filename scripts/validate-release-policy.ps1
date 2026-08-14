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

foreach ($requirement in @(
    'Upload verified engineering native candidate',
    'actions/upload-artifact@v7',
    "if: github.event_name == 'push'",
    'QS3D-AutoCAD-native-candidate-${{ github.sha }}',
    'artifacts/QS3D-AutoCAD-0.0.0-ci.zip',
    'artifacts/QS3D-AutoCAD-0.0.0-ci-Setup.exe',
    'artifacts/RELEASE-PROVENANCE.json',
    'artifacts/SHA256SUMS.txt',
    'if-no-files-found: error',
    'Candidate source SHA: ${{ github.sha }}',
    'not a signed commercial release and not native PASS'
)) {
    if (-not $ciWorkflow.Contains($requirement, [StringComparison]::Ordinal)) {
        throw "Native candidate handoff regression: CI workflow is missing '$requirement'."
    }
}
$packageStepIndex = $ciWorkflow.IndexOf('Package and verify release contract', [StringComparison]::Ordinal)
$uploadStepIndex = $ciWorkflow.IndexOf('Upload verified engineering native candidate', [StringComparison]::Ordinal)
$nativeRejectionIndex = $ciWorkflow.IndexOf('Native acceptance rejection smoke', [StringComparison]::Ordinal)
if ($packageStepIndex -lt 0 -or $uploadStepIndex -le $packageStepIndex -or $nativeRejectionIndex -le $uploadStepIndex) {
    throw 'Native candidate handoff must upload only after package verification and before later synthetic native-evidence work can add files.'
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

foreach ($requiredDoc in @(
    'docs\RELEASE-SECURITY.md',
    'docs\PRIVACY.md',
    'docs\NATIVE-ACCEPTANCE.md',
    'docs\NATIVE-CANDIDATE-HANDOFF.md'
)) {
    if (-not (Test-Path (Join-Path $repo $requiredDoc) -PathType Leaf)) {
        throw "Release documentation regression: missing $requiredDoc"
    }
}

Write-Host 'Release security policy guards passed.'
