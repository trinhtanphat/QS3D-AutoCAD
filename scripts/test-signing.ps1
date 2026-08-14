param(
    [Parameter(Mandatory = $true)][string]$FilePath
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$signScript = Join-Path $repo 'scripts\sign-file.ps1'

if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
    throw "Signing smoke target not found: $FilePath"
}

$tempRoot = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { [IO.Path]::GetTempPath() } else { $env:RUNNER_TEMP }
$root = Join-Path $tempRoot ("qs3d-signing-smoke-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$target = Join-Path $root (Split-Path -Leaf $FilePath)
$pfx = Join-Path $root 'smoke.pfx'
$passwordText = [Guid]::NewGuid().ToString('N')
$rsa = $null
$certificate = $null

try {
    Write-Host 'Preparing ephemeral signing smoke target...'
    Copy-Item -Force $FilePath $target

    Write-Host 'Generating ephemeral code-signing certificate with .NET APIs...'
    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=QS3D CI Signing Smoke',
        $rsa,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

    $eku = [System.Security.Cryptography.OidCollection]::new()
    [void]$eku.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3', 'Code Signing'))
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku, $false))
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature,
            $true))

    $notBefore = [DateTimeOffset]::UtcNow.AddMinutes(-5)
    $notAfter = [DateTimeOffset]::UtcNow.AddDays(2)
    $certificate = $request.CreateSelfSigned($notBefore, $notAfter)
    $pfxBytes = $certificate.Export(
        [System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx,
        $passwordText)
    [IO.File]::WriteAllBytes($pfx, $pfxBytes)

    Write-Host 'Invoking QS3D signing helper in offline smoke mode...'
    & $signScript `
        -FilePath $target `
        -PfxPath $pfx `
        -Password $passwordText `
        -SkipTimestamp `
        -SkipTrustVerification
    if ($LASTEXITCODE -ne 0) {
        throw 'Signing smoke helper returned a non-zero exit code.'
    }

    Write-Host 'Ephemeral Authenticode signing smoke passed. This verifies signing plumbing/signer identity only, not production certificate trust or timestamp evidence.'
}
finally {
    if ($certificate) { $certificate.Dispose() }
    if ($rsa) { $rsa.Dispose() }
    Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue
}
