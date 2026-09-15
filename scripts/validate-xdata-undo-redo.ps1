$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$metadataPath = Join-Path $repo 'src\QS3D.AutoCAD\Metadata\Qs3dEntityMetadata.cs'
$commandsPath = Join-Path $repo 'src\QS3D.AutoCAD\Commands\Qs3dCommands.cs'

foreach ($path in @($metadataPath, $commandsPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Undo/redo guard source missing: $path"
    }
}

$metadata = Get-Content -Raw -LiteralPath $metadataPath
$commands = Get-Content -Raw -LiteralPath $commandsPath

if (-not $metadata.Contains('EnsureRegApp(transaction, database);', [StringComparison]::Ordinal)) {
    throw 'QS3D metadata Attach must ensure its RegApp before assigning XData.'
}

$methodStart = $metadata.IndexOf('private static void EnsureRegApp(', [StringComparison]::Ordinal)
if ($methodStart -lt 0) { throw 'QS3D RegApp helper is missing.' }

$undoState = $metadata.IndexOf('database.UndoRecording', $methodStart, [StringComparison]::Ordinal)
$disable = $metadata.IndexOf('database.DisableUndoRecording(true)', $methodStart, [StringComparison]::Ordinal)
$record = $metadata.IndexOf('new RegAppTableRecord', $methodStart, [StringComparison]::Ordinal)
$restore = $metadata.IndexOf('database.DisableUndoRecording(false)', $methodStart, [StringComparison]::Ordinal)
if ($undoState -lt 0 -or $disable -lt 0 -or $record -lt 0 -or $restore -lt 0) {
    throw 'First-use QS3D RegApp registration must preserve Database.UndoRecording and create the APPID with undo recording disabled.'
}
if (-not ($undoState -lt $disable -and $disable -lt $record -and $record -lt $restore)) {
    throw 'QS3D RegApp undo-safety operations are not in the required preserve/disable/register/restore order.'
}
$tryIndex = $metadata.IndexOf('try', $methodStart, [StringComparison]::Ordinal)
$finallyIndex = $metadata.IndexOf('finally', $methodStart, [StringComparison]::Ordinal)
if ($tryIndex -lt 0 -or $finallyIndex -lt 0 -or $tryIndex -gt $record -or $finallyIndex -lt $record) {
    throw 'QS3D RegApp undo recording must be restored through try/finally.'
}
if ($commands.Contains('DisableUndoRecording(', [StringComparison]::Ordinal)) {
    throw 'Geometry command code must not disable database undo recording; only RegApp bootstrap may do so.'
}

Write-Host 'QS3D XData first-create undo/redo RegApp guard passed.'
