# Agent rules

## Mandatory multi-agent integration

Before substantive repository work, read `docs/AGENT-WORK-REGISTRATION.md` and `CI_POLICY.md`, refresh the latest `origin/main`, and inspect every `ACTIVE` / `BLOCKED` claim under `docs/agent-work-claims/`.

- AI agents and chat sessions must not push source, tests, scripts, workflows, installer, packaging or release implementation directly to `main`.
- Permission to dispatch, diagnose, or repair CI does **not** grant a direct-to-`main` implementation exception. CI recovery work uses `recovery/<agent>/<scope>` or `agent/<agent>/<scope>`.
- Publish a visible claim before implementation and implement only the reserved lane on `agent/<agent>/<scope>`.
- Push the final intended task head and open/update its PR before claiming remote completion.
- `.github/workflows/ci.yml` runs automatically for `agent/**`, `recovery/**`, `integration/**`, PRs to `main`, and `main`.
- An agent **must not report a task completed or stop as completed until CI is `success` for the exact current branch/PR head SHA**. Old green runs, another branch, another PR or `main` do not count.
- If CI fails, keep the task active, fix the real defect, push a new SHA and repeat the exact-SHA CI gate. Do not weaken guards/tests/security policy merely to get green.
- For a multi-agent batch, combine participating work on `integration/<batch-id>`, require green CI for the exact integration head, resolve semantic conflicts deliberately, and perform one authorized final PR/landing to `main`.
- Require green CI again for the exact resulting `main` SHA before reporting `ALL MERGED TO MAIN`.
- Never force-push `main`, reset it backwards, or overwrite concurrent work.

A GitHub Issue is a reservation/coordination surface, not a build target; it must reference the branch/PR SHA whose CI proves the task. CI success is a quality/completion gate, not merge authorization.

## AutoCAD product rules

- Never add `Autodesk.*` references or types to `src/QS3D.Core`.
- Keep AutoCAD transactions, ObjectIds, Entities, Editors and UI inside `src/QS3D.AutoCAD`.
- Do not claim native AutoCAD runtime PASS from source review or CI alone.
- Do not weaken architecture guards, tests or packaging checks to make CI green.
- Before modifying a file on a moving branch, refresh `main` and avoid overwriting unrelated concurrent work.
- Generated QS3D geometry must be tagged with QS3D metadata so BOQ/editing can distinguish it from user-authored entities.
- Startup must remain lightweight; do not create PaletteSet UI from `IExtensionApplication.Initialize()`.
- Every release candidate must preserve AutoCAD 2025–2026 (`net8.0-windows`) and AutoCAD 2027 (`net10.0-windows`) separation.
