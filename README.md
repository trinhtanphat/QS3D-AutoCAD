# QS3D AutoCAD

QS3D AutoCAD is the Autodesk AutoCAD host for QS3D structural modelling and quantity workflows.

## Supported host generations

- AutoCAD 2025–2026: .NET 8 host payload compiled against Autodesk-owned `AutoCAD.NET` 25.0.1
- AutoCAD 2027: .NET 10 host payload compiled against Autodesk-owned `AutoCAD.NET` 26.0.0
- Autodesk-specific code is isolated from the host-neutral QS3D Core
- deployment uses an AutoCAD `.bundle`
- the release pipeline produces both a portable bundle zip and a self-contained `QS3D-AutoCAD-<version>-Setup.exe`

## Implemented modelling workflow

Run `QS3D` to lazy-load the plugin and open the dockable QS3D workspace. The current host implements:

- `QS3DINIT` — initialize/rename the DWG-backed QS3D project
- `QS3DLEVEL` — level marker
- `QS3DGRID` — grid axis
- `QS3DCOLUMN` — 3D structural column
- `QS3DBEAM` — plan-oriented 3D beam
- `QS3DSLAB` — rectangular 3D slab
- `QS3DWALL` — plan-oriented 3D wall
- `QS3DCURTAIN` — modular curtain panels
- `QS3DSECTION` — section marker
- `QS3DBOQ` — quantity summary from QS3D-tagged entities
- `QS3DEDIT` — edit QS3D properties while rebuilding physical solids when dimensions change
- `QS3DREFRESH` — refresh the model browser
- `QS3DABOUT` — host/runtime information

The dockable workspace has a Tools tab plus a Project tab. The project browser lists QS3D-owned entities, synchronizes with AutoCAD pickfirst selection, exposes geometry and quantity properties, and can launch safe editing. Palette controls can switch between Vietnamese and English.

Generated geometry carries typed QS3D XData. Project identity/name is stored in the DWG Named Objects Dictionary, so QS3D state travels with the drawing. Solid property edits preserve QS3D semantic IDs while replacing geometry, preventing BOQ metadata from diverging from the visible model.

## Build and delivery

GitHub `CI` builds and smoke-tests the host-neutral Core, compiles both AutoCAD host generations using Autodesk-owned packages, validates command/bundle architecture, and compiles the self-contained Setup.exe. Autodesk assemblies are compile-time dependencies only and are excluded from QS3D release payloads.

`./scripts/package.ps1 -Version <version>` creates:

- `artifacts/QS3D-AutoCAD-<version>.zip`
- `artifacts/QS3D-AutoCAD-<version>-Setup.exe`
- `artifacts/SHA256SUMS.txt`

The setup executable embeds the bundle and installs it to the all-users Autodesk `ApplicationPlugins` directory; `--uninstall` removes it. Tag builds use the release workflow to publish prerelease assets once native acceptance is complete.

A green source build is not a native runtime qualification. The exact generated bundle still requires acceptance testing in real AutoCAD 2025, 2026 and 2027 for autoload, modelling, editing, undo/redo, save/reopen persistence and installer behavior.

See `docs/IMPLEMENTATION-PLAN.md` and `docs/BUILD.md` for architecture, build and native acceptance gates.
