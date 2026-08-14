# Work claim — Level/Grid manager lifecycle completion

- Status: `ACTIVE`
- Agent: `chatgpt56sol`
- Registered: `2026-08-14T21:45:00+07:00`
- Baseline main SHA: `5f0f709117dcd76125779c91c00990fff1952120`
- Implementation branch: `agent/chatgpt56sol/reference-manager-lifecycle-20260814`
- Integration batch: `integration/reference-manager-lifecycle-20260814`

## Reserved scope
Complete the remaining source-safe Level/Grid manager operations from issue #5 after the JIG/Grid-snap lane: single Level/Grid rename while preserving semantic GUID dependencies; bulk Level sequencing by elevation; deterministic Grid resequencing for a selected parallel Grid family; UI/bundle/architecture/native acceptance coverage for those manager operations.

## Expected surfaces
- `src/QS3D.Core/Services/*` deterministic reference ordering/validation when host-neutral
- `tests/QS3D.Core.SmokeTests/*` ordering regressions
- `src/QS3D.AutoCAD/Commands/Qs3dReferenceCommands.cs` or a scoped manager command file
- linked visual annotation update helpers
- `src/QS3D.AutoCAD/UI/UiText.cs`, `Qs3dPalette.cs`, `Qs3dRibbon.cs`
- `bundle/QS3D.bundle/PackageContents.xml`
- `scripts/validate-architecture.ps1`
- `native-acceptance/required-checks.json` and manager acceptance docs

## Existing capabilities not to duplicate
- Level listing/dependency counts: `QS3DREFERENCES`
- Level elevation edit + dependent geometry propagation: `QS3DLEVELMOVE`
- Grid spacing/array creation: `QS3DGRIDARRAY`
- dependency-safe Level/Grid deletion: `QS3DREFERENCEDELETE`
- semantic Grid binding and explicit geometry snapping: `QS3DBINDGRID` / `QS3DGRIDSNAP`

## Excluded scope
- Native AutoCAD PASS claims without licensed 2025/2026/2027 evidence
- JIG implementation already completed by the previous claim
- production signing/licensing/updater/telemetry services
- unrelated BOQ/metadata schema redesign

## Validation plan
- deterministic Core smoke regressions for Level/Grid ordering
- exact-head AutoCAD 2025–2026 and 2027 host compilation
- architecture/bundle/native acceptance guards
- package/provenance/signing-plumbing CI gates
- real manager interaction/undo/save-reopen remains native evidence

## Completion condition
Manager source implementation is integrated through agent -> integration -> main, exact final-main CI is green, this claim is terminal, and native-only evidence remains explicitly pending rather than inferred from hosted CI.
