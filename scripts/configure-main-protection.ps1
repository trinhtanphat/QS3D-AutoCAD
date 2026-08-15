[CmdletBinding()]
param(
    [ValidateSet('Verify', 'Apply')][string]$Mode = 'Verify',
    [string]$Repository = 'trinhtanphat/QS3D-AutoCAD',
    [string]$Branch = 'main',
    [string[]]$RequiredChecks = @('core-host-and-guards'),
    [switch]$ConfirmApply
)

$ErrorActionPreference = 'Stop'
if ($Repository -notmatch '^[^/]+/[^/]+$') {
    throw "Repository must be in owner/name form: $Repository"
}
if ([string]::IsNullOrWhiteSpace($Branch)) {
    throw 'Branch is required.'
}
if ($RequiredChecks.Count -eq 0 -or @($RequiredChecks | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
    throw 'At least one non-empty required status check is required.'
}
if ($null -eq (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required. Authenticate it with an admin-capable token before applying/verifying branch protection.'
}

function Invoke-GhJson([string[]]$Arguments) {
    $output = @(& gh @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($Arguments -join ' ') failed:`n$($output -join [Environment]::NewLine)"
    }
    return (($output -join [Environment]::NewLine) | ConvertFrom-Json)
}

function Read-Protection {
    return Invoke-GhJson @(
        'api',
        '-H', 'Accept: application/vnd.github+json',
        '-H', 'X-GitHub-Api-Version: 2022-11-28',
        "repos/$Repository/branches/$Branch/protection"
    )
}

if ($Mode -eq 'Apply') {
    if (-not $ConfirmApply) {
        throw 'Apply mode changes GitHub repository settings. Re-run with -ConfirmApply after reviewing the requested policy.'
    }

    $payload = [ordered]@{
        required_status_checks = [ordered]@{
            strict = $true
            contexts = @($RequiredChecks)
        }
        enforce_admins = $true
        required_pull_request_reviews = [ordered]@{
            dismiss_stale_reviews = $false
            require_code_owner_reviews = $false
            required_approving_review_count = 0
            require_last_push_approval = $false
        }
        restrictions = $null
        required_linear_history = $false
        allow_force_pushes = $false
        allow_deletions = $false
        block_creations = $false
        required_conversation_resolution = $true
        lock_branch = $false
        allow_fork_syncing = $true
    }

    $payloadPath = Join-Path ([IO.Path]::GetTempPath()) ("qs3d-main-protection-" + [Guid]::NewGuid().ToString('N') + '.json')
    try {
        $payload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $payloadPath -Encoding utf8NoBOM
        [void](Invoke-GhJson @(
            'api', '--method', 'PUT',
            '-H', 'Accept: application/vnd.github+json',
            '-H', 'X-GitHub-Api-Version: 2022-11-28',
            "repos/$Repository/branches/$Branch/protection",
            '--input', $payloadPath
        ))
    }
    finally {
        Remove-Item -LiteralPath $payloadPath -Force -ErrorAction SilentlyContinue
    }
}

$protection = Read-Protection
$violations = [Collections.Generic.List[string]]::new()
if ($null -eq $protection.required_pull_request_reviews) {
    $violations.Add('pull requests are not required before protected-branch updates')
}
if ($protection.enforce_admins.enabled -ne $true) {
    $violations.Add('administrator enforcement is disabled')
}
if ($protection.allow_force_pushes.enabled -eq $true) {
    $violations.Add('force pushes are allowed')
}
if ($protection.allow_deletions.enabled -eq $true) {
    $violations.Add('branch deletion is allowed')
}
if ($protection.required_conversation_resolution.enabled -ne $true) {
    $violations.Add('conversation resolution is not required')
}
if ($null -eq $protection.required_status_checks -or $protection.required_status_checks.strict -ne $true) {
    $violations.Add('strict required-status-check behavior is not enabled')
}

$presentChecks = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($context in @($protection.required_status_checks.contexts)) {
    if (-not [string]::IsNullOrWhiteSpace([string]$context)) { [void]$presentChecks.Add([string]$context) }
}
foreach ($check in @($protection.required_status_checks.checks)) {
    if (-not [string]::IsNullOrWhiteSpace([string]$check.context)) { [void]$presentChecks.Add([string]$check.context) }
}
foreach ($required in $RequiredChecks) {
    if (-not $presentChecks.Contains($required)) {
        $violations.Add("required status check is missing: $required")
    }
}

if ($violations.Count -gt 0) {
    throw "Branch protection for $Repository/$Branch is not compliant:`n- $($violations -join "`n- ")"
}

Write-Host "Branch protection verified for $Repository/$Branch."
Write-Host "Required checks: $($RequiredChecks -join ', ')"
Write-Host 'PR-only updates, admin enforcement, strict checks, conversation resolution, no force-push and no deletion are all enabled.'
