$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mcpRoot = Join-Path $root 'src/QS3D.AutoCAD/Infrastructure/Mcp'
$pluginPath = Join-Path $root 'src/QS3D.AutoCAD/PluginEntry.cs'

$requiredFiles = @(
    'McpTopLevelJson.cs',
    'McpToolCapabilityContract.cs',
    'McpToolRegistry.cs',
    'McpMutationAckLedger.cs',
    'McpCadMutationCoordinator.cs',
    'McpDiagnosticHub.cs',
    'McpCadAgentRuntime.cs',
    'McpCadDirectModelRuntime.cs',
    'McpCadLayerStateRuntime.cs',
    'McpCadViewStatusRuntime.cs',
    'McpQs3dDomainRuntime.cs',
    'McpDesktopAutomationRuntime.cs',
    'McpEmbeddedServer.cs',
    'McpTransportSettings.cs',
    'McpTransportSupervisor.cs'
)

if (-not (Test-Path $mcpRoot)) {
    throw "AutoCAD MCP runtime directory is missing: $mcpRoot"
}

foreach ($file in $requiredFiles) {
    $path = Join-Path $mcpRoot $file
    if (-not (Test-Path $path)) {
        throw "AutoCAD MCP parity file is missing: $file"
    }
}

$sources = ($requiredFiles | ForEach-Object { Get-Content -Raw (Join-Path $mcpRoot $_) }) -join "`n"
$plugin = Get-Content -Raw $pluginPath

$forbidden = @(
    'Bricscad.',
    'Teigha.',
    'bricscad_status',
    'bricscad_ui_',
    'bricscad_interaction_policy_'
)
foreach ($token in $forbidden) {
    if ($sources -match [regex]::Escape($token)) {
        throw "BricsCAD/Teigha leakage detected in AutoCAD MCP runtime: $token"
    }
}

$requiredTokens = @(
    'mcp_status',
    'autocad_status',
    'qs3d_status',
    'cad_active_document',
    'cad_mutation_status',
    'cad_selection',
    'cad_database_snapshot',
    'cad_entity_inspect',
    'cad_view_state',
    'cad_wait_idle',
    'cad_sysvar',
    'cad_create_line',
    'cad_create_circle',
    'cad_create_arc',
    'cad_create_polyline',
    'cad_create_text',
    'cad_create_mtext',
    'cad_entity_transform',
    'cad_entity_delete',
    'cad_entity_set_layer',
    'cad_layer',
    'cad_command_catalog',
    'cad_command_sequence',
    'qs3d_run_command',
    'cad_create_box',
    'cad_extrude',
    'cad_boolean_union',
    'cad_boolean_subtract',
    'cad_boolean_intersect',
    'cad_save',
    'cad_save_as',
    'autocad_interaction_policy_get',
    'autocad_interaction_policy_set',
    'autocad_ui_text_snapshot',
    'autocad_ui_invoke',
    'autocad_ui_set_text',
    'desktop_window_list',
    'desktop_foreground_window',
    'desktop_window_focus',
    'desktop_mouse_move',
    'desktop_mouse_click',
    'desktop_type',
    'desktop_key',
    'desktop_clipboard_read',
    'desktop_clipboard_write',
    'desktop_screenshot',
    'desktop_sequence',
    'cad_agent_stop',
    'cad_agent_resume',
    'cad_audit_tail',
    'cad_cancel_command',
    'confirmMutation',
    'actionId',
    'writerToken',
    'ReserveOrReplay',
    'EnterMutation',
    'ToolDescriptors',
    '127.0.0.1',
    'ExternalTransportEnabledByDefault = false'
)
foreach ($token in $requiredTokens) {
    if ($sources -notmatch [regex]::Escape($token)) {
        throw "AutoCAD MCP parity contract token is missing: $token"
    }
}

$v2Path = Join-Path $mcpRoot 'McpEmbeddedServerV2.cs'
if (-not (Test-Path $v2Path)) { throw 'McpEmbeddedServerV2 lifecycle facade is missing.' }
$v2 = Get-Content -Raw $v2Path
if ($plugin -notmatch 'McpEmbeddedServerV2\.EnsureStarted' -or $v2 -notmatch 'McpEmbeddedServer\.Start') {
    throw 'PluginEntry must start the canonical embedded AutoCAD MCP server through V2.'
}
if ($plugin -notmatch 'McpEmbeddedServerV2\.Stop' -or $v2 -notmatch 'McpEmbeddedServer\.Stop') {
    throw 'PluginEntry must stop the canonical embedded AutoCAD MCP server through V2.'
}

Write-Host 'AutoCAD MCP tooling/function-calling parity guard passed.'
