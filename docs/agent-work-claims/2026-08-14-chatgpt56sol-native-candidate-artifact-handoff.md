# Work claim — native candidate artifact handoff

- Status: `COMPLETED`
- Agent: `chatgpt56sol`
- Registered: `2026-08-14T22:36:00+07:00`
- Baseline main SHA: `e2e0f97767bc37f02a54889b603c52f02ba50c34`
- Implementation branch: `agent/chatgpt56sol/native-candidate-artifact-handoff-20260814`
- Integration batch: `integration/native-candidate-artifact-handoff-20260814`
- Integrated main SHA: `3d902ccfba909cc7fd0d80b71b81c5ccee96ebb9`
- Exact-main CI: run `31816260353` / CI #101 — `SUCCESS`
- Engineering artifact id: `9225168284`
- Engineering artifact name: `QS3D-AutoCAD-native-candidate-3d902ccfba909cc7fd0d80b71b81c5ccee96ebb9`
- Engineering artifact digest: `sha256:ec6ce17a3fc82dc4e50fdba7ab7a4ec62c6a158ae5d93c7ff1fe9eb6b6dd8ca6`

## Reserved scope
Close the source-side native-handoff gap in issue #4: hosted CI already builds and verifies the engineering Setup/ZIP/provenance/checksum candidate, but previously published no GitHub Actions artifact. Add a verified engineering artifact handoff bound to the exact tested SHA so licensed AutoCAD machines can consume the same candidate instead of rebuilding/substituting files.

## Integrated outcome
- Main push CI now uploads the already-verified `0.0.0-ci` candidate with current official `actions/upload-artifact@v7` after package/provenance verification and before synthetic native-evidence work.
- The upload is push-only, fails if expected files are missing, keeps the artifact for 14 days and reports artifact id/digest/url.
- Artifact name includes the exact tested main SHA.
- Upload is limited to the verified ZIP, Setup.exe, `RELEASE-PROVENANCE.json` and `SHA256SUMS.txt`; no secrets/private keys or later synthetic evidence are included.
- Release-policy guards lock the upload action/version, exact file list, ordering and engineering-only/not-native-PASS wording.
- Added `docs/NATIVE-CANDIDATE-HANDOFF.md` with exact verification/install/evidence-chain procedure.
- Exact integration CI #100 passed every repository gate with the push-only upload steps intentionally skipped on the PR event.
- Final integration PR #28 landed on main at `3d902ccfba909cc7fd0d80b71b81c5ccee96ebb9`.
- Exact-main CI #101 passed every gate and both upload/report steps executed successfully.
- Actions read-back returned one non-empty 67,713,027-byte artifact tied to `head_sha=3d902ccfba909cc7fd0d80b71b81c5ccee96ebb9`, expiring 2026-08-28.
- Download inspection confirmed exactly four files. `RELEASE-PROVENANCE.json` reports version `0.0.0-ci`, `sourceCommit=3d902ccfba909cc7fd0d80b71b81c5ccee96ebb9`, `sourceDirty=false`, `signed=false`; SHA256SUMS matches the engineering ZIP, Setup.exe and provenance file.

## Excluded / remaining native-only scope
- Licensed AutoCAD execution/evidence itself
- Setting `QS3D_NATIVE_ACCEPTED_SHA`
- Production code-signing certificate/PFX
- Licensing/updater/telemetry backend services

The source-side artifact handoff is complete. The published artifact is an engineering/native-acceptance candidate only; it is not a signed commercial release and is not native PASS.
