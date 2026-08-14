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

### Native/UI work still requiring AutoCAD runtime qualification

- Ribbon integration. Autodesk's Ribbon/application-menu customization surface requires `AdWindows.dll`, so this should be compiled and qualified against the installed AutoCAD/ObjectARX UI runtime instead of weakening the hosted compile gate.
- full 3D entity jig/live-solid previews beyond AutoCAD's built-in rubber-band point prompts
- richer Level/Grid manager operations (bulk rename/reorder/delete and model-wide placement rules)
- native visual QA for high-DPI/dark/light AutoCAD themes

### Commercial layer requiring production credentials/infrastructure

- Authenticode/code signing
- licensing/login/device activation service
- update service/channel and rollback policy
- telemetry with explicit opt-in and privacy policy

These items are intentionally not represented as complete by source-only placeholders. Signing requires a real certificate/private key, and licensing/update/telemetry require production services and policy decisions.

## Acceptance gates

A green source CI run proves that Core, both Autodesk API compile targets, bundle metadata and Setup.exe compile successfully. It does **not** prove AutoCAD runtime behavior.

Native acceptance must use a real licensed AutoCAD installation for every supported host generation and verify discovery/autoload, all commands, browser selection sync, property editing, undo/redo, save/reopen persistence, BOQ semantics, restart behavior, install/upgrade/uninstall and exact-build provenance.

CI and runtime gates must never be weakened merely to obtain a green result.
