$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root 'docs/mcp-ecosystem-parity.json'
$sourceRoot = Join-Path $root 'src/QS3D.AutoCAD'

if (-not (Test-Path $manifestPath)) { throw "MCP ecosystem parity manifest is missing: $manifestPath" }
$manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1) { throw "MCP ecosystem parity schemaVersion must be 1." }
if ($manifest.bricsCadBaseline -ne '5e198b249b45795f865f980eb7e1344cb05e0f84') { throw 'Pinned BricsCAD MCP baseline changed without a versioned parity update.' }

$requiredFamilies = @(
  'agent-center-first-run-settings',
  'background-host-semantic-ui',
  'embedded-server-v2-watchdog-local-agent',
  'public-cloudflare-secure-tunnel',
  'oauth-authorization-consent',
  'popup-recovery-self-healing-provenance',
  'desktop-control-session',
  'transport-profiles-agent-center',
  'qs3d-code-host-local-ipc',
  'ribbon-command-integration'
)

$byId = @{}
foreach ($family in $manifest.families) {
  if ([string]::IsNullOrWhiteSpace([string]$family.id)) { throw 'MCP ecosystem family id is required.' }
  if ($byId.ContainsKey([string]$family.id)) { throw "Duplicate MCP ecosystem family: $($family.id)" }
  $byId[[string]$family.id] = $family
}

foreach ($id in $requiredFamilies) {
  if (-not $byId.ContainsKey($id)) { throw "Required MCP ecosystem family is missing: $id" }
  $family = $byId[$id]
  $status = [string]$family.status
  if ($status -ne 'implemented' -and $status -ne 'host-inapplicable') {
    throw "MCP ecosystem family is not complete: $id status=$status"
  }
  if ($status -eq 'host-inapplicable') {
    $reason = [string]$family.reason
    if ([string]::IsNullOrWhiteSpace($reason) -or $reason.Length -lt 24) {
      throw "Host-inapplicable MCP family requires a concrete technical reason: $id"
    }
  }
  if ($status -eq 'implemented') {
    if ($null -eq $family.paths -or $family.paths.Count -eq 0) { throw "Implemented MCP family has no source paths: $id" }
    foreach ($relative in $family.paths) {
      $path = Join-Path $root ([string]$relative)
      if (-not (Test-Path $path)) { throw "Implemented MCP ecosystem path is missing for $id: $relative" }
    }
  }
}

if ($manifest.safeDefaults.publicTransportEnabled -ne $false) { throw 'Public MCP transport must be disabled by default.' }
if ($manifest.safeDefaults.publicPublishEnabled -ne $false) { throw 'Public MCP publication must be disabled by default.' }
if ($manifest.safeDefaults.foregroundControlEnabled -ne $false) { throw 'Foreground MCP desktop control must be disabled by default.' }
if ($manifest.safeDefaults.paidServiceAutoActivation -ne $false) { throw 'Paid-service automatic activation must remain disabled.' }

$allSource = (Get-ChildItem -Path $sourceRoot -Recurse -File -Filter '*.cs' | ForEach-Object { Get-Content -Raw $_.FullName }) -join "`n"
foreach ($token in @('Bricscad.', 'Teigha.', 'bricscad_status', 'bricscad_ui_', 'bricscad_interaction_policy_')) {
  if ($allSource -match [regex]::Escape($token)) { throw "Forbidden BricsCAD/Teigha MCP ecosystem token leaked into AutoCAD: $token" }
}

# Contract-level fail-closed markers. These deliberately assert source semantics rather than runtime availability.
$requiredTokens = @(
  'background_only',
  'EnableFromLocalUser',
  'requiresLocalConsent',
  'autocad_interaction_policy_get',
  'autocad_ui_text_snapshot',
  'expectedDiscoveryGeneration',
  'McpEmbeddedServerV2',
  'McpRuntimeWatchdog',
  'IsLoopback',
  'Public = false',
  'Enabled = false',
  'Publish = false',
  'https',
  'explicitLocalAction',
  'deny-by-default',
  'one-time',
  'loopback',
  'McpPopupWindowClassifier',
  'McpProjectRecovery',
  'McpSelfHealingRepair',
  'buildSha',
  'buildId',
  'buildUtc',
  'Qs3dCodeHostBridge',
  'requestId',
  'contractVersion',
  'QS3DMCPAGENTCENTER'
)
foreach ($token in $requiredTokens) {
  if ($allSource -notmatch [regex]::Escape($token)) { throw "MCP ecosystem safety/contract token is missing: $token" }
}

# Explicitly ban common unsafe activation shapes in ecosystem sources.
$ecosystemFiles = Get-ChildItem -Path (Join-Path $sourceRoot 'Infrastructure/Mcp') -File -Filter '*.cs' -ErrorAction SilentlyContinue
$ecosystemSource = ($ecosystemFiles | ForEach-Object { Get-Content -Raw $_.FullName }) -join "`n"
foreach ($unsafe in @('Public = true; // default', 'Publish = true; // default', 'ForegroundEnabled = true; // default', 'AutoPurchase', 'TopUpCredits')) {
  if ($ecosystemSource -match [regex]::Escape($unsafe)) { throw "Unsafe automatic MCP ecosystem activation marker detected: $unsafe" }
}

Write-Host 'AutoCAD MCP ecosystem parity guard passed.'
