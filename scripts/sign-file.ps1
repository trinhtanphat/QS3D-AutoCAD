param(
    [Parameter(Mandatory = $true)][string]$FilePath,
    [Parameter(Mandatory = $true)][string]$PfxPath,
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
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
if ([string]::IsNullOrWhiteSpace($TimestampUrl)) {
    throw 'RFC3161 timestamp URL is required.'
}

$kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$signTool = Get-ChildItem -Path $kitsRoot -Filter signtool.exe -File -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if ($null -eq $signTool) {
    throw "signtool.exe (x64) was not found under $kitsRoot. Install the Windows SDK signing tools."
}

& $signTool.FullName sign /fd SHA256 /td SHA256 /tr $TimestampUrl /f $PfxPath /p $Password $FilePath
if ($LASTEXITCODE -ne 0) {
    throw "signtool failed for $FilePath with exit code $LASTEXITCODE."
}

& $signTool.FullName verify /pa /all /v $FilePath
if ($LASTEXITCODE -ne 0) {
    throw "Authenticode verification failed for $FilePath with exit code $LASTEXITCODE."
}

Write-Host "Signed and verified $FilePath"
