param(
    [Parameter(Mandatory = $true)][string]$FilePath,
    [Parameter(Mandatory = $true)][string]$PfxPath,
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$SkipTimestamp,
    [switch]$SkipTrustVerification
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
    throw "Signing target not found: $FilePath"
}
if (-not (Test-Path -LiteralPath $PfxPath -PathType Leaf)) {
    throw "Signing certificate not found: $PfxPath"
}
if ([string]::IsNullOrWhiteSpace($Password)) {
    throw 'Signing certificate password is required.'
}
if (-not $SkipTimestamp -and [string]::IsNullOrWhiteSpace($TimestampUrl)) {
    throw 'RFC3161 timestamp URL is required for production signing.'
}

function Resolve-SignTool {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command -and (Test-Path -LiteralPath $command.Source -PathType Leaf)) {
        return $command.Source
    }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (-not (Test-Path -LiteralPath $kitsRoot -PathType Container)) {
        throw "Windows SDK signing tools root was not found: $kitsRoot"
    }

    $direct = Join-Path $kitsRoot 'x64\signtool.exe'
    if (Test-Path -LiteralPath $direct -PathType Leaf) {
        return $direct
    }

    $sdkDirectories = Get-ChildItem -LiteralPath $kitsRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending
    foreach ($directory in $sdkDirectories) {
        $candidate = Join-Path $directory.FullName 'x64\signtool.exe'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw "signtool.exe (x64) was not found under $kitsRoot. Install the Windows SDK signing tools."
}

Write-Host 'Resolving Windows signing tool...'
$signToolPath = Resolve-SignTool
Write-Host "Using signtool: $signToolPath"

$storageFlags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::UserKeySet -bor
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet
$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($PfxPath, $Password, $storageFlags)
if (-not $certificate.HasPrivateKey) {
    $certificate.Dispose()
    throw 'The PFX does not contain a private key.'
}

$store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
    [System.Security.Cryptography.X509Certificates.StoreName]::My,
    [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
$store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
$store.Add($certificate)
Write-Host "Imported signing certificate $($certificate.Thumbprint) into CurrentUser/My."

try {
    $signArguments = @('sign', '/fd', 'SHA256', '/sha1', $certificate.Thumbprint, '/s', 'My')
    if (-not $SkipTimestamp) {
        $signArguments += @('/td', 'SHA256', '/tr', $TimestampUrl)
    }
    $signArguments += $FilePath

    Write-Host "Signing $(Split-Path -Leaf $FilePath)..."
    & $signToolPath @signArguments
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $FilePath with exit code $LASTEXITCODE."
    }

    if ($SkipTrustVerification) {
        Write-Host 'Verifying signer identity without trust-chain enforcement (smoke mode only)...'
        $signature = Get-AuthenticodeSignature -LiteralPath $FilePath
        if ($null -eq $signature.SignerCertificate) {
            throw "Authenticode smoke verification did not find a signer certificate for $FilePath."
        }
        if ($signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
            throw "Authenticode smoke signer mismatch for $FilePath."
        }
        if ($signature.Status -eq [System.Management.Automation.SignatureStatus]::NotSigned) {
            throw "Authenticode smoke verification reports the file is not signed: $FilePath."
        }
    }
    else {
        Write-Host 'Verifying full Authenticode trust chain...'
        & $signToolPath verify /pa /all /v $FilePath
        if ($LASTEXITCODE -ne 0) {
            throw "Authenticode verification failed for $FilePath with exit code $LASTEXITCODE."
        }
    }
}
finally {
    try {
        $store.Remove($certificate)
    }
    finally {
        $store.Close()
        $store.Dispose()
        $certificate.Dispose()
    }
}

Write-Host "Signed and verified $FilePath"
