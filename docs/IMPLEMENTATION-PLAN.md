# QS3D AutoCAD implementation plan

## Architecture decision

QS3D AutoCAD is a native in-process AutoCAD Managed .NET plugin. The distributable product is a `.bundle` containing the plugin DLLs; the generated Setup.exe installs or removes that bundle.

The codebase is split into two boundaries:

1. `QS3D.Core` — host-neutral geometry, structural element semantics, quantities, placement references and project rules. It must not reference Autodesk namespaces.
2. `QS3D.AutoCAD` — Autodesk document/database transactions, commands, entity creation, DWG persistence and UI.

This repository starts with a local Core so the AutoCAD product can move independently. Once cross-host contracts stabilize, Core should be promoted to a shared versioned package used by AutoCAD and BricsCAD rather than copied between hosts.

## Runtime/build matrix

| AutoCAD | Host TFM | Compile API |
|---|---|---|
| 2025 | `net8.0-windows` | Autodesk-owned `AutoCAD.NET` 25.0.1 |
| 2026 | `net8.0-windows` | same 2025-compatible payload; native acceptance required on 2026 |
| 2027 | `net10.0-windows` | Autodesk-owned `AutoCAD.NET` 26.0.0 |

Autodesk runtime assemblies are compile-time dependencies only and are excluded from the QS3D payload; AutoCAD supplies them at runtime.

## Delivery status

### Implemented — P0 modelling and delivery loop

- lazy-loaded `QS3D` dockable workspace
- `QS3DINIT`
- `QS3DLEVEL`
- `QS3DGRID`
- `QS3DCOLUMN`
- `QS3DBEAM`
- `QS3DSLAB`
- `QS3DWALL`
- `QS3DCURTAIN`
- `QS3DSECTION`
- `QS3DBOQ`
- typed QS3D XData ownership metadata
- DWG Named Objects Dictionary project persistence
- runtime-specific `.bundle` entries
- portable zip packaging and SHA-256 checksums
- self-contained Windows Setup.exe install/upgrade/uninstall flow
- tag-driven prerelease workflow
- Core smoke tests, both AutoCAD host compile gates, architecture/package guards and Setup.exe compilation in GitHub CI

### Implemented — P1 production UX foundation

- Tools + Project browser workspace
- project browser for QS3D-owned DWG entities
- AutoCAD pickfirst selection synchronization with the browser/property inspector
- geometry and quantity property inspector
- `QS3DEDIT` property editing
- dimension edits rebuild physical solids while preserving QS3D semantic IDs
- Level/Grid/Section name edits synchronize their visible annotation text
- browser auto-refresh after QS3D commands
- Vietnamese/English palette controls

### Implemented source-side — P1.5 Level/Grid dependency manager

- host-neutral `LevelId`, `StartGridId` and `EndGridId` placement references
- backward-compatible `QS3D2` entity metadata while retaining reads for legacy `QS3D1` entities
- linked Level/Grid/Section annotation identity rather than relying only on layer/position matching
- Level assignment that moves/rebuilds physical geometry to the referenced elevation
- Level elevation changes that propagate to Level-bound structural geometry and metadata
- semantic one/two-Grid binding with dependency tracking
- explicit clear-reference workflow so users can unbind before deleting references
- dependency-safe Level/Grid deletion
- fixed-spacing parallel Grid-array creation
- reference/dependent listing and placement IDs in the property inspector
- localized Levels & Grids palette tab
- Core placement regression coverage and command/bundle guards

Grid references in this slice are semantic references. They intentionally do not auto-snap or reshape element XY geometry until native interactive Grid-placement behavior has been designed and qualified.

### Implemented source-side — P2 release hardening foundation

- transactional Setup install/upgrade with staged bundle validation and rollback of the previous install on replacement failure
- Setup refuses install/upgrade/uninstall while AutoCAD is running
- package script refuses dirty tracked source by default and records the exact git SHA
- `RELEASE-PROVENANCE.json` with version, exact source SHA, dirty state, runtime matrix, signing state, artifact sizes and SHA-256 hashes
- independent `verify-artifacts.ps1` validation against provenance and `SHA256SUMS.txt`
- Authenticode signing helper for staged plugin assemblies and Setup.exe when a real PFX/password is supplied
- release tag gate requires exact `QS3D_NATIVE_ACCEPTED_SHA`, main ancestry, real signing secrets and signed provenance before GitHub Release creation
- CI packages and verifies an engineering release contract rather than only compiling Setup.exe
- privacy posture documents current no-telemetry/no-production-license-network behavior and future opt-in/minimization constraints

The signing hooks are implemented but cannot be called production-qualified until a real certificate is configured and a release run proves the produced binaries verify successfully. Artifact verification is not a complete secure update service.

### Implemented source-side — native acceptance evidence and Ribbon bridge

- versioned native evidence schema and a fixed required-check contract for AutoCAD 2025, 2026 and 2027
- evidence sessions bind the exact source SHA, candidate version, release artifact hashes, actual `acad.exe` product/file version and operator/machine identity
- all native checks start `pending`; result tooling requires explicit pass/fail/blocked records with notes
- runtime identity requires CLR major 8 for AutoCAD 2025/2026 and CLR major 10 for AutoCAD 2027
- final validation requires exactly one passing session per supported AutoCAD generation and every required check explicitly passing
- validator produces review artifacts only and never changes GitHub variables, creates tags or publishes releases
- hosted CI parses/guards the acceptance tooling and proves three synthetic `pending` sessions remain rejected
- `QS3DRIBBON` provides a fail-soft runtime Ribbon bridge that resolves the real loaded `Autodesk.Windows`/`AdWindows` types only inside AutoCAD
- the Ribbon bridge has no compile-time `AdWindows.dll` reference and no `using Autodesk.Windows`; architecture guards prevent those dependencies from being added to hosted builds
- Ribbon reconciliation is idempotent by QS3D tab identity and keeps palette/model commands available when the native Ribbon surface cannot be created

This source implementation does **not** make Ribbon native-qualified. The actual runtime type/property/collection behavior, button dispatch, workspace compatibility, high-DPI behavior and light/dark visual quality remain mandatory `ribbon_surface` and `ribbon_visual_qa` native evidence gates for each supported host.

### Native/UI work still requiring AutoCAD runtime qualification

- execute the exact candidate in licensed AutoCAD 2025, 2026 and 2027 and complete the evidence sessions in `docs/NATIVE-ACCEPTANCE.md`
- verify the runtime Ribbon bridge against the installed AutoCAD/ObjectARX UI runtime and record the mandatory Ribbon surface/visual checks
- full 3D entity jig/live-solid previews beyond AutoCAD's built-in rubber-band point prompts
- native interactive Grid snapping/reshaping from Grid bindings
- additional bulk Level/Grid operations such as multi-select rename/resequence and visual drag/reorder where they add model semantics
- native visual QA for high-DPI/dark/light AutoCAD themes

### Commercial infrastructure still requiring real credentials/services

- real Authenticode certificate/private-key configuration and signed release evidence
- licensing/login/device activation backend and client contract
- authenticated/signed update service/channel with compatibility policy and rollback
- any future telemetry endpoint, consent UI and privacy/retention operations

These items are intentionally not represented as complete by source-only placeholders. Do not add always-allow licensing, fake signatures, embedded production secrets, or network telemetry without the corresponding production service/policy.

## Acceptance gates

A green source CI run proves that Core, both Autodesk API compile targets, bundle metadata, Setup.exe, engineering packaging, provenance/checksums, Ribbon bridge source boundaries and native-evidence tooling succeed. It does **not** prove AutoCAD runtime behavior, Ribbon visual behavior or production signing.

Native acceptance must use a real licensed AutoCAD installation for every supported host generation and verify discovery/autoload, all commands, browser selection sync, property editing, Level/Grid assign/move/bind/clear/delete operations, Ribbon surface/button behavior, undo/redo, save/reopen persistence, BOQ semantics, restart behavior, install/upgrade/uninstall and exact-build provenance.

A production tag additionally requires the exact native-accepted SHA and real Authenticode credentials; the release workflow must remain fail-closed if either is absent or stale.

CI and runtime gates must never be weakened merely to obtain a green result.
