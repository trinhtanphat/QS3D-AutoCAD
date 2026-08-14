param(
    [Parameter(Mandatory = $true)][string]$FilePath,
    [Parameter(Mandatory = $true)][string]$PfxPath,
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$SkipTimestamp
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

$signToolPath = Resolve-SignTool
$securePassword = ConvertTo-SecureString -String $Password -AsPlainText -Force
$imported = @(Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation 'Cert:\CurrentUser\My' -Password $securePassword -Exportable:$false)
$certificate = $imported |
    Where-Object { $_.HasPrivateKey } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($null -eq $certificate) {
    foreach ($item in $imported) {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($item.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
    throw 'The PFX did not import a certificate with a private key.'
}

try {
    $signArguments = @('sign', '/fd', 'SHA256', '/sha1', $certificate.Thumbprint, '/s', 'My')
    if (-not $SkipTimestamp) {
        $signArguments += @('/td', 'SHA256', '/tr', $TimestampUrl)
    }
    $signArguments += $FilePath

    & $signToolPath @signArguments
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $FilePath with exit code $LASTEXITCODE."
    }

    & $signToolPath verify /pa /all /v $FilePath
    if ($LASTEXITCODE -ne 0) {
        throw "Authenticode verification failed for $FilePath with exit code $LASTEXITCODE."
    }
}
finally {
    foreach ($item in $imported) {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($item.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Signed and verified $FilePath"
