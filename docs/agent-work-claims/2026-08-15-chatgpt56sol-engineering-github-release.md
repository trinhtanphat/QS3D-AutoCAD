# Work claim — engineering GitHub Releases handoff

- Status: `COMPLETED`
- Agent: `chatgpt56sol`
- Registered: `2026-08-15T08:37:00+07:00`
- Baseline main SHA: `85e0607bfd99fdf87a668da96bca961ae49fd3c7`
- Implementation branch: `agent/chatgpt56sol/engineering-github-release-20260815`
- Integration batch: `integration/engineering-github-release-20260815`
- Integrated main SHA: `0aaee37c6126364e8b3b10a88371cd1a95c6d6b5`
- Integration PR: `#38`
- Exact-main CI: run `31857250966` / CI `#124` — `SUCCESS`
- Engineering release workflow: run `31857352562` — `SUCCESS`
- Git tag: `test-v0.1.0-ci.124`
- GitHub prerelease: `QS3D AutoCAD Test v0.1.0 — CI #124 (0aaee37c)`

## Reserved scope
Publish the already CI-verified unsigned engineering/native candidate to the repository GitHub Releases page with an explicit test/engineering tag, without weakening the existing signed production-release gate or claiming native AutoCAD PASS.

## Expected surfaces
- `.github/workflows/engineering-release.yml`
- `docs/NATIVE-CANDIDATE-HANDOFF.md`
- release-policy/source guards only if needed to lock the engineering/publication boundary

## Excluded scope
- `.github/workflows/release.yml` signed production gate semantics
- `QS3D_NATIVE_ACCEPTED_SHA`
- production Authenticode certificate/secrets
- native AutoCAD qualification evidence
- modelling/Core/host feature implementation

## Validation completed
- PR #38 CI #123 passed every repository build/test/architecture/release/native-contract guard before integration.
- Exact-main CI #124 passed on `0aaee37c6126364e8b3b10a88371cd1a95c6d6b5` and uploaded the verified native candidate.
- Engineering release workflow run `31857352562` downloaded that exact CI artifact, re-verified provenance/checksums, and published successfully.
- Git tag `test-v0.1.0-ci.124` resolves to exact source SHA `0aaee37c6126364e8b3b10a88371cd1a95c6d6b5`.
- GitHub prerelease contains `QS3D-AutoCAD-0.0.0-ci-Setup.exe`, `QS3D-AutoCAD-0.0.0-ci.zip`, `RELEASE-PROVENANCE.json`, and `SHA256SUMS.txt`.
- The prerelease remains explicitly unsigned/test-only and does not claim native PASS; signed production `v*` publication remains gated separately.

## Completion condition
Satisfied. The exact-main verified engineering candidate is now visible on GitHub Releases under a test-only git tag with downloadable Setup.exe/ZIP/provenance/checksums, while the signed production release gate remains unchanged.
