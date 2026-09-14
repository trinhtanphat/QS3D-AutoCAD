$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

$setupProjectPath = Join-Path $repo 'installer\QS3D.Setup\QS3D.Setup.csproj'
$setupProgramPath = Join-Path $repo 'installer\QS3D.Setup\Program.cs'
$ciPath = Join-Path $repo '.github\workflows\ci.yml'
$engineeringPath = Join-Path $repo '.github\workflows\engineering-release.yml'
$runtimeSmokePath = Join-Path $repo 'scripts\test-installer-runtime.ps1'

$setupProject = Get-Content -Raw -LiteralPath $setupProjectPath
$setupProgram = Get-Content -Raw -LiteralPath $setupProgramPath
$ci = Get-Content -Raw -LiteralPath $ciPath
$engineering = Get-Content -Raw -LiteralPath $engineeringPath
$runtimeSmoke = Get-Content -Raw -LiteralPath $runtimeSmokePath

# Keep a console-subsystem executable so --quiet/help/CI retain normal native process
# waiting/stdout semantics, but detach the automatically-created console for the
# normal interactive installer path before any user-visible output is written.
if (-not $setupProject.Contains('<OutputType>Exe</OutputType>', [StringComparison]::Ordinal)) {
    throw 'Installer UX regression: Setup must remain OutputType=Exe so command-line and CI execution semantics stay intact.'
}
foreach ($required in @(
    'ConfigureConsole(args);',
    'static void ConfigureConsole(string[] args)',
    'Console.SetOut(TextWriter.Null);',
    'Console.SetError(TextWriter.Null);',
    'NativeMethods.FreeConsole()',
    '"--quiet"',
    '"--help"'
)) {
    if (-not $setupProgram.Contains($required, [StringComparison]::Ordinal)) {
        throw "Installer UX regression: Program.cs is missing '$required'."
    }
}

# Engineering artifacts must carry the same visible CI identity as the GitHub
# prerelease instead of the misleading historical 0.0.0-ci placeholder.
if (-not $ci.Contains('ENGINEERING_VERSION: 0.1.0-ci.${{ github.run_number }}', [StringComparison]::Ordinal)) {
    throw 'Engineering version regression: CI must derive ENGINEERING_VERSION from github.run_number.'
}
if ($ci.Contains('0.0.0-ci', [StringComparison]::Ordinal)) {
    throw 'Engineering version regression: CI still contains the obsolete 0.0.0-ci artifact version.'
}
foreach ($required in @(
    '$engineeringVersion = "0.1.0-ci.$ciRunNumber"',
    '"ENGINEERING_VERSION=$engineeringVersion"',
    'QS3D-AutoCAD-$env:ENGINEERING_VERSION'
)) {
    if (-not $engineering.Contains($required, [StringComparison]::Ordinal)) {
        throw "Engineering release regression: engineering-release.yml is missing '$required'."
    }
}
if ($engineering.Contains('0.0.0-ci', [StringComparison]::Ordinal)) {
    throw 'Engineering release regression: engineering-release.yml still contains the obsolete 0.0.0-ci artifact version.'
}
if ($runtimeSmoke.Contains('0.0.0-ci', [StringComparison]::Ordinal)) {
    throw 'Installer smoke regression: test-installer-runtime.ps1 must not default to an obsolete fixed engineering artifact name.'
}

# AutoCAD application bundles installed for all users must live under CommonApplicationData
# (%ProgramData%\Autodesk\ApplicationPlugins). AutoCAD 2021 native discovery on the licensed
# host does not discover the QS3D bundle from Program Files.
$resolverStart = $setupProgram.IndexOf('static string ResolveDestinationRoot', [StringComparison]::Ordinal)
$resolverEnd = $setupProgram.IndexOf('static string GetLegacyProgramFilesDestination', $resolverStart, [StringComparison]::Ordinal)
if ($resolverStart -lt 0 -or $resolverEnd -le $resolverStart) {
    throw 'Installer discovery regression: unable to isolate ResolveDestinationRoot implementation.'
}
$resolverBody = $setupProgram.Substring($resolverStart, $resolverEnd - $resolverStart)
if (-not $resolverBody.Contains('Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)', [StringComparison]::Ordinal)) {
    throw 'Installer discovery regression: all-users setup must resolve Autodesk\\ApplicationPlugins from CommonApplicationData (ProgramData).'
}
if ($resolverBody.Contains('Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)', [StringComparison]::Ordinal)) {
    throw 'Installer discovery regression: all-users setup must not install QS3D.bundle under Program Files.'
}
if (-not $setupProgram.Contains('static string GetLegacyProgramFilesDestination()', [StringComparison]::Ordinal)) {
    throw 'Installer migration regression: setup must retain a Program Files legacy destination cleanup helper.'
}
if ($setupProgram.Contains('GetLegacyProgramDataDestination', [StringComparison]::Ordinal)) {
    throw 'Installer migration regression: ProgramData is canonical again and must not be treated as the legacy destination.'
}
Write-Host 'Installer interactive-console UX, all-users bundle discovery, and engineering prerelease version identity guards passed.'
