# AutoCAD MCP Ecosystem Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port every transferable MCP ecosystem family from the pinned QS3D-BricsCAD baseline into QS3D-AutoCAD while preserving PR #97 safety and backward compatibility.

**Architecture:** Extend the existing AutoCAD MCP core rather than creating a second runtime. New ecosystem services are small host-neutral/stateful units under `Infrastructure/Mcp`; same-process UI and CAD interactions use AutoCAD/Windows adapters; Agent Center/ribbon are thin local-user surfaces. Public transports, foreground desktop control and repair mutations are fail-closed and explicit.

**Tech Stack:** C#, Autodesk AutoCAD .NET APIs, WPF/Windows UI Automation, HttpListener/HttpClient/local IPC where already supported, PowerShell CI guards, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-09-autocad-mcp-ecosystem-parity.md`

## Global Constraints

- Pinned BricsCAD reference: `5e198b249b45795f865f980eb7e1344cb05e0f84`.
- Execution branch baseline refreshed to AutoCAD `main` `961ad5dde768f925d0f839803229da5a8e51ee14` before implementation.
- No `Bricscad.*` or `Teigha.*` references.
- Existing PR #97 MCP tool schemas remain backward compatible; extensions are additive.
- External/public transports are disabled by default and are never auto-published.
- Foreground/global desktop control is disabled by default and requires explicit local consent.
- CAD mutations retain `confirmMutation`, emergency stop, action-id/replay, writer coordination and audit gates.
- No automatic paid-service activation or credit/top-up behavior.
- Hosted CI is not native licensed AutoCAD runtime acceptance.
- No merge to `main` without separate owner authorization.

---

### Task 1: Ecosystem parity manifest and RED CI guard

**Files:**
- Create: `docs/mcp-ecosystem-parity.json`
- Create: `scripts/validate-autocad-mcp-ecosystem-parity.ps1`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: source tree and pinned family list from the spec.
- Produces: a fail-closed parity manifest and CI gate required by all later tasks.

- [ ] **Step 1: Write the failing guard**

Create a PowerShell script that loads `docs/mcp-ecosystem-parity.json`, verifies `schemaVersion=1`, checks every required family has `status=implemented` or `host-inapplicable`, requires a concrete reason for host-inapplicable entries, requires every `implemented` family path to exist, scans `src/QS3D.AutoCAD` for `Bricscad.`/`Teigha.`, and asserts safe-default source markers: public transports disabled, foreground disabled, no automatic paid activation.

- [ ] **Step 2: Add the initial manifest in RED state**

Record the ten spec families with explicit expected AutoCAD paths but set newly missing families to `pending`. Keep PR #97 core entries as already implemented.

- [ ] **Step 3: Wire the guard into CI**

Add a named `AutoCAD MCP ecosystem parity guard` step immediately after the existing MCP parity guard in `.github/workflows/ci.yml`:

```powershell
pwsh -NoProfile -File scripts/validate-autocad-mcp-ecosystem-parity.ps1
```

- [ ] **Step 4: Verify RED**

Run the branch workflow and confirm the new guard fails because `pending` ecosystem families remain, while existing unrelated gates do not define the expected failure.

- [ ] **Step 5: Commit**

Commit message: `test(mcp): add ecosystem parity fail-closed guard`.

---

### Task 2: Settings, provenance and transport profiles

**Files:**
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpEcosystemSettings.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpRuntimeBuildProvenance.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpTransportProfileRegistry.cs`
- Modify: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpTransportOptions.cs`
- Modify: `docs/mcp-ecosystem-parity.json`

**Interfaces:**
- Produces: `McpEcosystemSettings.Snapshot()`, `McpRuntimeBuildProvenance.SnapshotJson()`, `McpTransportProfileRegistry.GetSelected()/SelectLocal(...)` and validated non-secret profile records.

- [ ] **Step 1: Add contract assertions to the parity guard**

Assert settings never expose fields named password/secret/token/key; require build provenance fields `host`, `contractVersion`, `buildSha`, `buildId`, `buildUtc`; require built-in local profile with `Public=false`, `Enabled=true` and all public profiles `Enabled=false` by default.

- [ ] **Step 2: Verify the new assertions fail**

Run the PowerShell guard against the branch source and confirm missing files/contracts fail.

- [ ] **Step 3: Implement minimal safe state**

Use process-safe locking and immutable snapshots. Persist only non-secret settings in a user-local QS3D settings file when persistence is needed. Profile selection must not start/stop transports.

- [ ] **Step 4: Update manifest family statuses covered by this task**

Mark settings/provenance/transport-profile portions implemented with exact paths.

- [ ] **Step 5: Verify guard and supported host builds**

Run parity guard and CI host build matrix.

- [ ] **Step 6: Commit**

Commit message: `feat(mcp): add safe ecosystem settings and transport profiles`.

---

### Task 3: Desktop-control session and background same-process UI

**Files:**
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpDesktopControlSession.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpBackgroundHostRuntime.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpBackgroundSemanticUiRuntime.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpPopupWindowClassifier.cs`
- Modify: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpDesktopAutomationRuntime.cs`
- Modify: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpCadAgentRuntime.cs`
- Modify: `docs/mcp-ecosystem-parity.json`

**Interfaces:**
- Produces: local-consent session state; `autocad_interaction_policy_get/set`, `autocad_ui_text_snapshot`, `autocad_ui_invoke`, `autocad_ui_set_text`; semantic discovery generation and same-process popup classification.

- [ ] **Step 1: Extend the guard with background/foreground safety contracts**

Require advertised AutoCAD tool names, process-start `background_only`, remote rejection of foreground enablement, local consent reset-on-process-start, bounded semantic depth/node constants and current-process HWND ownership checks.

- [ ] **Step 2: Verify RED**

Confirm the new assertions fail before implementation.

- [ ] **Step 3: Implement local-consent session**

`EnableFromLocalUser` and `Disable` are the only state transitions; emergency stop/pause revokes consent. The public MCP setter can only choose `background_only`.

- [ ] **Step 4: Implement text and semantic background control**

Port host-neutral Win32/UIA concepts from BricsCAD but replace document/window affinity with AutoCAD process/document identity. Reads are bounded. Semantic mutations require fresh discovery generation and exact control identity; uncertain provider outcomes write an accepted-but-uncertain acknowledgement and prohibit retry.

- [ ] **Step 5: Gate existing global desktop automation**

Before screenshot/focus/mouse/keyboard/clipboard/sequence operations, require both foreground policy and active local consent.

- [ ] **Step 6: Wire descriptors/dispatch and update manifest**

Advertise only `autocad_*` host-specific tools.

- [ ] **Step 7: Verify guard/build and commit**

Commit message: `feat(mcp): add background and consent-gated desktop control`.

---

### Task 4: Embedded server v2, watchdog and local agent

**Files:**
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpEmbeddedServerV2.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpRuntimeWatchdog.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpLocalAgentClient.cs`
- Modify: `src/QS3D.AutoCAD/PluginEntry.cs`
- Modify: `docs/mcp-ecosystem-parity.json`

**Interfaces:**
- Produces: lifecycle facade `EnsureStarted/Stop/StatusJson`, watchdog `Start/Stop/Snapshot`, loopback-only local client request method with bounded timeout/payload.

- [ ] **Step 1: Add guard assertions**

Require V2 to delegate to canonical `McpEmbeddedServer`, require watchdog emergency-stop/mutation checks, and require local client loopback validation/no public fallback.

- [ ] **Step 2: Verify RED**

Run guard and confirm missing lifecycle files fail.

- [ ] **Step 3: Implement V2 facade and watchdog**

Do not duplicate tool registry. Watchdog may restart only safe local listener/runtime lifecycle state; it may not intervene during a writer/mutation or emergency stop.

- [ ] **Step 4: Implement bounded local-agent client**

Reject non-loopback endpoints, cap request/response sizes, enforce timeout and cancellation.

- [ ] **Step 5: Wire plugin lifecycle**

Initialize/terminate the facade/watchdog exactly once through plugin lifecycle while preserving the existing server behavior.

- [ ] **Step 6: Update manifest, verify build, commit**

Commit message: `feat(mcp): add v2 lifecycle watchdog and local agent`.

---

### Task 5: Public endpoint, Cloudflare and secure tunnel capability layer

**Files:**
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpPublicEndpointResolver.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpCloudflareOnboarding.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpSecureTunnelRuntime.cs`
- Modify: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpTransportProfileRegistry.cs`
- Modify: `docs/mcp-ecosystem-parity.json`

**Interfaces:**
- Produces: validated endpoint resolution, non-secret Cloudflare onboarding/readiness status, explicit local-only tunnel start/stop abstraction.

- [ ] **Step 1: Add guard assertions for safe public defaults**

Require `Enabled = false`/`Publish = false` for public profiles; HTTPS-only remote endpoints; no credentials in URL; explicit-local-action marker for tunnel activation; secret redaction markers.

- [ ] **Step 2: Verify RED**

Run guard before implementation.

- [ ] **Step 3: Implement resolver and onboarding state**

Resolver returns readiness errors rather than silently switching providers. Cloudflare onboarding records account/profile identifiers and required manual setup steps only; no purchase/resource creation path is added.

- [ ] **Step 4: Implement secure tunnel abstraction**

Tunnel process launch/configuration is callable only from an explicit local-user route. Never log runtime key/token; do not auto-start from plugin load, settings load or remote MCP call.

- [ ] **Step 5: Update manifest, verify, commit**

Commit message: `feat(mcp): add opt-in public transport capability layer`.

---

### Task 6: OAuth authorization and consent

**Files:**
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpOAuthAuthorizationServer.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpOAuthConsentStore.cs`
- Modify: `docs/mcp-ecosystem-parity.json`

**Interfaces:**
- Produces: deny-by-default authorization flow with bounded scopes, loopback redirect allowlist, short-lived one-time codes and redacted diagnostics.

- [ ] **Step 1: Add auth guard assertions**

Require deny-by-default, code expiry, one-time consumption, loopback redirect validation, token/secret redaction and no browser-cookie APIs.

- [ ] **Step 2: Verify RED**

Run guard.

- [ ] **Step 3: Implement consent and authorization state machine**

Use cryptographically strong random identifiers available on all project targets, store only hashes when persistence is needed, bound scope count/string sizes and expiration.

- [ ] **Step 4: Update manifest, verify, commit**

Commit message: `feat(mcp): add local OAuth consent contracts`.

---

### Task 7: Recovery, popup observation and self-healing

**Files:**
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpPopupObserver.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpProjectRecovery.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpSelfHealingRepair.cs`
- Modify: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpDiagnosticHub.cs`
- Modify: `docs/mcp-ecosystem-parity.json`

**Interfaces:**
- Produces: bounded popup events, recovery-plan snapshots, safe-repair actions and diagnostics.

- [ ] **Step 1: Extend guard**

Require same-process popup ownership, recovery plan-before-mutation, no arbitrary dialog dismissal, no DWG mutation in automatic repair, and audit calls for explicit repair.

- [ ] **Step 2: Verify RED**

Run guard.

- [ ] **Step 3: Implement observer/classification integration**

Poll/observe only same-process windows, deduplicate bounded events and never click automatically.

- [ ] **Step 4: Implement recovery/self-healing**

Safe automatic actions are limited to local runtime lifecycle/stale non-secret state. Any drawing-affecting or explicit repair path delegates into canonical mutation gating.

- [ ] **Step 5: Update diagnostics/manifest, verify, commit**

Commit message: `feat(mcp): add bounded recovery and self-healing`.

---

### Task 8: QS3D local IPC/code-host bridge

**Files:**
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/Qs3dCodeHostContracts.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/Qs3dCodeHostBridge.cs`
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/Qs3dCodeHostLocalIpcServer.cs`
- Modify: `docs/mcp-ecosystem-parity.json`

**Interfaces:**
- Produces: versioned local request/response envelope, bounded local-only IPC service and bridge to canonical MCP tool execution.

- [ ] **Step 1: Extend guard**

Require version/requestId/operation contracts, local-only transport, message/time limits, no arbitrary shell execution, and canonical MCP delegation for mutations.

- [ ] **Step 2: Verify RED**

Run guard.

- [ ] **Step 3: Implement contracts and local server**

Use a local-only mechanism supported by project targets. Validate request IDs, versions and bounded body sizes before dispatch.

- [ ] **Step 4: Implement bridge**

Allow an explicit allowlist of MCP/QS3D operations; mutation requests keep normal MCP confirmation/action-id/writer/audit behavior.

- [ ] **Step 5: Update manifest, verify, commit**

Commit message: `feat(mcp): add local QS3D code-host IPC bridge`.

---

### Task 9: Agent Center, first-run and ribbon integration

**Files:**
- Create or modify AutoCAD Agent Center service/window files under the existing UI/service architecture after fetching exact current paths.
- Create: `src/QS3D.AutoCAD/Infrastructure/Mcp/McpFirstRunExperience.cs`
- Modify existing MCP command/ribbon integration files after exact path discovery.
- Modify: `docs/mcp-ecosystem-parity.json`

**Interfaces:**
- Produces: local Agent Center UI for connection, Agent control, recovery and advanced diagnostics; first-run idempotent setup; ribbon/command launch paths.

- [ ] **Step 1: Discover exact existing AutoCAD UI/ribbon paths**

Fetch current `Commands`, `UI` and ribbon-related files from the branch; reuse existing QS3D ribbon patterns and command dispatcher.

- [ ] **Step 2: Extend guard**

Require Agent Center command/ribbon entry, first-run idempotence marker, no automatic public/tunnel/foreground enablement, and AutoCAD host wording.

- [ ] **Step 3: Verify RED**

Run guard.

- [ ] **Step 4: Implement thin local-user surfaces**

Render state from ecosystem services. Buttons invoke explicit local actions only. Do not duplicate transport or mutation logic in WPF handlers.

- [ ] **Step 5: Update manifest to all-implemented and verify guard**

Every required family must now be `implemented` or concretely `host-inapplicable`; no `pending` may remain.

- [ ] **Step 6: Commit**

Commit message: `feat(mcp): integrate Agent Center and ribbon ecosystem controls`.

---

### Task 10: Full verification and PR handoff

**Files:**
- Modify documentation only if verification discovers contract notes that must be recorded.

**Interfaces:**
- Produces: exact-head green branch, non-draft PR, exact-head green PR and integration handoff.

- [ ] **Step 1: Run parity guards and exact-head CI**

Verify existing core MCP parity plus new ecosystem parity, supported AutoCAD host builds, smoke tests, architecture/bundle/release/security gates.

- [ ] **Step 2: Inspect any failure by job/step/log and fix on the branch**

Do not weaken a guard merely to make CI green; reconcile source/contract defects and rerun exact-head CI.

- [ ] **Step 3: Refresh current `main` and compare**

If `main` moved, reconcile non-destructively and rerun exact-head CI.

- [ ] **Step 4: Open a non-draft PR**

Title: `feat(mcp): complete AutoCAD MCP ecosystem parity`. Body must include `Closes #98`, subsystem summary, exact validation SHA/runs, safe-default notes and explicit native-runtime caveat.

- [ ] **Step 5: Verify exact-head PR CI**

Only mark READY_FOR_INTEGRATION when every required hosted gate is green.

- [ ] **Step 6: Stop before merge**

No merge to `main` occurs without a new explicit owner authorization.