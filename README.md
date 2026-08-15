# QS3D AutoCAD

QS3D AutoCAD is the Autodesk AutoCAD host for QS3D structural modelling and quantity workflows.

## Supported host generations

- AutoCAD 2021: legacy .NET Framework 4.8 host payload compiled against Autodesk-owned `AutoCAD.NET` 24.0.0 and loaded from bundle series `R24.0`
- AutoCAD 2025–2026: .NET 8 host payload compiled against Autodesk-owned `AutoCAD.NET` 25.0.1
- AutoCAD 2027: .NET 10 host payload compiled against Autodesk-owned `AutoCAD.NET` 26.0.0
- Autodesk-specific code is isolated from the host-neutral QS3D Core
- deployment uses an AutoCAD `.bundle`
- the release pipeline produces both a portable bundle zip and a self-contained `QS3D-AutoCAD-<version>-Setup.exe`

The AutoCAD 2021 payload exists so installations on the R24.0 host can discover and lazy-load QS3D instead of receiving `Unknown command "QS3D"`. It is built and packaged independently from the modern .NET 8/.NET 10 payloads; adding legacy support does not downgrade the 2025–2027 binaries.

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
- `QS3DASSIGNLEVEL` — bind a structural element to a QS3D Level and move/rebuild it to that elevation
- `QS3DLEVELMOVE` — change a Level elevation and propagate the Z shift to all Level-bound structural elements
- `QS3DBINDGRID` — attach one or two semantic Grid references to a structural element
- `QS3DGRIDSNAP` — rebuild Grid-bound structural geometry and metadata together
- `QS3DREFERENCERENAME` — rename Level/Grid references while preserving semantic IDs and bindings
- `QS3DLEVELSEQUENCE` — sequence Level names by elevation
- `QS3DGRIDSEQUENCE` — resequence a parallel Grid family by spatial order
- `QS3DCLEARREFS` — remove Level/Grid placement references without moving geometry
- `QS3DGRIDARRAY` — create a parallel named Grid series with fixed spacing
- `QS3DREFERENCEDELETE` — delete an unused Level/Grid while refusing deletion when dependents remain
- `QS3DREFERENCES` — list Level/Grid references and dependent counts
- `QS3DCOLUMNJIG`, `QS3DBEAMJIG`, `QS3DSLABJIG`, `QS3DWALLJIG`, `QS3DCURTAINJIG` — transient live-solid authoring previews with dimension/orientation feedback and commit-only persistence
- `QS3DRIBBON` — reconcile/create the QS3D Ribbon through AutoCAD's loaded `Autodesk.Windows` runtime UI types
- `QS3DREFRESH` — refresh the model browser
- `QS3DABOUT` — host/runtime information

The dockable workspace has Tools, Project and Levels & Grids tabs. The project browser lists QS3D-owned entities, synchronizes with AutoCAD pickfirst selection, exposes geometry, quantity and placement-reference properties, and can launch safe editing. Palette controls can switch between Vietnamese and English.

Generated geometry carries typed QS3D XData. Project identity/name is stored in the DWG Named Objects Dictionary, so QS3D state travels with the drawing. Current metadata uses the backward-compatible `QS3D2` schema for Level/Grid references while continuing to read legacy `QS3D1` entities. Solid property or Level-placement changes preserve QS3D semantic IDs while replacing or moving physical geometry, preventing BOQ metadata from diverging from the visible model.

The JIG/Grid-manager implementation is source-complete but remains subject to real-host native acceptance. Hosted builds are not evidence that cursor previews, Ribbon visuals, undo/redo or persistence behave correctly in every supported AutoCAD generation.

### Ribbon boundary

The Ribbon bridge deliberately does **not** compile against `AdWindows.dll` or `Autodesk.Windows`. Hosted CI cannot substitute or mock that native AutoCAD UI dependency. `QS3DRIBBON` resolves the loaded AutoCAD UI assembly/types at runtime, builds an idempotent QS3D tab with Model/References/Review panels, and fails softly so the palette/model commands remain usable if the Ribbon API is unavailable.

A successful hosted compile only proves the bridge source remains host-safe. `ribbon_surface` and `ribbon_visual_qa` remain native acceptance gates; the legacy AutoCAD 2021 lane can be qualified separately, while the default production qualification matrix remains AutoCAD 2025, 2026 and 2027 until release policy is intentionally changed.

## Build and delivery

GitHub `CI` builds and smoke-tests the host-neutral Core, compiles AutoCAD 2021, 2025–2026 and 2027 host payloads using Autodesk-owned packages, validates command/bundle architecture, packages an engineering release candidate, and verifies release provenance/checksums end to end. Autodesk assemblies are compile-time dependencies only and are excluded from QS3D release payloads.

CI also validates the native-acceptance tooling itself and proves that synthetic evidence with `pending` checks is rejected for both the default modern matrix and the separate AutoCAD 2021 lane. Hosted CI never creates a native PASS result.

`./scripts/package.ps1 -Version <version>` creates:

- `artifacts/QS3D-AutoCAD-<version>.zip`
- `artifacts/QS3D-AutoCAD-<version>-Setup.exe`
- `artifacts/RELEASE-PROVENANCE.json`
- `artifacts/SHA256SUMS.txt`

`RELEASE-PROVENANCE.json` records the exact source commit, version, three runtime families, signing state, artifact sizes and SHA-256 hashes. `./scripts/verify-artifacts.ps1 -Version <version>` independently checks that contract.

The setup executable embeds the bundle and installs it to the all-users Autodesk `ApplicationPlugins` directory. Install/upgrade is staged and rollback-safe; Setup refuses install, upgrade or `--uninstall` while AutoCAD is running.

Tag publication is fail-closed: the tagged SHA must be on `main`, must exactly equal repository variable `QS3D_NATIVE_ACCEPTED_SHA`, and real Authenticode PFX/password secrets must be configured. The workflow signs the plugin assemblies and Setup.exe, verifies those signatures/provenance, and only then creates a GitHub prerelease. Manual packaging remains suitable for engineering validation but must not be represented as a signed production release when provenance reports `signed=false`.

The current plugin sends no telemetry or production licensing calls. See `docs/PRIVACY.md` for the current privacy posture and `docs/RELEASE-SECURITY.md` for release/signing gates.

A green source build is not a native runtime qualification. The exact generated bundle still requires acceptance testing in real AutoCAD. The default formal release matrix remains AutoCAD 2025/2026/2027; AutoCAD 2021 uses a separate legacy evidence lane and must also be tested in the actual 2021 host before it can be called native-qualified. See `docs/NATIVE-ACCEPTANCE.md` for the exact evidence workflow.

See `docs/IMPLEMENTATION-PLAN.md` and `docs/BUILD.md` for architecture, build and native acceptance gates.
