param(
    [string]$SetupPath = 'artifacts/QS3D-AutoCAD-0.0.0-ci-Setup.exe'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$resolvedSetup = Join-Path $repo $SetupPath
if (-not (Test-Path -LiteralPath $resolvedSetup -PathType Leaf)) {
    throw "Setup runtime smoke input is missing: $resolvedSetup"
}

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("QS3D-Setup-Smoke-" + [Guid]::NewGuid().ToString('N'))
$bundle = Join-Path $testRoot 'QS3D.bundle'

try {
    & $resolvedSetup --install-root $testRoot --skip-autocad-check --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Setup install runtime smoke failed with exit code $LASTEXITCODE."
    }

    foreach ($relative in @(
        'PackageContents.xml',
        'Contents\2025-2026\QS3D.AutoCAD.dll',
        'Contents\2025-2026\QS3D.Core.dll',
        'Contents\2027\QS3D.AutoCAD.dll',
        'Contents\2027\QS3D.Core.dll'
    )) {
        $path = Join-Path $bundle $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Setup runtime smoke installed an incomplete bundle: missing $relative"
        }
    }

    & $resolvedSetup --uninstall --install-root $testRoot --skip-autocad-check --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Setup uninstall runtime smoke failed with exit code $LASTEXITCODE."
    }

    if (Test-Path -LiteralPath $bundle) {
        throw "Setup uninstall runtime smoke left the bundle behind: $bundle"
    }

    Write-Host 'QS3D Setup install/uninstall runtime smoke passed.'
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}
