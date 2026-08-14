# Agent work registration and integration

**Owner rule:** implementation work stays off `main` until one reviewed final integration landing.

This protocol applies to AI agents, chat sessions, CI/recovery sessions, local workers and remote workers.

## Claims

Claims live under `docs/agent-work-claims/` and use one Markdown file per lane.

Statuses:

- `ACTIVE` — reserved and not yet fully integrated;
- `BLOCKED` — reserved but currently blocked;
- `COMPLETED` — verified in the intended final `main` tree;
- `RELEASED` — intentionally abandoned/superseded without claiming completion.

Before implementation:

1. fetch latest `origin/main`;
2. read `AGENTS.md`, `CI_POLICY.md`, this file and every `ACTIVE` / `BLOCKED` claim;
3. choose a non-overlapping scope;
4. create `docs/agent-work-claims/YYYY-MM-DD-<agent>-<scope>.md` with agent identity, baseline SHA, exact scope/files/symbols, exclusions and validation plan;
5. make the claim visible on `main` before implementation, preferably through a tiny `claim/<agent>/<scope>` PR;
6. refresh again and resolve any concurrently published overlap;
7. create the implementation branch, normally `agent/<agent>/<scope>`.

A chat message, local patch, unpushed branch, issue assignment or draft PR is not a reservation by itself.

## Implementation branches

Source/test/script/workflow/installer/packaging/release implementation must remain on the dedicated branch until integration. Do not independently land implementation to `main`.

For CI repair, use `recovery/<agent>/<scope>` or an ordinary `agent/...` branch. Being the CI operator does not bypass this rule.

Each implementation agent must refresh `main` periodically, stay inside the reserved lane, use coherent commits, run relevant branch-local validation, publish the branch/commit SHA, and keep the claim non-terminal until integration is verified.

## Batch integration

For multi-agent work, the coordinator uses `integration/<batch-id>` as the combined candidate. The coordinator must:

1. refresh current `origin/main`;
2. enumerate the exact participating claims and implementation SHAs;
3. merge/cherry-pick/rebase every required lane into the integration branch without silently dropping work;
4. resolve semantic/API/test conflicts deliberately rather than blindly choosing `ours`/`theirs`;
5. verify no required lane remains only on another branch, local worktree, stash or unmerged PR;
6. run relevant combined-tree builds/tests/preflights;
7. inspect the combined diff for accidental reversions and duplicate competing implementations;
8. freeze the batch;
9. perform one final PR/landing from the combined candidate to `main`;
10. fetch `main` again and record the exact final SHA.

## Definition of ALL MERGED TO MAIN

Report `ALL MERGED TO MAIN` only when current `main` has been freshly verified to contain every required implementation, all participating claims are terminal or explicitly excluded/superseded, no required code remains only off-main, combined validation is acceptable, and the exact final `main` SHA is recorded.

Branch deletion, PR UI state, issue state and old green CI are not sufficient proof. Commit/tree reachability and the current combined source are authoritative.

## Suggested claim template

```markdown
# Work claim — <scope>

- Status: `ACTIVE`
- Agent: `<stable-agent-id>`
- Registered: `<ISO-8601 timestamp with timezone>`
- Baseline main SHA: `<40-char SHA>`
- Implementation branch: `agent/<agent>/<scope>`
- Integration batch: `integration/<batch-id>` or `TBD`

## Reserved scope
<exact lane>

## Expected surfaces
- <files/symbols/tests>

## Excluded scope
- <neighboring work not owned>

## Validation plan
- <checks>

## Completion condition
<integrated outcome>
```
