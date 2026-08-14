# QS3D AutoCAD

QS3D AutoCAD is the Autodesk AutoCAD host for the QS3D structural modelling and quantity workflow.

## Product direction

- AutoCAD 2025–2026: .NET 8 host
- AutoCAD 2027: .NET 10 host
- Autodesk host code is isolated from the host-neutral QS3D core
- Deployment uses an AutoCAD `.bundle` package
- `QS3D Setup` installs the bundle; the DLL remains the actual AutoCAD extension

## Bootstrap scope

The initial implementation provides:

- host-neutral structural primitives and quantity calculations
- AutoCAD plugin entry point and command registration
- Level, Grid, Column, Beam, Slab, Wall, Curtain, Section and BOQ commands
- DWG-backed QS3D project persistence
- QS3D entity metadata/XData
- a dockable command palette
- bundle manifests for AutoCAD 2025–2026 and 2027
- install/uninstall/package PowerShell scripts
- core smoke tests and architecture guards in CI

See `docs/IMPLEMENTATION-PLAN.md` for the architecture and acceptance plan.
