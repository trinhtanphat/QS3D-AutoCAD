# QS3D AutoCAD implementation plan

## Architecture decision

QS3D AutoCAD is a native in-process AutoCAD Managed .NET plugin. The distributable product is a `.bundle` containing the plugin DLLs. An installer script or future setup executable only deploys that bundle.

The codebase is split into two boundaries:

1. `QS3D.Core` — host-neutral geometry, structural element semantics, quantities and project rules. It must not reference Autodesk namespaces.
2. `QS3D.AutoCAD` — Autodesk document/database transactions, commands, entity creation, DWG persistence and UI.

This repository starts with a local Core so the AutoCAD product can move independently. Once the AutoCAD contracts stabilize, Core should be promoted to a shared versioned package used by AutoCAD and BricsCAD rather than copied between hosts.

## Runtime matrix

| AutoCAD | Host TFM | SDK input |
|---|---|---|
| 2025 | `net8.0-windows` | AutoCAD 2025/2026 Managed SDK |
| 2026 | `net8.0-windows` | AutoCAD 2026 Managed SDK |
| 2027 | `net10.0-windows` | AutoCAD 2027 Managed SDK |

The build deliberately produces separate host binaries for the .NET 8 and .NET 10 generations.

## Feature slices

### P0 — working modelling loop

- `QS3D` palette
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

Generated entities carry QS3D XData so BOQ and future editing commands can distinguish QS3D-owned geometry from arbitrary DWG geometry.

### P1 — production UX

- Ribbon parity with the command palette
- property editing and selection synchronization
- project browser and level/grid manager
- live preview/jig creation tools
- localization (Vietnamese/English)

### P2 — commercial delivery

- signed installer executable
- code signing for plugin assemblies
- update channel and rollback
- licensing/login/device activation
- telemetry with explicit opt-in and privacy policy

## Acceptance gates

A change is not considered runtime-qualified only because Core CI is green. Native acceptance must use a real licensed AutoCAD installation for each supported runtime family and verify startup, command execution, undo/redo, save/reopen persistence and unload/restart behavior.

CI must always enforce the host boundary and must never replace Autodesk runtime evidence with mocks or lower a gate merely to obtain a green build.
