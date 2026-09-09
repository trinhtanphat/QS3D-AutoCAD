# AutoCAD MCP Tooling / Function-Calling Parity Implementation Plan

> **For agent execution:** Follow TDD and exact-head CI. Do not merge to `main` without separate explicit owner authorization.

**Goal:** Port the transferable MCP/tooling/function-calling architecture from `QS3D-BricsCAD` into `QS3D-AutoCAD` with Autodesk AutoCAD-native adapters, preserving mutation safety and bounded automation contracts while eliminating BricsCAD/Teigha dependencies and BricsCAD-specific public tool names.

**Architecture:** Keep the MCP protocol, tool schema registry, action-id replay ledger, writer coordination, diagnostics, emergency stop, local embedded transport, desktop automation and execution-mode/capability concepts host-neutral. Put native CAD reads/mutations behind AutoCAD-specific runtime code using `Autodesk.AutoCAD.*`. Reuse the existing `Qs3dCommandCatalog` / `Qs3dCommandDispatcher` for QS3D command dispatch. External tunnel providers remain opt-in and disabled by default.

**Tech stack:** C#, Autodesk AutoCAD managed API, .NET Framework host targets used by the repository, PowerShell CI guards, GitHub Actions.

---

## Task 1: Establish TDD parity guard

**Files:**
- Create: `scripts/validate-autocad-mcp-parity.ps1`
- Modify: `.github/workflows/ci.yml`

1. Add a deterministic source guard that requires the approved MCP architecture/files and representative public tool names.
2. Guard against any `Bricscad.` / `Teigha.` namespace leakage in AutoCAD MCP sources.
3. Require AutoCAD-specific public names (`autocad_status`, `autocad_ui_*`, `autocad_interaction_policy_*`) and reject BricsCAD public-name leakage.
4. Require mutation contracts (`confirmMutation`, action-id/replay ledger, writer coordination, stop/resume) and local-only/default-off external transport behavior.
5. Wire the guard into CI.
6. Commit guard-only changes and prove exact-head CI RED specifically because the production MCP implementation is absent.

## Task 2: Host-neutral MCP contracts and registry

**Files:**
- Create under `src/QS3D.AutoCAD/Infrastructure/Mcp/`:
  - `McpTopLevelJson.cs`
  - `McpToolCapabilityContract.cs`
  - `McpToolRegistry.cs`
  - `McpMutationAckLedger.cs`
  - `McpCadMutationCoordinator.cs`
  - `McpDiagnosticHub.cs`

1. Port/normalize bounded top-level JSON extraction and tool schema helpers.
2. Define a single tool registry for function-calling / `tools/list` descriptors.
3. Preserve execution-mode checks and mutation classification.
4. Port action-id reserve/replay semantics and writer coordination.
5. Keep diagnostics bounded and deterministic.

## Task 3: AutoCAD-native CAD runtime

**Files:**
- Create:
  - `McpCadAgentRuntime.cs`
  - `McpCadDirectModelRuntime.cs`
  - `McpCadLayerStateRuntime.cs`
  - `McpCadViewStatusRuntime.cs`
  - `McpQs3dDomainRuntime.cs`

1. Port status/document/selection/database/entity/view/sysvar read tools to `Autodesk.AutoCAD.*`.
2. Port bounded primitive/entity/layer mutations with `confirmMutation=true`, action-id replay and writer coordination.
3. Port direct model functions: box/extrude/boolean/save/save-as where supported by AutoCAD APIs.
4. Bridge `qs3d_run_command` through the existing AutoCAD command catalog/dispatcher; do not create a competing command system.
5. Expose `autocad_status` rather than `bricscad_status`.
6. Preserve emergency stop/resume and audit boundaries.

## Task 4: Desktop/UI automation adapter

**Files:**
- Create: `McpDesktopAutomationRuntime.cs`

1. Port bounded current-session Windows automation only (window metadata/focus, cursor/mouse, keyboard, clipboard, screenshot, sequence, diagnostics/theme).
2. Preserve local-consent boundaries for mutation and sensitive reads.
3. Rename BricsCAD-specific public functions to AutoCAD equivalents.
4. Do not add process/shell/script execution.

## Task 5: Embedded MCP transport and lifecycle

**Files:**
- Create:
  - `McpEmbeddedServer.cs`
  - `McpTransportSettings.cs`
  - `McpTransportSupervisor.cs`
- Modify: `src/QS3D.AutoCAD/PluginEntry.cs`

1. Add loopback/local embedded MCP server lifecycle and JSON-RPC initialize/tools-list/tools-call handling.
2. Bind narrowly to loopback by default.
3. Keep external transport/tunnel startup explicitly disabled unless user configuration enables it.
4. Start/stop MCP with AutoCAD plugin initialize/terminate without allowing transport lifecycle code to own CAD mutations.

## Task 6: Regression/parity hardening

**Files:**
- Modify: `scripts/validate-autocad-mcp-parity.ps1`
- Modify: `.github/workflows/ci.yml` as needed

1. Verify the registry covers the approved status/read/mutation/direct-model/QS3D/desktop families.
2. Verify mutation tools are confirmation gated.
3. Verify stop/resume and action-id replay plumbing are reachable.
4. Verify no BricsCAD/Teigha references or BricsCAD-specific public function names remain.
5. Verify supported AutoCAD host builds still run in CI.

## Task 7: Exact-head integration handoff

1. Refresh the remote task branch before each material push; never force-push.
2. Run exact-head branch CI until GREEN.
3. Compare branch against current `main`; reconcile if `main` moved.
4. Open a non-draft PR with Issue #95 linkage, RED/GREEN evidence, scope notes and explicit native-runtime limitation.
5. Verify exact-head PR CI GREEN.
6. Add READY_FOR_INTEGRATION handoff to Issue #95.
7. Do **not** merge to `main` unless the owner separately grants explicit merge authorization.
