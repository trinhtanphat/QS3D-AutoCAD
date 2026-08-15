[CmdletBinding()]
param(
    [string]$ExpectedCommit = '',
    [string]$ArtifactsDirectory = '',
    [string]$NativeAcceptedSha = $env:QS3D_NATIVE_ACCEPTED_SHA,
    [string]$SigningPfxBase64 = $env:QS3D_SIGNING_PFX_BASE64,
    [string]$SigningPassword = $env:QS3D_SIGNING_PFX_PASSWORD,
    [string]$LicenseApiUrl = $env:QS3D_LICENSE_API_URL,
    [string]$UpdateManifestUrl = $env:QS3D_UPDATE_MANIFEST_URL,
    [string]$UpdatePublicKeyPem = $env:QS3D_UPDATE_PUBLIC_KEY_PEM,
    [string]$TelemetryMode = $env:QS3D_TELEMETRY_MODE,
    [string]$TelemetryEndpoint = $env:QS3D_TELEMETRY_ENDPOINT,
    [switch]$Json,
    [switch]$FailOnBlocked
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$artifacts = if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    Join-Path $repo 'artifacts'
}
else {
    [IO.Path]::GetFullPath($ArtifactsDirectory)
}
$verifyScript = Join-Path $repo 'scripts\verify-artifacts.ps1'
$checks = [Collections.Generic.List[object]]::new()

function Add-ReadinessCheck([string]$Name, [string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject][ordered]@{
        name = $Name
        status = $Status
        detail = $Detail
    })
}

function Test-HttpsUri([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $uri = $null
    if (-not [Uri]::TryCreate($Value.Trim(), [UriKind]::Absolute, [ref]$uri)) { return $false }
    return $uri.Scheme -eq [Uri]::UriSchemeHttps
}

$provenance = $null
$provenancePath = Join-Path $artifacts 'RELEASE-PROVENANCE.json'
if (Test-Path -LiteralPath $provenancePath -PathType Leaf) {
    try {
        $provenance = Get-Content -Raw -LiteralPath $provenancePath | ConvertFrom-Json
    }
    catch {
        Add-ReadinessCheck 'candidate' 'blocked' "RELEASE-PROVENANCE.json is invalid: $($_.Exception.Message)"
    }
}
else {
    Add-ReadinessCheck 'candidate' 'blocked' "No prepared candidate provenance exists at $provenancePath"
}

$commit = $ExpectedCommit
if ([string]::IsNullOrWhiteSpace($commit) -and $null -ne $provenance) {
    $commit = [string]$provenance.sourceCommit
}
if ([string]::IsNullOrWhiteSpace($commit)) {
    Push-Location $repo
    try {
        $commit = (& git rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0) { $commit = '' }
    }
    finally {
        Pop-Location
    }
}
if ($commit -match '^[0-9a-fA-F]{40}$') {
    $commit = $commit.ToLowerInvariant()
}
else {
    Add-ReadinessCheck 'exact-source-sha' 'blocked' "Unable to resolve a full 40-character candidate SHA: $commit"
    $commit = $null
}

if ($null -ne $provenance -and $null -ne $commit) {
    try {
        & $verifyScript -Version ([string]$provenance.version) -ExpectedCommit $commit -ArtifactsDirectory $artifacts
        Add-ReadinessCheck 'candidate' 'ready' "Prepared artifacts verify against exact source SHA $commit."
    }
    catch {
        Add-ReadinessCheck 'candidate' 'blocked' "Prepared artifact verification failed: $($_.Exception.Message)"
    }
}

if ($null -eq $commit) {
    Add-ReadinessCheck 'native-acceptance' 'blocked' 'Exact candidate SHA is unavailable, so native acceptance cannot be bound.'
}
elseif ([string]::IsNullOrWhiteSpace($NativeAcceptedSha)) {
    Add-ReadinessCheck 'native-acceptance' 'blocked' 'QS3D_NATIVE_ACCEPTED_SHA is not configured.'
}
elseif ($NativeAcceptedSha.Trim() -notmatch '^[0-9a-fA-F]{40}$') {
    Add-ReadinessCheck 'native-acceptance' 'blocked' 'QS3D_NATIVE_ACCEPTED_SHA is not a full 40-character SHA.'
}
elseif ($NativeAcceptedSha.Trim().ToLowerInvariant() -ne $commit) {
    Add-ReadinessCheck 'native-acceptance' 'blocked' "Native-accepted SHA does not match the candidate SHA $commit."
}
else {
    Add-ReadinessCheck 'native-acceptance' 'ready' "Native acceptance is bound to the exact candidate SHA $commit."
}

if ([string]::IsNullOrWhiteSpace($SigningPfxBase64) -or [string]::IsNullOrWhiteSpace($SigningPassword)) {
    Add-ReadinessCheck 'authenticode-signing' 'blocked' 'Both QS3D_SIGNING_PFX_BASE64 and QS3D_SIGNING_PFX_PASSWORD are required.'
}
else {
    try {
        $bytes = [Convert]::FromBase64String($SigningPfxBase64.Trim())
        $flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
        $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($bytes, $SigningPassword, $flags)
        try {
            if (-not $certificate.HasPrivateKey) {
                throw 'The configured PFX does not contain a private key.'
            }
            Add-ReadinessCheck 'authenticode-signing' 'ready' "Signing certificate is parseable and contains a private key; subject '$($certificate.Subject)'."
        }
        finally {
            $certificate.Dispose()
        }
    }
    catch {
        Add-ReadinessCheck 'authenticode-signing' 'blocked' "Signing PFX/password validation failed without exposing secret material: $($_.Exception.Message)"
    }
}

if (Test-HttpsUri $LicenseApiUrl) {
    Add-ReadinessCheck 'licensing-service' 'ready' 'Production licensing endpoint is explicitly configured as HTTPS. Authentication/availability still requires deployment acceptance.'
}
elseif ([string]::IsNullOrWhiteSpace($LicenseApiUrl)) {
    Add-ReadinessCheck 'licensing-service' 'blocked' 'QS3D_LICENSE_API_URL is not configured; source-side LicensePolicy alone is not a production activation service.'
}
else {
    Add-ReadinessCheck 'licensing-service' 'blocked' 'QS3D_LICENSE_API_URL must be an absolute HTTPS URL.'
}

if (-not (Test-HttpsUri $UpdateManifestUrl)) {
    if ([string]::IsNullOrWhiteSpace($UpdateManifestUrl)) {
        Add-ReadinessCheck 'update-service' 'blocked' 'QS3D_UPDATE_MANIFEST_URL is not configured.'
    }
    else {
        Add-ReadinessCheck 'update-service' 'blocked' 'QS3D_UPDATE_MANIFEST_URL must be an absolute HTTPS URL.'
    }
}
elseif ([string]::IsNullOrWhiteSpace($UpdatePublicKeyPem)) {
    Add-ReadinessCheck 'update-service' 'blocked' 'QS3D_UPDATE_PUBLIC_KEY_PEM is not configured for signed manifest verification.'
}
elseif ($UpdatePublicKeyPem -notmatch '-----BEGIN PUBLIC KEY-----' -or $UpdatePublicKeyPem -match 'PRIVATE KEY') {
    Add-ReadinessCheck 'update-service' 'blocked' 'QS3D_UPDATE_PUBLIC_KEY_PEM must contain a public key only.'
}
else {
    Add-ReadinessCheck 'update-service' 'ready' 'HTTPS update manifest endpoint and public verification key are explicitly configured. Publisher private signing remains outside the plugin.'
}

$mode = if ([string]::IsNullOrWhiteSpace($TelemetryMode)) { 'disabled' } else { $TelemetryMode.Trim().ToLowerInvariant() }
switch ($mode) {
    'disabled' {
        Add-ReadinessCheck 'telemetry-privacy' 'ready' 'Telemetry is explicitly disabled; this matches the current documented no-telemetry posture.'
    }
    'opt-in' {
        if (Test-HttpsUri $TelemetryEndpoint) {
            Add-ReadinessCheck 'telemetry-privacy' 'ready' 'Telemetry mode is opt-in and the endpoint is HTTPS; consent/retention operations still require production acceptance.'
        }
        else {
            Add-ReadinessCheck 'telemetry-privacy' 'blocked' 'Opt-in telemetry requires QS3D_TELEMETRY_ENDPOINT to be an absolute HTTPS URL.'
        }
    }
    default {
        Add-ReadinessCheck 'telemetry-privacy' 'blocked' "QS3D_TELEMETRY_MODE must be 'disabled' or 'opt-in', not '$mode'."
    }
}

$blocked = @($checks | Where-Object { $_.status -eq 'blocked' })
$result = [ordered]@{
    schemaVersion = 1
    candidateSha = $commit
    productionReleaseReady = ($blocked.Count -eq 0)
    checks = @($checks)
}

if ($Json) {
    $result | ConvertTo-Json -Depth 8
}
else {
    $checks | Format-Table name, status, detail -Wrap -AutoSize
    if ($result.productionReleaseReady) {
        Write-Host 'Commercial release readiness preflight: READY for the configured prerequisites. The release workflow remains the authoritative signing/native gate.'
    }
    else {
        Write-Host "Commercial release readiness preflight: BLOCKED by $($blocked.Count) prerequisite(s). No bypass was applied."
    }
}

if ($FailOnBlocked -and $blocked.Count -gt 0) {
    throw "Commercial release readiness has $($blocked.Count) blocking prerequisite(s)."
}
