# Live modelling preview and Grid snap contract

This document defines the source/native boundary for issue #5 advanced modelling UX.

## Live preview commands

- `QS3DCOLUMNJIG`
- `QS3DBEAMJIG`
- `QS3DSLABJIG`
- `QS3DWALLJIG`
- `QS3DCURTAINJIG`

`Qs3dPointPreviewJig` is a transient drawing helper. It samples cursor points through `Editor.Drag`/`DrawJig`, creates short-lived preview `Entity` objects for a frame, invokes their `WorldDraw`, and disposes them. The helper must not start a transaction, append an entity, attach QS3D metadata/XData, or use `Geometry.Draw` with a short-lived drawable.

The command layer owns persistence. A command may create final DWG geometry only after `Editor.Drag` returns `PromptStatus.OK`. Cancel, ESC or any non-OK drag result returns before transaction/append/metadata work.

## Native JIG acceptance

For each AutoCAD 2025, 2026 and 2027 native evidence session:

1. start from a clean drawing and record current QS3D browser row count and `QS3DBOQ` totals;
2. run each of the five JIG commands with representative positive dimensions;
3. move the cursor without clicking and confirm the expected solid/panels visibly follow cursor sampling;
4. press ESC and confirm no new persistent DWG object is left by the command;
5. refresh the QS3D browser and rerun `QS3DBOQ`; row/count/quantity totals must match the pre-command values;
6. repeat the command and accept placement; exactly the intended QS3D geometry/metadata must be committed and browser/BOQ must agree with visible geometry.

Record these observations under native gates `jig_live_solid_preview` and `jig_cancel_safety`. Hosted CI/source review cannot set either gate to PASS.

## Grid geometry snap

`QS3DBINDGRID` remains a semantic reference command. It stores Grid GUIDs without silently moving geometry.

`QS3DGRIDSNAP` is the explicit geometry operation:

- one Grid: project the element Start anchor to the infinite Grid line in plan and translate the whole structural element by the same XY delta, preserving shape, size, Z and Level binding;
- two Grids for Beam/Wall/Slab/Curtain: project Start to the first Grid and End to the second Grid, then rebuild the QS3D solid from the resulting metadata;
- two Grids for Column: place the Column at the intersection of the two infinite Grid lines while preserving Z/Level; parallel Grids are rejected rather than fabricating an intersection.

The AutoCAD host rebuilds replacement geometry, attaches the updated metadata, erases the old entity and commits in one transaction so the visible model and BOQ semantics cannot intentionally diverge.

## Native Grid-snap acceptance

For AutoCAD 2025, 2026 and 2027:

1. create representative structural elements and at least two QS3D Grids;
2. bind a structural element with `QS3DBINDGRID`, verify binding alone does not move geometry, then run `QS3DGRIDSNAP`;
3. confirm one-Grid snapping translates the element without changing its plan length/dimensions;
4. confirm two-Grid snapping reshapes the supported element endpoints onto the selected Grid lines;
5. confirm a Column bound to two intersecting Grids lands at their intersection and retains its Level/Z placement;
6. confirm a Column bound to two parallel Grids is rejected without changing geometry or metadata;
7. run `QS3DBOQ`, save/reopen and refresh the browser; visible geometry, stored references and quantities must remain aligned.

Record this under `grid_geometry_snap`. Hosted CI may validate deterministic Core mathematics and host compilation, but cannot claim the native gate PASS.
