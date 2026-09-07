$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$commandPath = Join-Path $repo 'src\QS3D.AutoCAD\Commands\Qs3dMepCommands.cs'
$projectPath = Join-Path $repo 'src\QS3D.AutoCAD\QS3D.AutoCAD.csproj'
$manifestPath = Join-Path $repo 'bundle\QS3D.bundle\PackageContents.xml'
$gitmodulesPath = Join-Path $repo '.gitmodules'
$expectedPlatform = 'e029d4ba0de6ffe80575f7aed96affa1db1b9b33'

foreach ($path in @($commandPath, $projectPath, $manifestPath, $gitmodulesPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Cubicost MEP parity source is missing: $path" }
}

$command = Get-Content -Raw -LiteralPath $commandPath
$project = Get-Content -Raw -LiteralPath $projectPath
$gitmodules = Get-Content -Raw -LiteralPath $gitmodulesPath
[xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath

foreach ($required in @(
    'QS3DMEPTAKEOFF',
    'QS3DMEPCLASH',
    'QS3DMEPCLASHLOCATE',
    'QS3DMEPEXACTCLASH',
    'QS3DMEPZOOMSELECTION',
    'MepRecognitionProfileProvider.Current.Recognize',
    'new MepQuantityService().Aggregate',
    'new ClashDetectionService().Detect',
    'left.Solid.CheckInterference',
    'editor.SetImpliedSelection(pair)',
    'editor.SetCurrentView(view)',
    'entity.GeometricExtents',
    'curve.GetDistanceAtParameter',
    'solid.MassProperties.Volume',
    'drawing INSUNITS is Unitless/unsupported'
)) {
    if (-not $command.Contains($required, [StringComparison]::Ordinal)) {
        throw "Cubicost MEP parity regression: missing '$required'."
    }
}

# Point3d.TransformBy is required for the read-only zoom view transform. Guard only
# native database-entity/solid transforms here so the source check does not mistake
# coordinate conversion for CAD mutation.
foreach ($forbidden in @(
    'OpenMode.ForWrite',
    'AppendEntity',
    '.Erase(',
    'entity.TransformBy(',
    'solid.TransformBy(',
    '.BooleanOperation(',
    'solid.Clone(',
    'entity.Clone(',
    'Task.Run',
    'Parallel.For'
)) {
    if ($command.Contains($forbidden, [StringComparison]::Ordinal)) {
        throw "Cubicost MEP native boundary violation: found '$forbidden'."
    }
}

$normalizedProject = $project.Replace('\', '/')
if (-not $normalizedProject.Contains('external/QS3D-Platform/src/QS3D.Platform.Parity/QS3D.Platform.Parity.csproj', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'AutoCAD host must consume QS3D.Platform.Parity through the pinned Platform source tree.'
}
if (-not $gitmodules.Contains('external/QS3D-Platform', [StringComparison]::Ordinal) -or
    -not $gitmodules.Contains('trinhtanphat/QS3D-Platform.git', [StringComparison]::Ordinal)) {
    throw 'QS3D-Platform submodule declaration is missing or unexpected.'
}

Push-Location $repo
try {
    $actualPlatform = (& git -C external/QS3D-Platform rev-parse HEAD).Trim().ToLowerInvariant()
    if ($LASTEXITCODE -ne 0 -or $actualPlatform -ne $expectedPlatform) {
        throw "QS3D-Platform exact pin mismatch: expected $expectedPlatform, actual $actualPlatform"
    }
}
finally {
    Pop-Location
}

$commands = @('QS3DMEPTAKEOFF','QS3DMEPCLASH','QS3DMEPCLASHLOCATE','QS3DMEPEXACTCLASH','QS3DMEPZOOMSELECTION')
$entries = @($manifest.ApplicationPackage.Components.ComponentEntry)
if ($entries.Count -ne 3) { throw "Expected exactly three AutoCAD runtime bundle entries, found $($entries.Count)." }
foreach ($entry in $entries) {
    $globals = @($entry.Commands.Command | ForEach-Object { [string]$_.Global })
    foreach ($name in $commands) {
        if ($globals -notcontains $name) { throw "Bundle entry '$($entry.AppName)' is missing MEP trigger $name." }
    }
}

Write-Host "Cubicost AutoCAD MEP parity guard passed at Platform $expectedPlatform."
