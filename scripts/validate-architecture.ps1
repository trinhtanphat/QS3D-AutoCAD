$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$core = Join-Path $repo 'src\QS3D.Core'
$hostProject = Join-Path $repo 'src\QS3D.AutoCAD\QS3D.AutoCAD.csproj'
$commandRoot = Join-Path $repo 'src\QS3D.AutoCAD\Commands'
$ribbonSourcePath = Join-Path $repo 'src\QS3D.AutoCAD\UI\Qs3dRibbon.cs'
$jigSourcePath = Join-Path $repo 'src\QS3D.AutoCAD\UI\Qs3dPointPreviewJig.cs'
$jigCommandsPath = Join-Path $repo 'src\QS3D.AutoCAD\Commands\Qs3dJigCommands.cs'
$gridSnapPath = Join-Path $repo 'src\QS3D.AutoCAD\Commands\Qs3dGridSnapCommands.cs'
$manifest = Join-Path $repo 'bundle\QS3D.bundle\PackageContents.xml'

$autodeskLeak = Get-ChildItem -Recurse -File $core -Filter '*.cs' | Select-String -SimpleMatch 'Autodesk.AutoCAD'
if ($autodeskLeak) { throw 'Architecture violation: QS3D.Core references Autodesk.AutoCAD.' }

$requiredCommands = @(
    'QS3D','QS3DABOUT','QS3DRIBBON','QS3DINIT','QS3DLEVEL','QS3DGRID','QS3DCOLUMN','QS3DCOLUMNJIG',
    'QS3DBEAM','QS3DBEAMJIG','QS3DSLAB','QS3DSLABJIG','QS3DWALL','QS3DWALLJIG','QS3DCURTAIN','QS3DCURTAINJIG',
    'QS3DSECTION','QS3DBOQ','QS3DEDIT','QS3DREFRESH','QS3DASSIGNLEVEL','QS3DLEVELMOVE','QS3DBINDGRID','QS3DGRIDSNAP',
    'QS3DCLEARREFS','QS3DREFERENCEDELETE','QS3DGRIDARRAY','QS3DREFERENCES'
)
$source = (Get-ChildItem -File $commandRoot -Filter '*.cs' | ForEach-Object { Get-Content -Raw $_.FullName }) -join "`n"
foreach ($command in $requiredCommands) {
    $needle = 'CommandMethod("' + $command + '"'
    if (-not $source.Contains($needle, [StringComparison]::Ordinal)) {
        throw "Missing command registration: $command"
    }
}

if (-not (Test-Path -LiteralPath $ribbonSourcePath -PathType Leaf)) {
    throw 'QS3D Ribbon runtime bridge source is missing.'
}
$hostProjectSource = Get-Content -Raw -LiteralPath $hostProject
$ribbonSource = Get-Content -Raw -LiteralPath $ribbonSourcePath
if ($hostProjectSource -match '<Reference\s+Include=["'']AdWindows') {
    throw 'Ribbon architecture violation: the hosted build must not add a compile-time AdWindows reference.'
}
if ($ribbonSource -match '(?m)^\s*using\s+Autodesk\.Windows') {
    throw 'Ribbon architecture violation: Qs3dRibbon must remain runtime-reflection based and must not compile against Autodesk.Windows.'
}
foreach ($runtimeType in @(
    'Autodesk.Windows.ComponentManager',
    'Autodesk.Windows.RibbonTab',
    'Autodesk.Windows.RibbonPanel',
    'Autodesk.Windows.RibbonPanelSource',
    'Autodesk.Windows.RibbonRow',
    'Autodesk.Windows.RibbonButton'
)) {
    if (-not $ribbonSource.Contains($runtimeType, [StringComparison]::Ordinal)) {
        throw "Ribbon runtime bridge regression: missing runtime type '$runtimeType'."
    }
}
if (-not $ribbonSource.Contains('Assembly.Load("AdWindows")', [StringComparison]::Ordinal)) {
    throw 'Ribbon runtime bridge regression: AdWindows runtime fallback is missing.'
}
if ($ribbonSource.Contains('PaletteSet', [StringComparison]::Ordinal)) {
    throw 'Ribbon bridge must not own or construct the QS3D PaletteSet.'
}
foreach ($command in @('QS3DCOLUMNJIG','QS3DBEAMJIG','QS3DSLABJIG','QS3DWALLJIG','QS3DCURTAINJIG','QS3DGRIDSNAP')) {
    if (-not $ribbonSource.Contains($command, [StringComparison]::Ordinal)) {
        throw "Ribbon advanced modelling regression: missing $command."
    }
}

foreach ($path in @($jigSourcePath, $jigCommandsPath, $gridSnapPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Advanced modelling source file is missing: $path"
    }
}

$jigSource = Get-Content -Raw -LiteralPath $jigSourcePath
$jigCommands = Get-Content -Raw -LiteralPath $jigCommandsPath
$gridSnapSource = Get-Content -Raw -LiteralPath $gridSnapPath
foreach ($requiredSnippet in @('DrawJig', 'editor.Drag(this)', 'JigPromptPointOptions', 'AcquirePoint', 'WorldDraw', 'entity.WorldDraw(draw)')) {
    if (-not $jigSource.Contains($requiredSnippet, [StringComparison]::Ordinal)) {
        throw "Jig preview regression: missing '$requiredSnippet'."
    }
}
foreach ($forbiddenSnippet in @('StartTransaction', 'AutoCadDrawing.Append', '.Attach(', 'XData', 'Geometry.Draw(')) {
    if ($jigSource.Contains($forbiddenSnippet, [StringComparison]::Ordinal)) {
        throw "Jig persistence/lifetime violation: transient preview helper contains '$forbiddenSnippet'."
    }
}
foreach ($command in @('QS3DCOLUMNJIG','QS3DBEAMJIG','QS3DSLABJIG','QS3DWALLJIG','QS3DCURTAINJIG')) {
    $needle = 'CommandMethod("' + $command + '"'
    if (-not $jigCommands.Contains($needle, [StringComparison]::Ordinal)) {
        throw "Jig command regression: missing $command."
    }
}
$dragCount = ([regex]::Matches($jigCommands, [regex]::Escape('var drag = jig.Drag(editor);'))).Count
$okGateCount = ([regex]::Matches($jigCommands, [regex]::Escape('if (drag.Status != PromptStatus.OK) return;'))).Count
if ($dragCount -lt 4 -or $okGateCount -ne $dragCount) {
    throw "Jig commit-order regression: expected one PromptStatus.OK gate for each Drag call; drag=$dragCount ok-gates=$okGateCount."
}
$firstDrag = $jigCommands.IndexOf('var drag = jig.Drag(editor);', [StringComparison]::Ordinal)
$firstGate = $jigCommands.IndexOf('if (drag.Status != PromptStatus.OK) return;', [StringComparison]::Ordinal)
$firstAppend = $jigCommands.IndexOf('AutoCadDrawing.Append(', [StringComparison]::Ordinal)
if ($firstDrag -lt 0 -or $firstGate -lt $firstDrag -or $firstAppend -lt $firstGate) {
    throw 'Jig commit-order regression: persistence must start only after Editor.Drag returns PromptStatus.OK.'
}

foreach ($requiredSnippet in @(
    'CommandMethod("QS3DGRIDSNAP"',
    'PlacementService.SnapToGrids',
    'Qs3dGeometryFactory.CreateSolid',
    'updated.Attach',
    'original.Erase()',
    'transaction.Commit()'
)) {
    if (-not $gridSnapSource.Contains($requiredSnippet, [StringComparison]::Ordinal)) {
        throw "Grid snap geometry/metadata regression: missing '$requiredSnippet'."
    }
}
$gridSnapRebuild = $gridSnapSource.IndexOf('Qs3dGeometryFactory.CreateSolid', [StringComparison]::Ordinal)
$gridSnapAttach = $gridSnapSource.IndexOf('updated.Attach', [StringComparison]::Ordinal)
$gridSnapErase = $gridSnapSource.IndexOf('original.Erase()', [StringComparison]::Ordinal)
$gridSnapCommit = $gridSnapSource.IndexOf('transaction.Commit()', [StringComparison]::Ordinal)
if ($gridSnapRebuild -lt 0 -or $gridSnapAttach -lt $gridSnapRebuild -or $gridSnapErase -lt $gridSnapAttach -or $gridSnapCommit -lt $gridSnapErase) {
    throw 'Grid snap transaction regression: rebuild, metadata attach, old-entity erase and commit ordering changed.'
}

[xml]$xml = Get-Content -Raw $manifest
$components = @($xml.ApplicationPackage.Components)
$entries = @($components.ComponentEntry)
if ($entries.Count -ne 2) { throw 'PackageContents.xml must contain two runtime-specific component entries.' }
if ($entries.ModuleName -notcontains './Contents/2025-2026/QS3D.AutoCAD.dll') { throw 'Missing AutoCAD 2025-2026 bundle payload.' }
if ($entries.ModuleName -notcontains './Contents/2027/QS3D.AutoCAD.dll') { throw 'Missing AutoCAD 2027 bundle payload.' }
if (@($entries | Where-Object { $_.LoadReasons -ne 'LoadOnCommandInvocation' }).Count -ne 0) { throw 'All .NET hosts must use command-invocation autoload.' }

foreach ($entry in $entries) {
    $manifestCommands = @($entry.Commands.Command | ForEach-Object { [string]$_.Global })
    foreach ($command in $requiredCommands) {
        if ($manifestCommands -notcontains $command) {
            throw "Bundle entry $($entry.AppName) is missing command trigger $command."
        }
    }
}

$net8 = $components | Where-Object { $_.RuntimeRequirements.SeriesMin -eq 'R25.0' }
$net10 = $components | Where-Object { $_.RuntimeRequirements.SeriesMin -eq 'R26.0' }
if ($null -eq $net8 -or $net8.RuntimeRequirements.SeriesMax -ne 'R25.1') { throw 'AutoCAD 2025-2026 runtime range must be R25.0-R25.1.' }
if ($null -eq $net10 -or $net10.RuntimeRequirements.SeriesMax -ne 'R26.0') { throw 'AutoCAD 2027 runtime range must be R26.0.' }

Write-Host 'Architecture and package guards passed.'
