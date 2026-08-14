param(
    [string]$DestinationRoot = (Join-Path $env:ProgramData 'Autodesk\ApplicationPlugins')
)

$ErrorActionPreference = 'Stop'
$destination = Join-Path $DestinationRoot 'QS3D.bundle'
if (Test-Path $destination) {
    Remove-Item -Recurse -Force $destination
    Write-Host "Removed $destination"
} else {
    Write-Host 'QS3D bundle is not installed in the all-users ApplicationPlugins folder.'
}
