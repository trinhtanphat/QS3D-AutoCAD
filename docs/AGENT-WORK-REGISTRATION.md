# Agent work registration and integration

**Owner rule:** implementation work stays off `main` until one reviewed final integration landing.

This protocol applies to AI agents, chat sessions, CI/recovery sessions, local workers and remote workers.

## Claims and branches

Claims live under `docs/agent-work-claims/` and use one Markdown file per lane. Before implementation, fetch latest `origin/main`, read `AGENTS.md`, `CI_POLICY.md`, this file and every `ACTIVE` / `BLOCKED` claim, choose a non-overlapping scope, make the claim visible, and create the implementation branch normally `agent/<agent>/<scope>`.

For CI repair, use `recovery/<agent>/<scope>`. For multi-agent combination, use `integration/<batch-id>`.

## Mandatory implementation-agent completion gate

Each implementation agent must run relevant local validation, push the final intended branch head, open/update the PR, record the exact head SHA, and observe `.github/workflows/ci.yml`.

The workflow runs automatically for `agent/**`, `recovery/**`, `integration/**`, PRs targeting `main`, and `main`.

An agent **must not report the task completed or stop as completed until the required CI run is `success` for the exact current branch/PR head SHA**. A green run for an older SHA, another branch, another PR or `main` does not satisfy the task.

If CI fails, keep the lane active, diagnose/fix the real defect on the task branch, push a new head SHA and repeat. Native AutoCAD/runtime/signing gates that cannot be proven by repository-safe CI remain explicitly `BLOCKED`/handed off; do not promote CI to native PASS.

A GitHub Issue is coordination only; it has no source tree. The Issue must reference the branch/PR and exact SHA whose CI result proves the task.

## Batch integration

For multi-agent work, the coordinator uses `integration/<batch-id>`. Enumerate exact participating claims/SHAs, integrate every required lane without silently dropping work, resolve semantic/API/test conflicts deliberately, verify no required lane remains only elsewhere, require green CI for the exact integration head, inspect the final diff, and perform one authorized final PR/landing to `main`.

After landing, fetch current `main`, record the exact final SHA, and require green CI for that exact SHA before reporting the batch fully integrated.

## Definition of ALL MERGED TO MAIN

Report `ALL MERGED TO MAIN` only when current `main` contains every required implementation, participating claims are terminal or explicitly excluded/superseded, no required code remains only off-main, the exact current `main` SHA is recorded, and the required CI run for that exact SHA is green.

Branch deletion, PR/Issue UI state and stale CI are not sufficient proof.

## Suggested claim template

```markdown
# Work claim — <scope>

- Status: `ACTIVE`
- Agent: `<stable-agent-id>`
- Baseline main SHA: `<40-char SHA>`
- Implementation branch: `agent/<agent>/<scope>`
- Integration batch: `integration/<batch-id>` or `TBD`
- Exact task head SHA: `<40-char SHA after final push>`
- Required CI: `.github/workflows/ci.yml`
- CI result: `PENDING` until exact-head success

## Reserved scope
<exact lane>

## Validation plan
- <local checks>
- exact-head remote CI success

## Completion condition
<task result plus exact-head CI success, without overstating native evidence>
```
