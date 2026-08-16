# Main write authorization policy

This document is the canonical repository rule for who may change `main`. It overrides older wording in `AGENTS.md`, `CI_POLICY.md`, handoffs, claim files or historical instructions that allowed claim/docs/chore/source commits directly to `main`.

## Default rule: agents treat `main` as read-only

Unless the repository owner explicitly authorizes the current agent/session to integrate or merge, every AI agent/chat session must treat `origin/main` as **read-only**.

Normal owner requests such as any of the following do **not** grant write/merge permission to `main`:

- `fix bug`
- `update code`
- `implement all`
- `continue all`
- `commit`
- `commit push git`
- `review and fix`
- `run tests`
- `run CI`
- `fix CI`
- `prepare release`
- `update docs`
- `update md`
- `chore`

Those requests authorize work only inside the task's own issue/branch/PR scope unless the owner also gives explicit `main` integration authorization.

## Explicit authorization required

An agent/session may change `main` only when the owner clearly grants that role for the current operation, for example:

- `merge all to main`
- `you are the integration coordinator`
- `allow merge PR #... to main`
- `merge this integration branch to main`
- another equally explicit instruction naming the `main` merge/integration action

Authorization is scope-specific. Permission to merge one PR/batch does not become standing permission for later tasks. Permission to run/fix CI does not imply permission to merge. Permission to prepare release material does not imply permission to publish a release.

## Mandatory workflow for normal agents

1. Fetch/read the latest `origin/main` and record the exact baseline SHA.
2. Perform the minimum current Issue/PR/claim check needed to detect overlap.
3. Register/reuse the task Issue or claim without writing to `main`.
4. Create a dedicated branch from the latest valid `main`, normally `agent/<agent-id>/<scope>` or `recovery/<agent-id>/<scope>`.
5. Put **all** task changes on that branch: source, tests, scripts, workflows, installer, packaging, docs, Markdown, claims, handoff/status files and chores.
6. Commit coherently and push only that branch.
7. Run applicable exact-head validation and fix failures on the task/recovery branch.
8. Re-fetch `main`; if it moved, reconcile safely without overwriting concurrent work, push the new head and obtain fresh required validation.
9. Open/update a PR targeting the intended integration branch or `main`.
10. Stop before merge unless the owner explicitly granted merge/integration authorization.

A normal agent must never use a direct ref update, direct contents write, force push, merge API or equivalent operation that changes `main`.

## Documentation, Markdown, claims and chores

There is **no docs-only exception** to the read-only-main rule for normal agents.

The following also require a dedicated branch/PR:

- `docs/**`
- `*.md`
- `docs/agent-work-claims/**`
- handoff/status files
- README/policy updates
- issue/PR templates
- non-functional chores
- release notes prepared by an agent

This prevents coordination commits from racing implementation commits and keeps one auditable integration path.

For work registration, prefer a GitHub Issue as the immediately visible reservation. If a Markdown claim is useful for repository history, create/update it on the task branch/PR; it does not need to land on `main` before implementation starts.

## Strict non-interference

Main authorization and lane ownership are separate. A normal agent may not use its own task authorization to manage another agent's lane.

Other active lanes may be inspected only to the minimum extent necessary to detect collisions. Broader review, repair, CI management, merge/close/reassignment or batch-wide inspection requires explicit owner authorization for that cross-agent role.

When overlapping work has already landed on current `main`, current `main` is the implementation truth. Reconcile against it rather than creating a duplicate competing patch.

## Integration coordinator

Only an owner-authorized integration coordinator may merge a named batch into `main`.

For multi-agent batches, prefer `integration/<batch-id>`.

The coordinator must:

1. refresh current `origin/main`;
2. identify the exact authorized participating PRs/branches/issues/SHAs;
3. assemble/review the combined candidate without silently dropping work;
4. resolve semantic/API/docs/test/workflow conflicts deliberately;
5. verify required commits are represented and no required task remains only on an unmerged branch/worktree/stash;
6. obtain required combined-tree validation on the exact candidate;
7. inspect the combined diff for accidental reversions and duplicate implementations;
8. freeze and record the candidate SHA;
9. merge to `main` only within the owner's explicit authorization and current GitHub protection requirements;
10. fetch `main` again and record the exact resulting SHA;
11. require applicable exact-main validation before calling the integrated tree green;
12. never treat that authorization as permission for unrelated later merges.

## CI behavior

`CI_POLICY.md` is authoritative for exact-SHA validation.

A commit message such as `docs:`, `chore:` or `md:` is **not** sufficient evidence that CI should be skipped; changed paths are authoritative. CI-neutral-only work may legitimately skip heavy CI only when every changed path is in the documented ignore set.

CI success is evidence for the exact tree it tested. It is not owner authorization to merge or publish.

## GitHub protection

Repository policy should be backed by GitHub branch protection/rulesets where available:

- protect `main` from force-push and deletion;
- require PR-based changes for normal writers;
- keep owner/admin bypass narrow and deliberate;
- require stable implementation checks when practical.

Repository Markdown does not prove external GitHub settings are currently active. When protection state matters, verify the effective GitHub rules/settings instead of assuming they match policy. If protection is absent or weaker than intended, treat that as a governance defect; do not reinterpret it as permission for agents to push directly to `main`.

## Precedence

When another repository document conflicts with this file on `main` write permission, this file wins unless the repository owner explicitly changes the policy again.
