# Level/Grid manager lifecycle contract

Issue #5 reference-manager operations are explicit commands layered on the existing semantic Level/Grid identities.

## Commands

- `QS3DREFERENCERENAME` — rename one Level or Grid. The semantic GUID is unchanged, so Level/Grid dependent bindings remain valid. Duplicate names inside the same reference kind are rejected case-insensitively.
- `QS3DLEVELSEQUENCE` — order all Levels by elevation (ascending or descending) and bulk-rename them with a chosen prefix/start/digit width. Geometry, elevation, semantic GUIDs and dependent bindings do not change.
- `QS3DGRIDSEQUENCE` — select a seed Grid, find only its parallel Grid family, order that family by spatial offset, and rename it using numeric or alphabetic sequencing. Orthogonal/non-parallel Grids are excluded rather than mixed into the sequence. Generated names are checked against Grids outside the selected family before any transaction is committed.

Existing manager lifecycle commands remain authoritative for other operations:

- `QS3DREFERENCES` — list Levels/Grids and dependency counts.
- `QS3DLEVELMOVE` — elevation edit plus dependent geometry/metadata propagation.
- `QS3DGRIDARRAY` — spacing-based Grid creation.
- `QS3DREFERENCEDELETE` — dependency-safe deletion.
- `QS3DBINDGRID` / `QS3DGRIDSNAP` — semantic binding and explicit geometry snap.

## Persistence rules

Rename/resequence changes reference names only. It must not move, replace, erase or append the Level/Grid geometry. The existing QS3D XData is rewritten with the same semantic `Id`, and linked `DBText` annotation is updated in the same transaction. Dependents continue to reference the same GUID.

## Native acceptance

For each licensed AutoCAD 2025, 2026 and 2027 evidence session:

1. create at least three Levels at distinct elevations, two orthogonal Grid families, and structural dependents bound to both Level and Grid references;
2. record each reference GUID and the dependent `LevelId` / `StartGridId` / `EndGridId` values;
3. run `QS3DREFERENCERENAME` on one Level and one Grid; verify linked annotations update, geometry does not move, GUIDs and dependent bindings are unchanged, and a duplicate same-kind name is rejected without partial writes;
4. run `QS3DLEVELSEQUENCE` ascending and verify names follow elevation order while elevation/dependencies remain unchanged; undo and redo the operation;
5. run `QS3DGRIDSEQUENCE` using a seed in one family; verify only parallel Grids are renamed in spatial order, the orthogonal family is untouched, numeric/alphabetic sequencing works, and an outside-family name collision is rejected transactionally;
6. save, close and reopen the DWG; refresh the QS3D browser and run `QS3DREFERENCES`; names, GUIDs, annotations and dependent references must persist;
7. run `QS3DBOQ` before/after manager-only renames and confirm quantities are unchanged.

Record this as native gate `reference_manager_lifecycle`. Hosted Core/host compilation proves deterministic ordering and API compatibility only; it is not native interaction/undo/persistence evidence.
