# Work claim — native candidate artifact handoff

- Status: `ACTIVE`
- Agent: `chatgpt56sol`
- Registered: `2026-08-14T22:36:00+07:00`
- Baseline main SHA: `e2e0f97767bc37f02a54889b603c52f02ba50c34`
- Implementation branch: `agent/chatgpt56sol/native-candidate-artifact-handoff-20260814`
- Integration batch: `integration/native-candidate-artifact-handoff-20260814`

## Reserved scope
Close the source-side native-handoff gap in issue #4: hosted CI already builds and verifies the engineering Setup/ZIP/provenance/checksum candidate, but currently publishes no GitHub Actions artifact. Add a verified engineering artifact handoff bound to the exact tested SHA so licensed AutoCAD machines can consume the same candidate instead of rebuilding/substituting files.

## Expected surfaces
- `.github/workflows/ci.yml`
- release/native acceptance policy guards only as needed
- `docs/RELEASE-SECURITY.md` / native acceptance documentation for artifact semantics

## Invariants
- Upload only after package/provenance verification succeeds.
- Artifact name must include the exact tested source SHA or otherwise expose exact SHA unambiguously.
- Upload the already-verified engineering ZIP, Setup.exe, RELEASE-PROVENANCE.json and SHA256SUMS.txt; do not create a second divergent package.
- CI artifact is explicitly an engineering/native-acceptance candidate, not a signed commercial release and not native PASS.
- No secrets/private keys may be uploaded.

## Excluded scope
- Licensed AutoCAD execution/evidence itself
- Setting `QS3D_NATIVE_ACCEPTED_SHA`
- Production code-signing certificate/PFX
- Licensing/updater/telemetry backend services

## Validation plan
- exact-head CI must still pass every existing gate
- workflow must upload a non-empty artifact only after package verification
- read back the Actions artifact metadata and confirm it belongs to the exact tested SHA

## Completion condition
The artifact-handoff workflow lands through agent -> integration -> main, exact final-main CI is green, a read-back confirms the final run published the expected engineering artifact, and native PASS remains pending real licensed-host evidence.
