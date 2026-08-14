# Work claim — commercial trust boundary

- Status: `ACTIVE`
- Agent: `chatgpt56sol`
- Registered: `2026-08-14T23:03:00+07:00`
- Baseline main SHA: `fd0a8f94f4d35d66e8a35c07f1c91a695bb11324`
- Implementation branch: `agent/chatgpt56sol/commercial-trust-boundary-20260814`
- Integration batch: `integration/commercial-trust-boundary-20260814`

## Reserved scope
Advance issue #6 with source-safe commercial trust primitives that can be tested without production secrets or services: deterministic license lease/offline-grace decisions and cryptographic updater-manifest/package verification bound to supported AutoCAD generations.

## Expected surfaces
- `src/QS3D.Core/Commercial/*`
- `tests/QS3D.Core.SmokeTests/Program.cs`
- release-security guards/docs as needed

## Invariants
- No always-allow, development bypass or embedded production credential/key.
- Missing/invalid/expired license state fails closed; offline grace is explicit and bounded by signed/validated lease timestamps supplied by a future real backend.
- License decisions are pure and must never mutate drawing data.
- Update manifests require an externally configured public key, a valid signature, HTTPS package URI, supported AutoCAD generation range and SHA-256 package identity.
- Package verification rejects hash mismatch before any future install/replace step.
- No network fetch, automatic installation, GitHub release mutation, activation backend or telemetry endpoint is added in this lane.

## Excluded scope
- Real account/subscription/device activation service and credentials
- Production signing certificate/PFX or updater private signing key
- Network updater/channel service and rollback execution
- Runtime command enforcement before backend/product policy is approved
- Production telemetry collection

## Validation plan
- deterministic Core smoke tests on .NET 8 and .NET 10 for active/grace/expired/mismatch cases
- cryptographic manifest tests use ephemeral test keys only and prove tamper/hash/generation failures
- architecture/release policy guards keep production-secret/service boundaries explicit
- exact integration and final-main CI remain green

## Completion condition
The pure-Core trust primitives, regression tests and source/security contract are integrated through agent -> integration -> main with exact final-main CI green. Issue #6 remains open for real production backend/credentials/network/native evidence.
