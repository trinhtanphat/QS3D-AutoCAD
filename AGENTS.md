# Agent rules

- `main` is the integration branch unless a task explicitly requires a PR.
- Never add `Autodesk.*` references or types to `src/QS3D.Core`.
- Keep AutoCAD transactions, ObjectIds, Entities, Editors and UI inside `src/QS3D.AutoCAD`.
- Do not claim native AutoCAD runtime PASS from source review or Core CI alone.
- Do not weaken architecture guards, tests or packaging checks to make CI green.
- Before modifying a file on a moving branch, refresh `main` and avoid overwriting unrelated concurrent work.
- Generated QS3D geometry must be tagged with QS3D metadata so BOQ/editing can distinguish it from user-authored entities.
- Startup must remain lightweight; do not create PaletteSet UI from `IExtensionApplication.Initialize()`.
- Every release candidate must preserve AutoCAD 2025–2026 (`net8.0-windows`) and AutoCAD 2027 (`net10.0-windows`) separation.
