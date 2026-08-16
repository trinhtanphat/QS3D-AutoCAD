# Agent work registration and integration

**Owner rule:** normal AI agents/chat sessions treat `origin/main` as read-only. Every task—including source, tests, scripts, workflows, installer, packaging, documentation, Markdown, claim/handoff/status and chores—must be done on a dedicated issue/branch/PR unless the repository owner explicitly grants integration authority.

`docs/MAIN-WRITE-AUTHORIZATION.md` is authoritative for `main` write permission. This file is the canonical work-registration and batch-integration protocol. `CI_POLICY.md` is authoritative for CI behavior.

## Source of truth for reservations

Use a GitHub Issue as the immediately visible work reservation whenever practical. Historical Markdown claims remain under:

```text
docs/agent-work-claims/
```

New or updated Markdown claims may still be used for repository history, but they belong on the task branch/PR; they are **not** pushed directly to `main` as a prerequisite for implementation.

A reservation should identify:

- status (`ACTIVE`, `BLOCKED`, `READY_FOR_INTEGRATION`, `COMPLETED`, or `RELEASED`);
- stable agent/session identity;
- exact baseline `main` SHA;
- exact scope and exclusions;
- expected files/symbols/tests/runtime surfaces;
- validation/CI plan;
- task branch and PR when created;
- related issue and integration batch when known.

A chat message, local patch, private note, stash or unpushed branch is not a reservation.

## Strict lane non-interference — highest priority for normal agents

A normal AI agent/chat session owns **only its assigned/reserved lane**. Work owned by another agent/session is out of scope unless the repository owner explicitly expands this session's role.

Cross-agent visibility is limited to the **minimum coordination metadata necessary to avoid an obvious collision**, for example whether a lane/file/symbol/runtime surface is already reserved and the reservation's stated scope/exclusions. Once another owner is identified, stop there and choose a different non-overlapping lane unless the owner explicitly assigns coordination with that agent.

For normal agents:

- do not fetch another agent's PR diff/patch for curiosity or a broad review;
- do not monitor another agent's branch commits, CI runs, draft status or completion status as a substitute for working this lane;
- do not merge/close/update/reassign another agent's PR or Issue;
- do not take over another lane because it appears stale, slow, blocked or incomplete;
- do not `continue all` by sweeping unrelated agents' open work;
- if another agent's already-landed work on current `main` overlaps this lane, inspect **current `main`** as implementation truth and reconcile against it instead of recreating the patch.

Broader cross-agent inspection requires explicit owner wording such as `review PR #...`, `coordinate with agent ...`, `merge this named batch`, or `you are the integration coordinator`.

## Mandatory sequence for a normal agent

1. Fetch/read current `origin/main` and record the exact SHA.
2. Read `AGENTS.md`, `docs/MAIN-WRITE-AUTHORIZATION.md`, `docs/AI-SESSION-WORKFLOW.md`, `CI_POLICY.md`, this file, and the Issue/claim/runbook for **this lane**.
3. Perform only the minimal reservation/collision check needed to verify this lane is not already owned; do not audit unrelated agents' work.
4. Choose a non-overlapping lane.
5. Create or update a GitHub Issue to register the lane, unless an existing owner-created issue already uniquely identifies it.
6. Publish a concrete plan with scope, exclusions, likely files/symbols/tests and validation.
7. Create a dedicated branch from the latest valid baseline, normally:

   ```text
   agent/<agent-id>/<scope>
   ```

   Use `recovery/<agent-id>/<scope>` for focused CI recovery when appropriate.
8. Put every repository change for the task on that branch, including docs/Markdown/claims/chores.
9. Implement only the reserved lane.
10. Run relevant local/static/unit/smoke/preflight validation available to this lane.
11. Review the diff for accidental reversions, unrelated edits and duplicate implementations.
12. Create coherent request/lane-scoped commits.
13. Immediately before each push, fetch the remote ref for this task branch and compare it with the task-branch head last observed by this session. If it advanced unexpectedly, preserve/reconcile those commits rather than overwriting them.
14. Push only a fast-forward-safe task-branch update.
15. For implementation-relevant work, wait for the applicable exact current branch-head CI to reach `SUCCESS` before treating the branch as PR-ready. Do not use a new PR merely as the first diagnostic CI attempt when branch CI is available for that path set.
16. Re-fetch `origin/main`. If it moved materially, reconcile against current `main` safely without overwriting another agent's unmerged work, re-check the remote task-branch head, push the reconciled branch, and obtain fresh required CI again.
17. Open/update a PR targeting the intended integration branch or `main` and record exact SHA/evidence.
18. Stop before merge unless the owner explicitly authorized this session to merge/integrate.

An Issue plus pushed task branch is the preferred visible coordination surface before PR creation; the PR becomes the review/handoff surface once the candidate is actually reviewable.

## No implicit `main` authorization

The following owner phrases authorize task work but **do not authorize a write or merge to `main`** by themselves:

- `fix bug`
- `update code`
- `implement all`
- `continue all`
- `commit`
- `commit push git`
- `review and fix`
- `update docs`
- `update md`
- `chore`
- `run CI`
- `fix CI`
- `loop until success`

A session may change `main` only after explicit owner authorization such as `merge all to main`, `you are the integration coordinator`, `allow merge PR #... to main`, or another equally clear instruction naming the merge/integration action.

Authorization is limited to the named PR/batch/task. It does not carry forward automatically.

Automatic CI is validation infrastructure, not merge authorization. Repository protection eligibility is also not merge authorization.

## Branch and conflict discipline

Every normal agent must:

1. base its branch on the latest valid `main` baseline;
2. refresh `origin/main` before material writes and before final handoff;
3. keep edits inside the reserved scope;
4. make coherent lane/request-level commits rather than file-by-file noise;
5. never force-push or reset shared `main`;
6. never update the `main` ref directly;
7. never use the GitHub contents API against `main` for docs, claims, chores or code;
8. never merge its own PR unless explicit owner merge authorization was granted;
9. never use `ours`/`theirs` blindly to hide a semantic conflict;
10. never silently drop a concurrent commit while rebasing/merging/reapplying;
11. record branch/commit/PR and actual validation evidence in the Issue or task handoff.

### Published task-branch write safety

A published `agent/**`, `recovery/**` or `integration/**` branch must not be treated like a private local branch.

- The expected remote branch head is part of the session state. Record or otherwise retain the exact remote SHA observed after each successful push/fetch.
- Immediately before a later push, fetch/read the current remote branch ref again.
- If the remote SHA differs from the last observed remote SHA and the new commit was not created by this session, treat it as a concurrent write even when the branch name belongs to the same lane.
- Do **not** force-push, reset the branch backwards, recreate the ref from an older local commit, or use a contents/ref API write that discards the newer remote commit.
- Inspect/reconcile only the unexpected commits on **this same task branch**, because they are part of the already-reserved lane. Preserve both changes when compatible.
- If the unexpected branch write makes ownership ambiguous, stop sharing that branch: update the Issue/PR handoff and continue on a new dedicated branch rather than racing for the same ref.
- After reconciliation, the next pushed commit must descend from the current remote task-branch head. A stale local candidate is not pushable merely because its file diff still looks correct.

Published task branches are therefore single-writer by default. Sharing one branch between sessions requires explicit same-lane handoff and exact remote-head reconciliation; force-push is not a coordination mechanism.

When `main` moved:

- first determine whether the moved code overlaps this reserved lane;
- if not, reconcile normally on the task branch;
- if yes, treat current `main` as the new truth, review semantics, preserve both intended behaviors when compatible, and stop for owner/coordinator direction when the lane ownership boundary becomes ambiguous.

A pushed branch, open PR, green CI run or artifact zip is **not** `ALL MERGED TO MAIN`.

## Request-scoped commit batching

The owner prefers coherent commits scoped to the request/lane.

- One owner request or one `continue all` lane is the default commit unit.
- Combine related implementation, regression guards, docs and handoff updates when they form one reviewable change.
- Split only when changes are independently risky/revertable or genuinely unrelated.
- Do not manufacture no-op commits merely to show activity.
- If current `main` already contains the intended fix, close/reframe the duplicate lane instead of committing a competing implementation.

## Shared branch/PR CI

The repository uses `.github/workflows/ci.yml`; agents do not create one workflow per branch.

For implementation-relevant paths:

- push to `agent/**` / `recovery/**` validates the exact branch tree;
- a PR targeting `main` or an authorized integration branch validates the current candidate according to repository workflow/protection settings;
- push to `integration/**` validates the exact combined tree assembled by an authorized coordinator;
- push to `main` validates the exact landed tree when the workflow applies.

A green agent branch proves only the exact tested branch SHA. A green PR candidate proves only that candidate. A green integration branch proves only the combined integration tree. Exact-main evidence proves only the landed SHA it actually tested.

CI-neutral-only work may legitimately have no heavy branch CI when every changed path is in the ignore set documented by `CI_POLICY.md` / `.github/workflows/ci.yml`. Record that classification instead of manufacturing an unrelated run.

## Documentation, Markdown, claims and chores

There is no direct-main docs-only exception. These surfaces also stay on task branches until an authorized merge:

```text
docs/**
*.md
docs/agent-work-claims/**
README.md
handoff/status files
policy files
release-note preparation
non-functional chores
```

## Multi-agent integration branch

For a multi-agent owner request, the owner-authorized coordinator should assemble the combined candidate on:

```text
integration/<batch-id>
```

The coordinator exception begins **only after explicit owner authorization**. Only then may the coordinator inspect the exact named participating Issues/PRs/branches required for the authorized batch.

The coordinator must:

1. refresh latest `origin/main`;
2. identify the exact authorized participating Issues/PRs/branches/SHAs;
3. preserve every required lane without silently dropping commits;
4. resolve semantic/API/docs/test/workflow conflicts deliberately rather than choosing `ours`/`theirs` blindly;
5. verify no required work remains only on an unmerged branch/PR/worktree/stash;
6. obtain green combined-tree CI on the exact frozen `integration/**` SHA when implementation-relevant;
7. inspect the final diff for accidental reversions, duplicate implementations and contract mismatches;
8. freeze and record the integration candidate SHA;
9. merge to `main` only within the owner's explicit authorization and applicable GitHub protection requirements;
10. fetch `main` again and record the exact resulting SHA;
11. require the applicable exact-main CI before claiming the integrated repository tree green.

Do not assemble a multi-agent batch by independently landing every agent PR on `main` unless the owner explicitly requests that strategy.

## Definition of `ALL MERGED TO MAIN`

For a specific owner request, state **ALL MERGED TO MAIN** only after an **owner-authorized integration reviewer/coordinator** freshly verifies:

- every required Issue/reservation in the authorized batch is terminal or explicitly excluded/superseded;
- every required implementation/docs commit is represented in current `main`;
- no required work exists only on an agent branch, worktree, stash, draft patch or unmerged PR;
- required participating branch/PR/integration/main CI evidence is green and fresh where applicable;
- current `main` was refreshed after the authorized landing;
- the combined tree contains the intended behavior without unresolved merge markers, accidental reversions, duplicate competing implementations or known semantic/API/test collisions;
- unavailable native AutoCAD, credentials, signing or external-service evidence is explicitly handed off rather than falsely reported as PASS;
- the exact current `main` SHA is recorded.

A normal non-coordinator agent must not perform this repository-wide sweep merely to decide whether its own lane is done.

## Scope changes and handoff

If work expands beyond the registered scope:

1. stop before touching the added implementation surface;
2. refresh `main` and perform only the minimum collision check for the added scope;
3. update the task Issue/branch handoff with the added scope;
4. if the added scope is owned by another agent, keep it excluded unless the owner explicitly reassigns/splits it;
5. continue only after the ownership boundary is clear.

If another agent should continue, leave exact completed state, remaining work, branch/commit/PR references and successor boundary in **this lane's** Issue/PR/handoff; do not manage the successor's execution.

## Closing a task

Before an authorized merge, update the Issue/PR with:

- branch name;
- implementation/docs commit SHA(s);
- validation actually executed;
- exact tested SHA and CI run when applicable;
- known native/local/external gates belonging to this lane;
- intended integration batch when known.

A normal agent does not push close-out Markdown directly to `main` and does not close another agent's Issue/PR.
