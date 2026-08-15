# Agent work registration and integration

**Owner rule:** implementation work stays off `main` until one reviewed final integration landing.

This protocol applies to AI agents, chat sessions, CI/recovery sessions, local workers and remote workers. `CI_POLICY.md` is authoritative for CI behavior, including the CI-neutral-only exemption.

## Claims and branches

Claims live under `docs/agent-work-claims/` and use one Markdown file per lane. Before implementation, fetch latest `origin/main`, read `AGENTS.md`, `CI_POLICY.md`, this file and every `ACTIVE` / `BLOCKED` claim, choose a non-overlapping scope, make the claim visible, and create the implementation branch normally `agent/<agent>/<scope>`.

For CI repair, use `recovery/<agent>/<scope>`. For multi-agent combination, use `integration/<batch-id>`.

## Mandatory implementation-agent completion gate

Each implementation agent must run relevant local validation, push the final intended branch head, open/update the PR, record the exact head SHA, and classify the final diff.

If every changed path is CI-neutral-only under `CI_POLICY.md`/`.github/workflows/ci.yml`, full build CI is not required; record the path classification and relevant lightweight validation instead. Otherwise observe `.github/workflows/ci.yml`, which runs automatically for implementation-relevant changes on `agent/**`, `recovery/**`, `integration/**`, PRs targeting `main`, and `main`.

For any task where CI is required, an agent **must not report the task completed or stop as completed until the required CI run is `success` for the exact current branch/PR head SHA**. A green run for an older SHA, another branch, another PR or `main` does not satisfy the task.

The CI-neutral exemption is path-based, not commit-message-based. A `chore:` commit still requires CI if it touches source, tests, project/build files, dependencies, scripts, workflows, packaging, signing/runtime-affecting configuration, or any other non-ignored path. Mixed changes always require CI.

If required CI fails, keep the lane active, diagnose/fix the real defect on the task branch, push a new head SHA and repeat. Native AutoCAD/runtime/signing gates that cannot be proven by repository-safe CI remain explicitly `BLOCKED`/handed off; do not promote CI to native PASS.

A GitHub Issue is coordination only; it has no source tree. When CI is required, the Issue must reference the branch/PR and exact SHA whose CI result proves the task.

## Batch integration

For multi-agent implementation work, the coordinator uses `integration/<batch-id>`. Enumerate exact participating claims/SHAs, integrate every required lane without silently dropping work, resolve semantic/API/test conflicts deliberately, verify no required lane remains only elsewhere, require green CI for the exact integration head when implementation-relevant paths changed, inspect the final diff, and perform one authorized final PR/landing to `main`.

After landing, fetch current `main`, record the exact final SHA, and require green CI for that exact SHA when implementation-relevant paths changed before reporting the batch fully integrated.

## Definition of ALL MERGED TO MAIN

Report `ALL MERGED TO MAIN` only when current `main` contains every required implementation, participating claims are terminal or explicitly excluded/superseded, no required code remains only off-main, the exact current `main` SHA is recorded, and any required CI run for that exact SHA is green.

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
- CI classification: `REQUIRED` or `CI_NEUTRAL_ONLY`
- Required CI: `.github/workflows/ci.yml` when classification is `REQUIRED`
- CI result: `PENDING` until exact-head success, or `N/A (CI_NEUTRAL_ONLY)`

## Reserved scope
<exact lane>

## Validation plan
- <local/lightweight checks>
- exact-head remote CI success when required

## Completion condition
<task result plus applicable completion gate, without overstating native evidence>
```
