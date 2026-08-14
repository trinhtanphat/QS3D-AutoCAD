$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$core = Join-Path $repo 'src\QS3D.Core'
$hostCommands = Join-Path $repo 'src\QS3D.AutoCAD\Commands\Qs3dCommands.cs'
$manifest = Join-Path $repo 'bundle\QS3D.bundle\PackageContents.xml'

$autodeskLeak = Get-ChildItem -Recurse -File $core -Filter '*.cs' | Select-String -SimpleMatch 'Autodesk.AutoCAD'
if ($autodeskLeak) { throw 'Architecture violation: QS3D.Core references Autodesk.AutoCAD.' }

$requiredCommands = @('QS3D','QS3DABOUT','QS3DINIT','QS3DLEVEL','QS3DGRID','QS3DCOLUMN','QS3DBEAM','QS3DSLAB','QS3DWALL','QS3DCURTAIN','QS3DSECTION','QS3DBOQ')
$source = Get-Content -Raw $hostCommands
foreach ($command in $requiredCommands) {
    if ($source -notmatch [regex]::Escape("CommandMethod(`"$command`"")) {
        throw "Missing command registration: $command"
    }
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
