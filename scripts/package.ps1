param(
    [string]$Version = '0.1.0',
    [string]$SigningPfxPath = '',
    [string]$SigningPassword = '',
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'src\QS3D.AutoCAD\QS3D.AutoCAD.csproj'
$setupProject = Join-Path $repo 'installer\QS3D.Setup\QS3D.Setup.csproj'
$bundleSource = Join-Path $repo 'bundle\QS3D.bundle'
$signScript = Join-Path $repo 'scripts\sign-file.ps1'
$artifacts = Join-Path $repo 'artifacts'
$stage = Join-Path $artifacts 'QS3D.bundle'
$zip = Join-Path $artifacts "QS3D-AutoCAD-$Version.zip"
$setupOutput = Join-Path $artifacts 'setup-publish'
$setupExe = Join-Path $artifacts "QS3D-AutoCAD-$Version-Setup.exe"
$provenance = Join-Path $artifacts 'RELEASE-PROVENANCE.json'
$checksums = Join-Path $artifacts 'SHA256SUMS.txt'

if ($Version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
    throw "Version must be SemVer-like, for example 0.1.0 or 0.1.0-preview.1: $Version"
}

Push-Location $repo
try {
    $commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'Unable to resolve the exact git commit for release provenance.'
    }

    $dirtyEntries = @(& git status --porcelain --untracked-files=no)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect git working tree state.'
    }
    if ($dirtyEntries.Count -gt 0 -and -not $AllowDirty) {
        throw 'Refusing to package a dirty tracked working tree. Commit/stash changes or pass -AllowDirty explicitly for non-release development use.'
    }
}
finally {
    Pop-Location
}

$signingEnabled = -not [string]::IsNullOrWhiteSpace($SigningPfxPath) -or -not [string]::IsNullOrWhiteSpace($SigningPassword)
if ($signingEnabled -and ([string]::IsNullOrWhiteSpace($SigningPfxPath) -or [string]::IsNullOrWhiteSpace($SigningPassword))) {
    throw 'Signing requires both -SigningPfxPath and -SigningPassword.'
}

Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $setupOutput -ErrorAction SilentlyContinue
Remove-Item -Force $zip, $setupExe, $provenance, $checksums -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item -Recurse -Force (Join-Path $bundleSource '*') $stage

$manifest = Join-Path $stage 'PackageContents.xml'
[xml]$xml = Get-Content -Raw $manifest
$xml.ApplicationPackage.AppVersion = $Version
$xml.Save($manifest)

dotnet build $project -c Release -f net48 "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'AutoCAD 2021 host build failed.' }
dotnet build $project -c Release -f net8.0-windows "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'AutoCAD 2025-2026 host build failed.' }
dotnet build $project -c Release -f net10.0-windows "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'AutoCAD 2027 host build failed.' }

$payloads = @(
    @{ Framework = 'net48'; Folder = '2021'; AutoCAD = '2021'; Runtime = '.NET Framework 4.8' },
    @{ Framework = 'net8.0-windows'; Folder = '2025-2026'; AutoCAD = '2025-2026'; Runtime = '.NET 8' },
    @{ Framework = 'net10.0-windows'; Folder = '2027'; AutoCAD = '2027'; Runtime = '.NET 10' }
)
$stagedAssemblies = @()
foreach ($payload in $payloads) {
    $output = Join-Path $repo "src\QS3D.AutoCAD\bin\Release\$($payload.Framework)"
    $target = Join-Path $stage "Contents\$($payload.Folder)"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    foreach ($assemblyName in @('QS3D.AutoCAD.dll', 'QS3D.Core.dll')) {
        $sourceAssembly = Join-Path $output $assemblyName
        $targetAssembly = Join-Path $target $assemblyName
        Copy-Item -Force $sourceAssembly $targetAssembly
        $stagedAssemblies += $targetAssembly
    }
}

if ($signingEnabled) {
    foreach ($assemblyPath in $stagedAssemblies) {
        & $signScript -FilePath $assemblyPath -PfxPath $SigningPfxPath -Password $SigningPassword -TimestampUrl $TimestampUrl
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $assemblyPath." }
    }
}

Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal

dotnet publish $setupProject -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    "-p:BundleZipPath=$zip" `
    "-p:Version=$Version" `
    -o $setupOutput
if ($LASTEXITCODE -ne 0) { throw 'Setup executable publish failed.' }

$publishedSetup = Join-Path $setupOutput 'QS3D-AutoCAD-Setup.exe'
if (-not (Test-Path $publishedSetup)) { throw "Setup executable was not produced at $publishedSetup" }
Copy-Item -Force $publishedSetup $setupExe

if ($signingEnabled) {
    & $signScript -FilePath $setupExe -PfxPath $SigningPfxPath -Password $SigningPassword -TimestampUrl $TimestampUrl
    if ($LASTEXITCODE -ne 0) { throw 'Setup executable signing failed.' }
}

function Get-ArtifactRecord([string]$Path) {
    $item = Get-Item -LiteralPath $Path
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $Path
    return [ordered]@{
        file = $item.Name
        sha256 = $hash.Hash.ToLowerInvariant()
        bytes = $item.Length
    }
}

$artifactRecords = @(
    (Get-ArtifactRecord $zip),
    (Get-ArtifactRecord $setupExe)
)

$provenanceObject = [ordered]@{
    schemaVersion = 1
    product = 'QS3D AutoCAD'
    version = $Version
    sourceCommit = $commit.ToLowerInvariant()
    sourceDirty = ($dirtyEntries.Count -gt 0)
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    signed = [bool]$signingEnabled
    runtimeMatrix = @(
        [ordered]@{ autoCAD = '2021'; targetFramework = 'net48'; managedRuntime = '.NET Framework 4.8'; apiPackage = 'AutoCAD.NET 24.0.0' },
        [ordered]@{ autoCAD = '2025-2026'; targetFramework = 'net8.0-windows'; managedRuntime = '.NET 8'; apiPackage = 'AutoCAD.NET 25.0.1' },
        [ordered]@{ autoCAD = '2027'; targetFramework = 'net10.0-windows'; managedRuntime = '.NET 10'; apiPackage = 'AutoCAD.NET 26.0.0' }
    )
    artifacts = $artifactRecords
}
$provenanceObject | ConvertTo-Json -Depth 8 | Set-Content -Path $provenance -Encoding utf8NoBOM

$checksumLines = foreach ($path in @($zip, $setupExe, $provenance)) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $path
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $path)
}
Set-Content -Path $checksums -Value $checksumLines -Encoding ascii

Write-Host "Created $zip"
Write-Host "Created $setupExe"
Write-Host "Created $provenance"
Write-Host "Created $checksums"
Write-Host "Source commit: $commit"
Write-Host "Authenticode signing enabled: $signingEnabled"
