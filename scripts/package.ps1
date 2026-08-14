param(
    [string]$Version = '0.1.0',
    [string]$AutoCAD2026SdkDir = $env:AUTOCAD_2026_SDK_DIR,
    [string]$AutoCAD2027SdkDir = $env:AUTOCAD_2027_SDK_DIR
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'src\QS3D.AutoCAD\QS3D.AutoCAD.csproj'
$bundleSource = Join-Path $repo 'bundle\QS3D.bundle'
$stage = Join-Path $repo 'artifacts\QS3D.bundle'
$zip = Join-Path $repo "artifacts\QS3D-AutoCAD-$Version.zip"

if (-not $AutoCAD2026SdkDir) { throw 'AUTOCAD_2026_SDK_DIR is required.' }
if (-not $AutoCAD2027SdkDir) { throw 'AUTOCAD_2027_SDK_DIR is required.' }

Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item -Recurse -Force (Join-Path $bundleSource '*') $stage

$manifest = Join-Path $stage 'PackageContents.xml'
[xml]$xml = Get-Content -Raw $manifest
$xml.ApplicationPackage.AppVersion = $Version
$xml.Save($manifest)

dotnet build $project -c Release -f net8.0-windows -p:AutoCADSdkDir="$AutoCAD2026SdkDir"
dotnet build $project -c Release -f net10.0-windows -p:AutoCADSdkDir="$AutoCAD2027SdkDir"

$payloads = @(
    @{ Framework = 'net8.0-windows'; Folder = '2025-2026' },
    @{ Framework = 'net10.0-windows'; Folder = '2027' }
)
foreach ($payload in $payloads) {
    $output = Join-Path $repo "src\QS3D.AutoCAD\bin\Release\$($payload.Framework)"
    $target = Join-Path $stage "Contents\$($payload.Folder)"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item (Join-Path $output 'QS3D.AutoCAD.dll') $target
    Copy-Item (Join-Path $output 'QS3D.Core.dll') $target
}

Remove-Item -Force $zip -ErrorAction SilentlyContinue
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Created $zip"
