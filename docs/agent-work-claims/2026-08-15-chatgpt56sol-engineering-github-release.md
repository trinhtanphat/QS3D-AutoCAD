# Work claim — engineering GitHub Releases handoff

- Status: `ACTIVE`
- Agent: `chatgpt56sol`
- Registered: `2026-08-15T08:37:00+07:00`
- Baseline main SHA: `85e0607bfd99fdf87a668da96bca961ae49fd3c7`
- Implementation branch: `agent/chatgpt56sol/engineering-github-release-20260815`
- Integration batch: `integration/engineering-github-release-20260815`

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

## Validation plan
- CI exact-SHA build/test/architecture/release guards remain green.
- Engineering release consumes the verified CI artifact for the exact `main` SHA instead of rebuilding.
- Provenance must match the exact source SHA, be clean, and remain `signed=false`.
- GitHub prerelease/tag naming must make the test-only status explicit and must not trigger the signed `v*` production workflow.
- Assets must include Setup.exe, bundle ZIP, provenance, and checksums.

## Completion condition
A reviewed integration lands on `main`, exact-main CI succeeds, and the resulting verified candidate is visible on GitHub Releases under a test-only git tag with downloadable Setup.exe/ZIP/provenance/checksums. Production signed-release gates remain unchanged.
