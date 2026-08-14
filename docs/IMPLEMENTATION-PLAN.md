# QS3D AutoCAD implementation plan

## Architecture decision

QS3D AutoCAD is a native in-process AutoCAD Managed .NET plugin. The distributable product is a `.bundle` containing the plugin DLLs; the generated Setup.exe installs or removes that bundle.

The codebase is split into two boundaries:

1. `QS3D.Core` — host-neutral geometry, structural element semantics, quantities and project rules. It must not reference Autodesk namespaces.
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

### Implemented — P0 modelling loop

- lazy-loaded `QS3D` dockable palette
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
- portable zip packaging
- self-contained Windows Setup.exe install/upgrade/uninstall flow
- Core smoke tests, host compilation and architecture/package guards in GitHub CI

### Next product layer — P1 production UX

- Ribbon parity with the command palette
- property editing and selection synchronization
- project browser and level/grid manager
- live preview/jig creation tools
- localization (Vietnamese/English)

### Commercial layer — P2

- Authenticode/code signing
- update channel and rollback
- licensing/login/device activation service
- telemetry with explicit opt-in and privacy policy

These commercial items require production identity/licensing/update infrastructure and signing credentials; they must not be represented as complete by source-only placeholders.

## Acceptance gates

A green source CI run proves that Core, both Autodesk API compile targets, bundle metadata and Setup.exe compile successfully. It does **not** prove AutoCAD runtime behavior.

Native acceptance must use a real licensed AutoCAD installation for every supported host generation and verify discovery/autoload, all commands, undo/redo, save/reopen persistence, BOQ semantics, restart behavior, install/upgrade/uninstall and exact-build provenance.

CI and runtime gates must never be weakened merely to obtain a green result.
