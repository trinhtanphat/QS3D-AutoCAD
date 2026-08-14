# Work claim — advanced modelling UX source completion

- Status: `COMPLETED`
- Agent: `chatgpt56sol`
- Registered: `2026-08-14T21:09:00+07:00`
- Baseline main SHA: `e1a517cc3bcb22a49c42418e8843115c959e71d2`
- Implementation branch: `agent/chatgpt56sol/advanced-modelling-ux-20260814`
- Integration batch: `integration/advanced-modelling-ux-20260814`
- Integrated main SHA: `1c07c82d7f34181ad075a31a43af84147d7601c6`
- Exact-main CI: run `31810061293` / CI #82 — `SUCCESS`

## Reserved scope
Complete the remaining source-safe portion of issue #5 without claiming native AutoCAD runtime PASS: live DrawJig/EntityJig-style previews for Column/Beam/Slab/Wall/Curtain with cancel-safe persistence boundaries; Grid snapping/reshape semantics tied to existing Grid references; corresponding host-side UI/bundle/architecture/native-acceptance guards and deterministic Core regressions where host-neutral semantics are involved.

## Integrated outcome
- Added transient live 3D JIG authoring for Column, Beam, Slab, Wall and Curtain.
- Kept preview helpers database-free; persistent entity/metadata creation occurs only after `PromptStatus.OK`.
- Added deterministic one-Grid translate, two-Grid projection and Column Grid-intersection semantics in Core, including parallel-Grid rejection.
- Added explicit `QS3DGRIDSNAP` that rebuilds replacement geometry, attaches updated metadata, erases the old entity and commits together.
- Exposed the new workflows through Palette, runtime Ribbon and both bundle runtime entries.
- Added architecture regression guards, Core smoke regressions, native evidence requirements and `docs/JIG-PREVIEW.md`.
- Fixed the exact CI #80 Autodesk host compile blocker through recovery PR #15; exact integration CI #81 and exact-main push CI #82 both passed all repository gates.

## Excluded / remaining native-only scope
- Production signing certificate/PFX procurement
- Licensing backend and activation service
- Updater/network service implementation
- Telemetry transport/consent backend
- AutoCAD 2025/2026/2027 native PASS for `jig_live_solid_preview`, `jig_cancel_safety`, `grid_geometry_snap`, Ribbon/runtime behavior and other native checks
- Unrelated release, installer, BOQ or metadata schema redesign

The source lane is complete. Native runtime gates remain pending until real licensed AutoCAD evidence is recorded; hosted CI is not evidence for those gates.
