param(
    [string]$BundlePath = (Join-Path $PSScriptRoot '..\bundle\QS3D.bundle'),
    [string]$DestinationRoot = (Join-Path $env:ProgramData 'Autodesk\ApplicationPlugins')
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path $BundlePath).Path
$destination = Join-Path $DestinationRoot 'QS3D.bundle'

New-Item -ItemType Directory -Force -Path $DestinationRoot | Out-Null
if (Test-Path $destination) {
    Remove-Item -Recurse -Force $destination
}
Copy-Item -Recurse -Force $source $destination
Write-Host "Installed QS3D bundle to $destination"
Write-Host 'Restart AutoCAD, then run QS3D.'
