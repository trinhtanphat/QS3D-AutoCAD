# Work claim — advanced modelling UX source completion

- Status: `ACTIVE`
- Agent: `chatgpt56sol`
- Registered: `2026-08-14T21:09:00+07:00`
- Baseline main SHA: `e1a517cc3bcb22a49c42418e8843115c959e71d2`
- Implementation branch: `agent/chatgpt56sol/advanced-modelling-ux-20260814`
- Integration batch: `integration/advanced-modelling-ux-20260814`

## Reserved scope
Complete the remaining source-safe portion of issue #5 without claiming native AutoCAD runtime PASS: live DrawJig/EntityJig-style previews for Column/Beam/Slab/Wall/Curtain with cancel-safe persistence boundaries; Grid snapping/reshape semantics tied to existing Grid references; corresponding host-side UI/bundle/architecture/native-acceptance guards and deterministic Core regressions where host-neutral semantics are involved.

## Expected surfaces
- `src/QS3D.AutoCAD/Commands/*` advanced modelling / placement commands
- `src/QS3D.AutoCAD/UI/*` palette/Ribbon/jig helpers
- `src/QS3D.AutoCAD/Infrastructure/*` geometry helpers only as needed
- `src/QS3D.Core/*` Grid placement semantics only when host-neutral
- `tests/QS3D.Core.SmokeTests/*` deterministic placement regressions
- `bundle/QS3D.bundle/PackageContents.xml`
- `scripts/validate-architecture.ps1`
- `native-acceptance/*` and native acceptance documentation for new runtime-only gates

## Excluded scope
- Production signing certificate/PFX procurement
- Licensing backend and activation service
- Updater/network service implementation
- Telemetry transport/consent backend
- Claiming AutoCAD 2025/2026/2027 native PASS without real licensed-host evidence
- Unrelated release, installer, BOQ or metadata schema redesign

## Validation plan
- Exact-HEAD Core build and smoke tests on .NET 8/.NET 10
- Exact-HEAD AutoCAD 2025–2026 and 2027 host compilation
- Architecture/bundle/native-acceptance guards
- Package/provenance and signing-plumbing CI gates already required by the repository
- Native runtime behavior remains pending until licensed AutoCAD evidence is recorded

## Completion condition
Source-safe issue #5 implementation is integrated through the branch/integration workflow, current exact `main` CI is green, the claim is marked `COMPLETED`, and remaining native-only evidence is explicitly documented rather than inferred from hosted CI.
