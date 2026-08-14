# QS3D AutoCAD

QS3D AutoCAD is the Autodesk AutoCAD host for QS3D structural modelling and quantity workflows.

## Supported host generations

- AutoCAD 2025–2026: .NET 8 host payload
- AutoCAD 2027: .NET 10 host payload
- Autodesk-specific code is isolated from the host-neutral QS3D Core
- deployment uses an AutoCAD `.bundle`
- the release pipeline produces both a portable bundle zip and a self-contained `QS3D-AutoCAD-<version>-Setup.exe`

## Implemented modelling loop

Run `QS3D` to open the dockable command palette. The current host implements:

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
- `QS3DABOUT` — host/runtime information

Generated geometry carries typed QS3D XData. Project identity/name is stored in the DWG Named Objects Dictionary, so QS3D state travels with the drawing.

## Build and delivery

`CI` builds and smoke-tests the host-neutral Core and verifies command/bundle architecture without requiring Autodesk binaries. Native AutoCAD compilation and packaging is intentionally isolated in `Package native AutoCAD release`, which requires a Windows self-hosted runner with the Autodesk Managed SDK configured.

`./scripts/package.ps1 -Version <version>` creates:

- `artifacts/QS3D-AutoCAD-<version>.zip`
- `artifacts/QS3D-AutoCAD-<version>-Setup.exe`

The setup executable embeds the bundle and installs it to the all-users Autodesk `ApplicationPlugins` directory; `--uninstall` removes it.

See `docs/IMPLEMENTATION-PLAN.md` and `docs/BUILD.md` for architecture, build prerequisites and native acceptance gates.
