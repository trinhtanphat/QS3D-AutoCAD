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
$protocolRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("QS3D-Setup-Protocol-Smoke-" + [Guid]::NewGuid().ToString('N'))
$protocolBundle = Join-Path $protocolRoot 'QS3D.bundle'
$protocolLog = Join-Path ([System.IO.Path]::GetTempPath()) ("QS3D-Setup-Protocol-" + [Guid]::NewGuid().ToString('N') + '.log')
$installResult = Join-Path ([System.IO.Path]::GetTempPath()) ("QS3D-Setup-Protocol-Install-" + [Guid]::NewGuid().ToString('N') + '.txt')
$uninstallResult = Join-Path ([System.IO.Path]::GetTempPath()) ("QS3D-Setup-Protocol-Uninstall-" + [Guid]::NewGuid().ToString('N') + '.txt')

function Assert-ResultFile([string]$Path, [string]$Operation) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Setup $Operation protocol smoke did not create an explicit completion result."
    }

    $value = (Get-Content -Raw -LiteralPath $Path).Trim()
    if ($value -ne '0') {
        throw "Setup $Operation protocol smoke reported completion result '$value' instead of 0."
    }
}

try {
    & $resolvedSetup --install-root $testRoot --skip-autocad-check --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Setup install runtime smoke failed with exit code $LASTEXITCODE."
    }

    foreach ($relative in @(
        'PackageContents.xml',
        'Contents\2021\QS3D.AutoCAD.dll',
        'Contents\2021\QS3D.Core.dll',
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

    [xml]$installedManifest = Get-Content -Raw -LiteralPath (Join-Path $bundle 'PackageContents.xml')
    $runtimeRequirements = @($installedManifest.ApplicationPackage.Components.RuntimeRequirements)
    if (@($runtimeRequirements | Where-Object { $_.SeriesMin -eq 'R24.0' -and $_.SeriesMax -eq 'R24.0' }).Count -ne 1) {
        throw 'Setup runtime smoke installed a bundle without the AutoCAD 2021 R24.0 runtime declaration.'
    }

    & $resolvedSetup --uninstall --install-root $testRoot --skip-autocad-check --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Setup uninstall runtime smoke failed with exit code $LASTEXITCODE."
    }

    if (Test-Path -LiteralPath $bundle) {
        throw "Setup uninstall runtime smoke left the bundle behind: $bundle"
    }

    & $resolvedSetup --install-root $protocolRoot --skip-autocad-check --quiet --elevated-child --log-path $protocolLog --result-path $installResult
    if ($LASTEXITCODE -ne 0) {
        throw "Setup elevated-child install protocol smoke failed with exit code $LASTEXITCODE."
    }
    Assert-ResultFile -Path $installResult -Operation 'install'
    if (-not (Test-Path -LiteralPath $protocolBundle -PathType Container)) {
        throw 'Setup elevated-child install protocol smoke did not install QS3D.bundle.'
    }

    & $resolvedSetup --uninstall --install-root $protocolRoot --skip-autocad-check --quiet --elevated-child --log-path $protocolLog --result-path $uninstallResult
    if ($LASTEXITCODE -ne 0) {
        throw "Setup elevated-child uninstall protocol smoke failed with exit code $LASTEXITCODE."
    }
    Assert-ResultFile -Path $uninstallResult -Operation 'uninstall'
    if (Test-Path -LiteralPath $protocolBundle) {
        throw 'Setup elevated-child uninstall protocol smoke left QS3D.bundle behind.'
    }

    if (-not (Test-Path -LiteralPath $protocolLog -PathType Leaf)) {
        throw 'Setup elevated-child protocol smoke did not create the shared log.'
    }

    $protocolLogText = Get-Content -Raw -LiteralPath $protocolLog
    $successCount = ([regex]::Matches($protocolLogText, 'RESULT: SUCCESS')).Count
    if ($successCount -lt 2) {
        throw "Setup elevated-child protocol smoke expected two explicit SUCCESS records but found $successCount."
    }
    if (-not $protocolLogText.Contains('Elevated child wrote explicit completion result 0.', [StringComparison]::Ordinal)) {
        throw 'Setup elevated-child protocol smoke log is missing the explicit completion-result record.'
    }

    Write-Host 'QS3D Setup install/uninstall runtime and elevated-child result protocol smoke passed, including AutoCAD 2021 payload verification.'
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $protocolRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $protocolLog -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $installResult -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $uninstallResult -Force -ErrorAction SilentlyContinue
}
