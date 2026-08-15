# Agent rules

## Mandatory AI/chat-session lifecycle

Before substantive work, every AI agent/chat session must read `docs/AI-SESSION-WORKFLOW.md`, `docs/AGENT-WORK-REGISTRATION.md` and `CI_POLICY.md` and follow them as mandatory owner policy.

- Register/claim the lane first through a visible GitHub issue or claim PR; do not create a coordination-only direct `main` commit.
- Publish a concrete plan before implementation.
- Work on `agent/<agent>/<scope>` or `recovery/<agent>/<scope>`; ordinary agents/sessions do not push or merge implementation to `main`.
- `fix bug`, `update code`, `commit push git`, `continue all`, `implement all`, `run CI`, `fix CI`, `loop until success` and equivalent prompts never grant `main` authority.
- Only explicit owner integration authorization may change `main`, such as `merge all to main`, `you are the integration coordinator`, or `allow merge PR #... to main`.
- Push the final intended task head and open/update its PR/handoff before claiming remote completion.
- Run/observe applicable task-scoped CI and continue diagnose -> fix -> push -> fresh CI until all required/applicable lane checks are green. CI ownership is not `main` authority and is not release/publish authority.
- Every session must end with explicit `PROMPT/LANE STATUS: 100% COMPLETE/NOT 100% COMPLETE`, `SESSION CAN BE CLOSED/DELETED: YES/NO`, and separate `MERGED TO MAIN: YES/NO` plus exact SHA/evidence/blockers.
- If not 100% complete and actionable work remains within the session's scope/tools/permissions, continue the loop instead of stopping at a checkpoint.

## Mandatory exact-SHA task CI

`.github/workflows/ci.yml` is the repository-safe task validation workflow. It runs automatically for implementation-relevant changes on `agent/**`, `recovery/**`, `integration/**`, pull requests targeting `main`, and `main`.

- For any task that is not CI-neutral-only, an agent must not report the task complete until CI is `success` for the exact current branch/PR head SHA.
- Old green runs, another branch, another PR, an older integration SHA, or an older `main` SHA do not count.
- CI-neutral-only work is exempt from the full build only when every changed path is in the documented ignore set in `CI_POLICY.md` / `.github/workflows/ci.yml`. Commit prefixes such as `docs:` or `chore:` are not exemptions by themselves.
- Mixed changes and changes to source, tests, projects/build files, dependencies, scripts, workflows, installer, packaging, signing/runtime configuration or release machinery require normal CI.
- If required CI fails, keep the task active, fix the real defect on the agent/recovery branch and repeat on a fresh SHA. Never weaken guards/tests/security/release policy merely to get green.
- A GitHub Issue is a reservation/coordination surface, not a build target. CI evidence belongs to the branch/PR SHA referenced by the issue.

## Mandatory multi-agent integration

Before substantive repository work, refresh the latest `origin/main`, inspect existing `ACTIVE` / `BLOCKED` claim files plus open claim issues/PRs, and avoid overlapping reserved surfaces.

- AI agents and chat sessions must not push source, tests, scripts, workflows, installer, packaging or release implementation directly to `main`.
- Permission to dispatch, diagnose, or repair CI does **not** grant a direct-to-`main` implementation exception. CI recovery work uses `recovery/<agent>/<scope>` or `agent/<agent>/<scope>`, then the normal integration path.
- New claims are issue/PR-first under `docs/AGENT-WORK-REGISTRATION.md`; existing active claim files remain valid coordination history.
- Implement only the reserved lane on a dedicated `agent/<agent>/<scope>` branch.
- For a multi-agent batch, an explicitly authorized coordinator combines participating work on `integration/<batch-id>`, resolves semantic conflicts deliberately, and requires green CI for the exact implementation-relevant integration head before final landing.
- After the authorized final landing, implementation-relevant changes require green CI again for the exact resulting `main` SHA before reporting `ALL MERGED TO MAIN`.
- Never force-push `main`, reset it backwards, or overwrite concurrent work. Refresh `main` immediately before integration and verify the resulting commit/tree is reachable from current `main`.
- A branch, issue, PR, or green CI run by itself is not proof that all required work is merged. See `docs/AGENT-WORK-REGISTRATION.md` for the `ALL MERGED TO MAIN` gate.

## AutoCAD product rules

- Never add `Autodesk.*` references or types to `src/QS3D.Core`.
- Keep AutoCAD transactions, ObjectIds, Entities, Editors and UI inside `src/QS3D.AutoCAD`.
- Do not claim native AutoCAD runtime PASS from source review or repository CI alone.
- Do not weaken architecture guards, tests or packaging checks to make CI green.
- Before modifying a file on a moving branch, refresh `main` and avoid overwriting unrelated concurrent work.
- Generated QS3D geometry must be tagged with QS3D metadata so BOQ/editing can distinguish it from user-authored entities.
- Startup must remain lightweight; do not create PaletteSet UI from `IExtensionApplication.Initialize()`.
- Every release candidate must preserve AutoCAD 2025–2026 (`net8.0-windows`) and AutoCAD 2027 (`net10.0-windows`) separation.
