# Work claim — live JIG dimensions and orientation

- Status: `ACTIVE`
- Agent: `chatgpt56sol`
- Registered: `2026-08-14T22:14:00+07:00`
- Baseline main SHA: `fab0c517e2a09ec79ed7553d9df7dbdeee373ded`
- Implementation branch: `agent/chatgpt56sol/jig-live-dimensions-20260814`
- Integration batch: `integration/jig-live-dimensions-20260814`

## Reserved scope
Complete the remaining source-safe issue #5 JIG UX gap: live transient dimension/orientation annotation during Column/Beam/Slab/Wall/Curtain cursor movement, with no database persistence and mandatory native visual/cancel evidence.

## Expected surfaces
- `src/QS3D.AutoCAD/UI/Qs3dPointPreviewJig.cs`
- `src/QS3D.AutoCAD/Commands/Qs3dJigCommands.cs`
- architecture/native acceptance guards and `docs/JIG-PREVIEW.md`

## Invariants
- Dimension/orientation graphics are transient only; no transaction, append, XData or project/browser record during sampling.
- Existing solid preview and PromptStatus.OK commit boundary remain unchanged.
- Annotation text must reflect current cursor-derived dimensions/orientation rather than stale final metadata.
- Native AutoCAD 2025/2026/2027 visual behavior remains evidence-only; hosted CI cannot claim PASS.

## Excluded scope
- Native AutoCAD qualification itself
- Level/Grid manager source lane already completed
- commercial signing/licensing/updater/telemetry services

## Validation plan
- both AutoCAD host families compile
- architecture guard requires transient annotation path and forbids persistence in the JIG helper
- native contract adds an explicit live-dimensions/orientation gate
- exact integration and final-main CI remain green

## Completion condition
The source lane is integrated through agent -> integration -> main, exact final-main CI is green, the claim is terminal, and licensed-host visual evidence remains explicitly pending.
