$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$providerPath = Join-Path $repo 'src\QS3D.AutoCAD\MepRecognitionProfileProvider.cs'
$commandPath = Join-Path $repo 'src\QS3D.AutoCAD\Commands\Qs3dMepCommands.cs'
$reviewCommandPath = Join-Path $repo 'src\QS3D.AutoCAD\Commands\Qs3dMepReviewCommands.cs'
$palettePath = Join-Path $repo 'src\QS3D.AutoCAD\UI\MepReviewPalette.cs'
$controlPath = Join-Path $repo 'src\QS3D.AutoCAD\UI\MepReviewControl.cs'
$manifestPath = Join-Path $repo 'bundle\QS3D.bundle\PackageContents.xml'
$docPath = Join-Path $repo 'docs\CUBICOST-MEP-REVIEW-PROFILE-AUTOCAD.md'

$paths = @($providerPath, $commandPath, $reviewCommandPath, $palettePath, $controlPath, $manifestPath, $docPath)
foreach ($path in $paths) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "MEP review/profile source missing: $path" }
}

$provider = Get-Content -Raw -LiteralPath $providerPath
$command = Get-Content -Raw -LiteralPath $commandPath
$reviewCommand = Get-Content -Raw -LiteralPath $reviewCommandPath
$palette = Get-Content -Raw -LiteralPath $palettePath
$control = Get-Content -Raw -LiteralPath $controlPath
$doc = Get-Content -Raw -LiteralPath $docPath
[xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath

$requiredProvider = @(
    'MepRecognitionProfileProvider',
    'Environment.SpecialFolder.ApplicationData',
    'mep-recognition-profile.xml',
    'DtdProcessing = DtdProcessing.Prohibit',
    'XmlResolver = null',
    'MaxProfileBytes',
    'MaxRules',
    'MaxTokensPerRule',
    'File.Replace(tempPath, path, backupPath, true)',
    'MepRecognitionProfiles.CreateDefault()'
)
foreach ($token in $requiredProvider) {
    if (-not $provider.Contains($token, [StringComparison]::Ordinal)) { throw "Profile provider missing '$token'." }
}

if (-not $command.Contains('MepRecognitionProfileProvider.Current.Recognize', [StringComparison]::Ordinal)) {
    throw 'MEP commands must consume the central runtime recognition profile provider.'
}
if ($command.Contains('private static readonly MepRecognitionProfile RecognitionProfile', [StringComparison]::Ordinal)) {
    throw 'MEP commands still contain an independent process-lifetime default recognition profile.'
}

foreach ($token in @(
    '[CommandMethod("QS3DMEPREVIEW")]',
    'MepReviewPalette.Show()'
)) {
    if (-not $reviewCommand.Contains($token, [StringComparison]::Ordinal)) { throw "MEP review command missing '$token'." }
}

foreach ($token in @(
    'new PaletteSet("QS3D MEP Review"',
    'new ElementHost',
    'MepReviewControl'
)) {
    if (-not $palette.Contains($token, [StringComparison]::Ordinal)) { throw "MEP review palette missing '$token'." }
}

foreach ($token in @(
    'QS3DMEPTAKEOFF',
    'QS3DMEPCLASH',
    'QS3DMEPCLASHLOCATE',
    'QS3DMEPEXACTCLASH',
    'QS3DMEPZOOMSELECTION',
    'MepRecognitionProfileProvider.Save(profile)',
    'MepRecognitionProfileProvider.Reload()',
    'DocumentManager.MdiActiveDocument',
    'SendStringToExecute(command + " ", true, false, false)',
    'new MepRecognitionProfile(rules)'
)) {
    if (-not $control.Contains($token, [StringComparison]::Ordinal)) { throw "MEP review control missing '$token'." }
}

foreach ($source in @($provider, $palette, $control, $reviewCommand)) {
    foreach ($forbidden in @(
        'OpenMode.ForWrite',
        'AppendEntity',
        '.Erase(',
        'BooleanOperation(',
        'Task.Run',
        'Parallel.For',
        'ProjectContextCoordinator',
        'QsdbProjectStore'
    )) {
        if ($source.Contains($forbidden, [StringComparison]::Ordinal)) {
            throw "MEP review/profile native/project boundary violation: found '$forbidden'."
        }
    }
}

# Modeless state may retain presentation controls only, never document/database object identity.
foreach ($forbidden in @(
    'private readonly Document',
    'private Document',
    'private readonly ObjectId',
    'private ObjectId',
    'private readonly DBObject',
    'private DBObject',
    'private readonly Solid3d',
    'private Solid3d'
)) {
    if ($palette.Contains($forbidden, [StringComparison]::Ordinal) -or $control.Contains($forbidden, [StringComparison]::Ordinal)) {
        throw "MEP modeless UI retains forbidden native field '$forbidden'."
    }
}

$entries = @($manifest.ApplicationPackage.Components.ComponentEntry)
if ($entries.Count -ne 3) { throw "Expected three AutoCAD runtime entries, found $($entries.Count)." }
foreach ($entry in $entries) {
    $globals = @($entry.Commands.Command | ForEach-Object { [string]$_.Global })
    if ($globals -notcontains 'QS3DMEPREVIEW') { throw "Bundle entry '$($entry.AppName)' is missing QS3DMEPREVIEW DemandLoad trigger." }
}

foreach ($token in @('QS3DMEPREVIEW', 'mep-recognition-profile.xml', 'PENDING_NATIVE', 'QS3D-Platform')) {
    if (-not $doc.Contains($token, [StringComparison]::Ordinal)) { throw "MEP review/profile documentation missing '$token'." }
}

Write-Host 'Cubicost AutoCAD MEP review/profile guard passed.'
