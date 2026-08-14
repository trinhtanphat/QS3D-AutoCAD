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

$kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$signTool = Get-ChildItem -Path $kitsRoot -Filter signtool.exe -File -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if ($null -eq $signTool) {
    throw "signtool.exe (x64) was not found under $kitsRoot. Install the Windows SDK signing tools."
}

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

    & $signTool.FullName @signArguments
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $FilePath with exit code $LASTEXITCODE."
    }

    & $signTool.FullName verify /pa /all /v $FilePath
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
