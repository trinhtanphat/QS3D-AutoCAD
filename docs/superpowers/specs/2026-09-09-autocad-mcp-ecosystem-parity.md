# AutoCAD MCP Ecosystem Parity Specification

## Goal

Port every transferable MCP ecosystem family present on the pinned `QS3D-BricsCAD` baseline `5e198b249b45795f865f980eb7e1344cb05e0f84` into `QS3D-AutoCAD`, building on PR #97 without copying BricsCAD/Teigha host bindings.

The execution branch is `agent/chatgpt-gpt56sol/mcp-ecosystem-parity-20260909`, refreshed to AutoCAD `main` `961ad5dde768f925d0f839803229da5a8e51ee14` before implementation.

## Architectural rule

Use contract parity plus AutoCAD adapters. Host-neutral MCP state machines, safety rules, diagnostics and transport concepts may mirror BricsCAD behavior. Anything that touches the CAD host must use Autodesk AutoCAD APIs or same-process Windows APIs with AutoCAD process/document affinity. No source file in the AutoCAD host may reference `Bricscad.*` or `Teigha.*`.

## Transferable families

### 1. Agent Center, first-run and settings

AutoCAD must expose a single MCP Agent Center experience that reports local MCP state, transport state, background/foreground control state, diagnostics, recovery and setup status. First-run onboarding must be idempotent and must never publish an endpoint, start a tunnel, enable foreground desktop control, or store provider credentials without explicit local user action.

Settings persisted by QS3D are limited to non-secret local preferences and provider/profile identifiers. Provider credentials remain provider-owned or OS-protected and are not scraped from ChatGPT/browser sessions.

### 2. Background host and semantic UI

Add same-process background UI discovery/control for AutoCAD. Background mode is the process-start default. It may inspect only windows owned by the current AutoCAD process, must use bounded text/semantic metadata, and must not capture pixels or inject global input.

Semantic mutations require a fresh discovery generation plus exact element identity. Provider calls that may have executed but cannot be verified return an uncertain outcome and must not be automatically retried. Existing action-id acknowledgement/replay safety from PR #97 remains authoritative.

Foreground/global desktop input remains a separate local-consent gate. Remote calls cannot enable it.

### 3. Embedded server v2, watchdog and local-agent client

`McpEmbeddedServerV2` is a lifecycle facade over the existing embedded MCP core, not a second competing tool registry. It owns health/version reporting and delegates protocol/tool execution to the canonical server/runtime.

A watchdog may observe health and request bounded restart/recovery, but it must fail closed while CAD mutation is active or emergency stop is set. The local-agent client is loopback/local-only by default, uses explicit timeouts and bounded payloads, and never silently falls back to a public endpoint.

### 4. Public endpoint, Cloudflare and secure tunnel

Add configuration/onboarding contracts for Cloudflare/public endpoint and secure-tunnel providers. These are capabilities, not automatic activation. Defaults are `enabled=false`, `publish=false`, and local transport remains preferred.

No code in this phase may purchase a service, top up credits, create a paid resource automatically, or turn on a public endpoint simply because configuration exists. A public endpoint resolver must reject non-HTTPS remote endpoints, credentials in URLs, loopback/public ambiguity, and unapproved provider/profile state.

Secure tunnel startup is an explicit local action. Runtime keys/tokens must not be written to logs, diagnostics or repository files.

### 5. OAuth authorization and consent

Provide a local authorization/consent state machine suitable for MCP clients. Authorization is deny-by-default, uses short-lived one-time codes/tokens, loopback redirect allowlisting, bounded scopes and explicit consent. No browser cookie scraping. No bearer token is returned by diagnostics or persisted in plaintext QS3D settings.

### 6. Popup observer/classifier, recovery, self-healing and provenance

Popup observation is same-process only. The classifier reports bounded metadata and classifies known modal/command/error surfaces without clicking by default.

Recovery produces a plan before mutation. Destructive or CAD-mutating repair routes through `confirmMutation`, emergency-stop checks, writer coordination and audit. Automatic self-healing is limited to safe runtime/lifecycle repairs such as restarting a stopped local listener or clearing stale non-secret state; it must not edit drawings, dismiss arbitrary dialogs or publish transports automatically.

Build provenance reports assembly/runtime identity, version, build SHA/build ID/build UTC when available, host identity and MCP contract version. It must not expose secrets or machine-private data beyond bounded diagnostic identity.

### 7. Desktop control session

Add a process-scoped desktop-control session that tracks local consent independently from interaction policy. Consent resets on process restart and can be revoked by pause/emergency-stop/Esc handling. Global mouse/keyboard/clipboard/screenshot operations in the existing desktop automation runtime must require both foreground policy and active local consent.

### 8. Transport profiles and Agent Center augmentation

Transport profiles are named, validated, non-secret configuration records. Built-in profiles cover local embedded transport and optional explicitly configured public/tunnel transports. Selecting a profile does not activate it. Agent Center exposes selected provider/profile, readiness, warnings and explicit local actions.

### 9. QS3D code-host / local IPC bridge

Provide a local IPC bridge for QS3D code-host integration using loopback or named-pipe/local-process semantics. The bridge has versioned request/response contracts, bounded messages, request IDs, timeouts and cancellation. It is not a generic unauthenticated remote code-execution endpoint.

Any operation that results in CAD mutation delegates to the canonical MCP mutation path and its confirmation/action-id/writer/audit contracts. Arbitrary shell execution is out of scope.

### 10. Ribbon and command integration

Expose AutoCAD-specific commands and ribbon entry points for Agent Center, connection/setup, diagnostics and safe start/stop actions. Reuse existing QS3D UI/ribbon architecture. Do not create duplicate command universes when an existing QS3D command or service is the canonical route.

## Public naming

Host-specific public MCP tools use the `autocad_` prefix. Generic cross-host concepts may retain `mcp_`, `desktop_` or `qs3d_` names. Compatibility aliases may exist internally but AutoCAD must not advertise `bricscad_*` tool names.

## Backward compatibility

PR #97 tool schemas and behavior remain valid unless a new versioned contract explicitly extends them. New ecosystem status fields are additive. Existing `confirmMutation`, action-id acknowledgement, replay blocking, writer coordination, audit and emergency-stop behavior remains authoritative.

## Safety invariants

1. Local/embedded transport is the default; external/public transport is off by default.
2. Foreground/global input is off by default and requires local consent plus foreground policy.
3. Sensitive reads and all mutations retain their existing explicit gates.
4. Provider credentials, OAuth tokens and tunnel keys are never returned by diagnostics or committed to source.
5. Same-process UI automation is bound to the current AutoCAD process and fresh discovery identity.
6. Uncertain provider/mutation outcomes are not automatically retried.
7. Self-healing cannot mutate a DWG or dismiss arbitrary dialogs autonomously.
8. No paid resource/action is activated automatically.

## Host-inapplicable handling

A BricsCAD MCP family may be marked host-inapplicable only in the parity manifest with a concrete technical reason and replacement path. A generic statement such as "AutoCAD differs" is insufficient. Host-inapplicable entries still require a regression guard proving they remain intentionally classified.

## Verification

The CI parity guard must fail if a required family/contract file disappears, if forbidden BricsCAD/Teigha references appear, if safe defaults are changed to public/foreground enabled, or if required wiring is removed.

Hosted CI must compile all supported AutoCAD host targets and run source/contract guards. This is not native licensed AutoCAD runtime acceptance. Native runtime status must be reported separately.

## Completion criteria

Phase 2 is implementation-complete only when every transferable family above is implemented or concretely classified host-inapplicable, exact-head branch CI is green, a non-draft PR is open, and exact-head PR CI is green. Merge to `main` remains a separate owner-authorized action.