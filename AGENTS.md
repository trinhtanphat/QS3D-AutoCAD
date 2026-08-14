# Agent rules

## Mandatory multi-agent integration

Before substantive repository work, read `docs/AGENT-WORK-REGISTRATION.md` and `CI_POLICY.md`, refresh the latest `origin/main`, and inspect every `ACTIVE` / `BLOCKED` claim under `docs/agent-work-claims/`.

- AI agents and chat sessions must not push source, tests, scripts, workflows, installer, packaging or release implementation directly to `main`.
- Permission to dispatch, diagnose, or repair CI does **not** grant a direct-to-`main` implementation exception. CI recovery work uses `recovery/<agent>/<scope>` or `agent/<agent>/<scope>`, then the normal integration path.
- Publish a visible claim before implementation. Prefer a tiny `claim/<agent>/<scope>` PR to `main`; a claim-only Markdown landing is coordination, not implementation.
- Implement only the reserved lane on a dedicated `agent/<agent>/<scope>` branch.
- For a multi-agent batch, combine participating work on `integration/<batch-id>`, resolve semantic conflicts deliberately, run combined validation, and perform one final PR/landing to `main`.
- Never force-push `main`, reset it backwards, or overwrite concurrent work. Refresh `main` immediately before integration and verify the resulting commit/tree is reachable from current `main`.
- A branch, issue, PR, or green CI run by itself is not proof that all required work is merged. See `docs/AGENT-WORK-REGISTRATION.md` for the `ALL MERGED TO MAIN` gate.

## AutoCAD product rules

- Never add `Autodesk.*` references or types to `src/QS3D.Core`.
- Keep AutoCAD transactions, ObjectIds, Entities, Editors and UI inside `src/QS3D.AutoCAD`.
- Do not claim native AutoCAD runtime PASS from source review or Core CI alone.
- Do not weaken architecture guards, tests or packaging checks to make CI green.
- Before modifying a file on a moving branch, refresh `main` and avoid overwriting unrelated concurrent work.
- Generated QS3D geometry must be tagged with QS3D metadata so BOQ/editing can distinguish it from user-authored entities.
- Startup must remain lightweight; do not create PaletteSet UI from `IExtensionApplication.Initialize()`.
- Every release candidate must preserve AutoCAD 2025–2026 (`net8.0-windows`) and AutoCAD 2027 (`net10.0-windows`) separation.
