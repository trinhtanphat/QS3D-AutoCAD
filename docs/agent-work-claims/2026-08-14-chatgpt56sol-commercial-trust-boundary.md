# Work claim — commercial trust boundary

- Status: `COMPLETED`
- Agent: `chatgpt56sol`
- Registered: `2026-08-14T23:03:00+07:00`
- Baseline main SHA: `fd0a8f94f4d35d66e8a35c07f1c91a695bb11324`
- Implementation branch: `agent/chatgpt56sol/commercial-trust-boundary-20260814`
- Integration batch: `integration/commercial-trust-boundary-20260814`
- Integrated main SHA: `10ea1e77eabbbf28001a335d21051c7fff17fa12`
- Exact-main CI: run `31818322365` / CI #107 — `SUCCESS`
- Engineering artifact id: `9225954672`
- Engineering artifact name: `QS3D-AutoCAD-native-candidate-10ea1e77eabbbf28001a335d21051c7fff17fa12`
- Engineering artifact digest: `sha256:720ad701b9cba4d5febec4197966552fc7bcdc268348e308c50fef677154951d`

## Reserved scope
Advance issue #6 with source-safe commercial trust primitives that can be tested without production secrets or services: deterministic license lease/offline-grace decisions and cryptographic updater-manifest/package verification bound to supported AutoCAD generations.

## Integrated outcome
- Added `LicensePolicy` with deterministic `Active`, bounded `OfflineGrace`, and fail-closed `Denied` decisions for missing, malformed, not-yet-valid, expired, or wrong-device lease snapshots.
- Added `UpdateManifestVerifier` using an externally supplied public key, RSA-PSS/SHA-256 signature verification, exact signed payload bytes, HTTPS-only package URI, update-channel and AutoCAD-generation compatibility checks, and SHA-256 package verification with fixed-time comparison.
- Production Commercial source contains no network fetch, private signing key, signing operation, automatic install path, telemetry endpoint, or always-allow bypass.
- Cross-TFM smoke tests use only an ephemeral test RSA key and prove active/grace/expired/device cases plus signature tamper, package tamper, wrong channel/generation, HTTP URI, and malformed package-hash rejection.
- Release-policy guards lock the fail-closed/private-key/network boundaries and require `docs/COMMERCIAL-TRUST-BOUNDARY.md`.
- Exact integration CI #106 passed every repository gate on `c473746e8dd6c7a42e8116620c4716efe4c6a0f5`.
- Final integration PR #32 landed on main at `10ea1e77eabbbf28001a335d21051c7fff17fa12`.
- Exact-main push CI #107 passed every gate and published the exact-SHA engineering candidate artifact.

## Excluded / remaining production scope
- Real account/subscription/device activation backend, authentication/token verification contract, credentials, and secure token storage
- Production Authenticode PFX/signature evidence and updater publisher private signing key
- Update manifest/download service, anti-downgrade/rollout policy, rollback execution, and native updater qualification
- Runtime command/license enforcement after product policy/backend approval
- Production telemetry endpoint, consent UI, retention/deletion operations

This source-safe trust boundary is complete. Issue #6 remains open and no commercial-readiness claim is made until the real production services, credentials, runtime integration and native evidence exist.
