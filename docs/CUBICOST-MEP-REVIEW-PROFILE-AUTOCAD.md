# Cubicost-style MEP review/profile — AutoCAD

Updated: 2026-08-15 (UTC+7)  
Tracking: #60  
Upstream adapter: #58 / PR #59

## Ownership

Shared MEP recognition, quantity and coordination contracts remain canonical in `QS3D-Platform`. This repository owns only the AutoCAD-native palette/command routing and user-level profile persistence required to operate those shared contracts.

The review/profile lane is stacked on the exact source-ready adapter head `96cfa7b4e4b64b3e07f508034ac79d464731d261`, which pins `QS3D-Platform@e029d4ba0de6ffe80575f7aed96affa1db1b9b33`.

## `QS3DMEPREVIEW`

`QS3DMEPREVIEW` opens a modeless AutoCAD `PaletteSet` containing a WPF review/profile control. The palette provides command actions for:

- `QS3DMEPTAKEOFF`;
- `QS3DMEPCLASH`;
- `QS3DMEPCLASHLOCATE`;
- `QS3DMEPEXACTCLASH`;
- `QS3DMEPZOOMSELECTION`.

The modeless UI does not retain an AutoCAD `Document`, `ObjectId`, `DBObject` or `Solid3d`. Each action resolves `MdiActiveDocument` when clicked and queues the existing native command against that active document. Presentation controls may live for the palette lifetime; native document/database identity may not.

## Recognition profile editor

The WPF grid edits the shared recognition fields:

- rule ID;
- integer priority;
- discipline;
- category;
- Layer / BlockName / combined source scope;
- MEP kind for MEP rules;
- semicolon-separated recognition tokens.

Non-MEP rules must leave MEP Kind blank. MEP rules require a valid shared `MepElementKind`. Saving rebuilds a shared `MepRecognitionProfile`; all normal MEP commands read `MepRecognitionProfileProvider.Current`, so a successful save/reload changes subsequent recognition consistently rather than creating command-specific private profiles.

## User configuration storage

The profile is user configuration, not DWG/project semantic data. It is stored under roaming Windows application data as:

`QS3D/AutoCAD/mep-recognition-profile.xml`

The store is intentionally shared by the supported AutoCAD product versions because the schema and shared recognition contract are host-version neutral. Runtime qualification still has to prove each supported AutoCAD host family independently.

Safety bounds:

- maximum profile file size: 512 KiB;
- maximum rules: 500;
- maximum tokens per rule: 100;
- DTD processing prohibited;
- external XML resolution disabled;
- unknown root/version/children/enum values fail closed;
- invalid or corrupt profile falls back to `MepRecognitionProfiles.CreateDefault()` and surfaces an error;
- save writes a same-directory temporary XML file and then uses `File.Replace` for an existing file or `File.Move` for first creation;
- temporary files are cleaned best-effort after failure.

No profile operation opens the AutoCAD database for write, creates a QS3D semantic project, modifies a sidecar or changes drawing entities.

## Exact source validation

The focused guard `scripts/validate-cubicost-mep-review-profile.ps1` verifies:

- centralized profile consumption in the MEP adapter;
- XML hardening and persistence bounds;
- atomic replace/move semantics;
- the `QS3DMEPREVIEW` DemandLoad trigger in AutoCAD 2021, 2025-2026 and 2027 bundle entries;
- all review actions route to existing MEP commands;
- modeless UI does not retain native document/object fields;
- no CAD write/project-store/threading paths are introduced by this UI/configuration lane.

Normal repository CI must still compile and package AutoCAD 2021 (`net48`), 2025-2026 (`net8.0-windows`) and 2027 (`net10.0-windows`) and run the full architecture/security/setup pipeline on the exact final head.

## PENDING_NATIVE matrix

Source/CI readiness is not licensed AutoCAD runtime evidence. The following remains **PENDING_NATIVE** until tested on exact integrated binaries in real supported AutoCAD hosts:

1. DemandLoad discovery and first invocation of `QS3DMEPREVIEW` on AutoCAD 2021, 2025/2026 and 2027.
2. Palette creation, docking, resize, focus, hide/show and clean close at 100/125/150/200% Windows DPI.
3. Keep the palette open while switching between two DWGs; every action must execute only against the document active at click time.
4. Close the previously active DWG while the palette remains visible; no stale callback may touch the closed database.
5. Run Takeoff, Broad Clash, Locate, Exact Clash and Zoom through palette buttons and compare behavior with direct command invocation.
6. Save a custom profile, reload it, close/restart AutoCAD and prove the same profile is loaded.
7. Prove one saved custom profile is interpreted consistently in AutoCAD 2021, 2025/2026 and 2027 for the shared schema.
8. Corrupt XML, unsupported version, oversized file, >500 rules and >100 tokens/rule must fail closed to built-in defaults without CAD/project mutation.
9. Exercise equal-priority ambiguous rules, unmatched rules, custom MEP rules and non-MEP rules; downstream quantity/clash commands must retain fail-closed behavior.
10. Interrupt or fail profile save and verify the previous valid profile remains recoverable and no temporary/backup residue affects later startup.
11. Verify profile save/reload/default actions do not alter DWG bytes, semantic project state, selection, undo history or drawing modified state.
12. Multi-DWG and process cleanup evidence must record exact QS3D SHA, exact Platform pin, AutoCAD product/version/target framework and sanitized profile/runtime outcomes.

Do not commit Autodesk proprietary binaries, customer drawings, machine-specific private paths or unsanitized native Handle lists as evidence.

## Deferred coordination persistence

The shared `CoordinationIssue` model already exists in Platform. Persisted clash issue status/assignee/comments in AutoCAD remains a separate persistence lane after the canonical shared Platform contract is integrated/published. Do not create a second AutoCAD-only coordination issue domain model merely to bypass that dependency.
