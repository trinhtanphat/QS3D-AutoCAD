# Cubicost-style MEP parity — AutoCAD adapter

Updated: 2026-08-15 (UTC+7)  
Tracking: #58

## Ownership

The canonical host-neutral Cubicost/QS3D contracts live in `QS3D-Platform`. This AutoCAD repository owns only Autodesk-native extraction, selection, view and exact-solid runtime behavior.

This lane is pinned to `QS3D-Platform@e029d4ba0de6ffe80575f7aed96affa1db1b9b33`, the exact head of Platform PR #15 that passed Platform CI #120 / run `31870648987`.

`Autodesk.*` types must never be introduced into `QS3D.Core` or Platform. Shared BQ/cost/tender/progress/4D-5D domain logic must not be copied into the AutoCAD host.

## Commands

- `QS3DMEPTAKEOFF` — current implied selection -> shared recognition -> exact available count/curve-length/area/Solid3d-volume metrics -> shared deterministic MEP aggregation.
- `QS3DMEPCLASH` — current implied selection -> shared recognition -> native read-only geometric extents -> shared hard/clearance AABB clash math.
- `QS3DMEPCLASHLOCATE` — bounded clash review followed by live Handle re-resolution; implied selection changes only when both pair members resolve live.
- `QS3DMEPEXACTCLASH` — recognized native `Solid3d` only; extents are conservative broad phase and `Solid3d.CheckInterference` is the exact hard-clash predicate.
- `QS3DMEPZOOMSELECTION` — read-only entity extents are fitted into the current AutoCAD view; it changes view state only.

## Unit policy

Shared MEP contracts use meters, square meters and cubic meters. The adapter converts from AutoCAD `Database.Insunits` before invoking shared quantity/clash services. `Unitless` or unsupported drawing units fail closed. Bounding-box diagonals are never used as quantity length.

## Safety boundary

The MEP adapter opens CAD entities only `ForRead`. It does not append, erase, transform, clone or Boolean-modify entities. Exact clash keeps native `Solid3d` references inside the active database transaction. Unknown or equal-priority ambiguous recognition is skipped rather than guessed.

Locate and Zoom are deliberate editor/view-state operations. Locate preserves the existing PICKFIRST/implied selection if either stored Handle is stale. Zoom does not write the DWG database.

Interactive limits protect the host: clash locate shows at most 200 pairs; exact clash accepts at most 500 recognized solids and 100,000 broad-phase candidate pairs per invocation.

## Qualification boundary

Repository CI must compile all supported hosts: AutoCAD 2021 (`net48`), AutoCAD 2025-2026 (`net8.0-windows`) and AutoCAD 2027 (`net10.0-windows`), verify the exact Platform pin, run source guards and produce the engineering package.

That evidence is source/build/package evidence only. It does **not** prove native Autodesk runtime behavior.

### LOCAL_ONLY / PENDING_NATIVE matrix

On each supported licensed AutoCAD family, using disposable drawings:

1. Millimeter and Meter takeoff with known curve length, closed-area and Solid3d-volume controls; compare equivalent physical geometry across unit systems.
2. Unitless drawing refusal with no selection/database mutation.
3. default recognition, custom layer/block naming controls, unmatched and intentionally ambiguous controls.
4. broad hard clash, clearance clash and no-clash controls; verify pair ordering and MEP-participant filtering.
5. Locate success changes PICKFIRST to exactly two objects; stale left/right Handle cases preserve the previous selection atomically.
6. exact `Solid3d` interference control, extents-overlap false positive, non-solid skip and dense-selection limit refusal.
7. Zoom from top/isometric/rotated view, narrow/wide aspect ratios and stale/invalid-extents selections.
8. two-DWG isolation: commands must act only on the active document and must never retain ObjectId/DBObject across document switches.
9. NETLOAD/bundle DemandLoad command discovery for all five commands, save/reopen and clean process exit.

Native evidence must record exact QS3D SHA, exact Platform pin, AutoCAD product/version, target framework, plugin hash and sanitized PASS/FAIL results. Do not commit proprietary Autodesk binaries, customer drawings, raw private paths or unsanitized Handle lists.

## Follow-up lanes

A modeless MEP review palette/profile editor and persisted shared `CoordinationIssue` storage are separate AutoCAD-native UI/persistence lanes. They should consume Platform contracts after the shared Platform landing rather than create competing domain models here.
