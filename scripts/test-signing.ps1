param(
    [Parameter(Mandatory = $true)][string]$FilePath
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$signScript = Join-Path $repo 'scripts\sign-file.ps1'

if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
    throw "Signing smoke target not found: $FilePath"
}

$root = Join-Path $env:RUNNER_TEMP ("qs3d-signing-smoke-" + [Guid]::NewGuid().ToString('N'))
if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    $root = Join-Path ([IO.Path]::GetTempPath()) ("qs3d-signing-smoke-" + [Guid]::NewGuid().ToString('N'))
}
New-Item -ItemType Directory -Force -Path $root | Out-Null
$target = Join-Path $root (Split-Path -Leaf $FilePath)
$pfx = Join-Path $root 'smoke.pfx'
$cer = Join-Path $root 'smoke.cer'
$passwordText = [Guid]::NewGuid().ToString('N')
$password = ConvertTo-SecureString -String $passwordText -AsPlainText -Force
$createdThumbprint = $null
$trustedThumbprint = $null

try {
    Copy-Item -Force $FilePath $target
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject 'CN=QS3D CI Signing Smoke' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddDays(2)
    $createdThumbprint = $certificate.Thumbprint

    Export-PfxCertificate -Cert $certificate -FilePath $pfx -Password $password | Out-Null
    Export-Certificate -Cert $certificate -FilePath $cer | Out-Null
    Remove-Item -LiteralPath "Cert:\CurrentUser\My\$createdThumbprint" -Force
    $createdThumbprint = $null

    $trusted = Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\CurrentUser\Root'
    $trustedThumbprint = $trusted.Thumbprint

    & $signScript -FilePath $target -PfxPath $pfx -Password $passwordText -SkipTimestamp
    if ($LASTEXITCODE -ne 0) {
        throw 'Signing smoke helper returned a non-zero exit code.'
    }

    Write-Host 'Ephemeral Authenticode signing smoke passed. This is CI plumbing evidence only, not production certificate evidence.'
}
finally {
    if ($createdThumbprint) {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$createdThumbprint" -Force -ErrorAction SilentlyContinue
    }
    if ($trustedThumbprint) {
        Remove-Item -LiteralPath "Cert:\CurrentUser\Root\$trustedThumbprint" -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue
}
