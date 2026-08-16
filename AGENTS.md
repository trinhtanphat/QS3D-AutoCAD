# Agent Collaboration Policy

This repository is expected to have multiple AI agents/chat sessions working concurrently. Every agent must protect other agents' work, reserve a non-overlapping lane, and keep all implementation/governance changes off `main` unless the owner explicitly grants integration authority.

## Highest-priority Git/Main rule

`docs/MAIN-WRITE-AUTHORIZATION.md` is authoritative for who may change `main`.

**Default:** every normal AI agent/chat session treats `origin/main` as read-only.

The following requests do **not** grant permission to push or merge to `main` by themselves:

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

A session may change `main` only when the repository owner explicitly grants a merge/integration role for the named PR/batch/task, for example `merge all to main`, `you are the integration coordinator`, or `allow merge PR #... to main`.

Authorization is scope-specific and does not automatically carry forward. There is no docs/Markdown/chore exception.

## Mandatory reading order

Before substantive work, read:

1. `AGENTS.md`;
2. `docs/MAIN-WRITE-AUTHORIZATION.md`;
3. `docs/AI-SESSION-WORKFLOW.md`;
4. `docs/AGENT-WORK-REGISTRATION.md`;
5. `CI_POLICY.md`;
6. fetch/read the latest `origin/main` and record its exact SHA;
7. the Issue/claim/runbook for the assigned lane;
8. only the minimum open Issue/PR/claim metadata needed to detect an overlap;
9. the exact product/native/release documents required by the assigned lane.

Current source on the latest valid baseline wins over stale chat checkpoints and historical handoffs.

## Mandatory lane registration

Before implementation, every normal agent/session must:

1. refresh current `origin/main` and record the exact baseline SHA;
2. perform the minimum collision check needed to verify the lane is not already reserved;
3. create or reuse a visible GitHub Issue/claim for the lane;
4. publish a concrete plan with scope, exclusions, expected files/symbols/tests and validation;
5. create a dedicated branch, normally `agent/<agent-id>/<scope>` or `recovery/<agent-id>/<scope>` for CI repair;
6. put **all** task changes on that branch, including source, tests, scripts, workflows, installer, packaging, docs, Markdown, claims and chores;
7. validate, commit and push only that branch;
8. open/update the PR/handoff only after the branch is reviewable and applicable exact-head validation is green;
9. stop before merge unless the owner explicitly authorized integration.

A chat message, local patch, stash or unpushed branch is not a visible reservation.

## Strict lane non-interference

A normal agent owns only its assigned/reserved lane. Work owned by another agent/session is out of scope unless the owner explicitly expands this session's role.

Cross-agent visibility is limited to the minimum coordination metadata necessary to avoid an obvious collision. Once another owner is identified, stop there and choose a different lane or ask the owner/coordinator to split/reassign the overlap.

Normal agents must not opportunistically:

- audit, fix, validate, merge, close, reassign or manage another agent's lane;
- fetch another agent's PR diff/patch merely for curiosity or a broad sweep;
- monitor another agent's branch/CI/draft status as a substitute for working their own lane;
- take over work because it appears stale, slow, blocked or incomplete;
- interpret `continue all` as permission to sweep unrelated active lanes;
- reimplement an already-landed solution instead of using current `main` as implementation truth.

Broader cross-agent inspection requires explicit owner wording such as `review PR #...`, `coordinate with agent ...`, `merge this named batch`, or `you are the integration coordinator`.

## Mandatory sync discipline

Before a material write:

1. refresh latest `origin/main`;
2. confirm the reserved scope still does not collide with newly landed work;
3. base edits on the current valid branch baseline, not an old conversation snapshot.

Before each final branch push and PR handoff:

1. refresh `origin/main` again;
2. check whether relevant current-main code moved;
3. reconcile safely on the task branch if necessary;
4. review the final diff for unrelated reversions or duplicate implementations;
5. obtain fresh exact-head CI when the changed paths require it.

Never force-push `main`, reset it backwards, silently overwrite concurrent work, or use `ours`/`theirs` blindly to hide semantic conflicts.

## Request-scoped commit batching

Prefer coherent commits scoped to the owner request/lane rather than a stream of tiny file-by-file commits.

- Treat one owner request or one `continue all` lane as the default commit unit.
- Accumulate related implementation, regression guards, docs and handoff updates into coherent commits.
- Split only when parts are genuinely independent or separately risky/revertable.
- If current `main` already contains overlapping work, reuse/reconcile the winning implementation instead of creating a duplicate patch.
- Refresh `main` before the final branch handoff.

## Mandatory exact-SHA task CI

`.github/workflows/ci.yml` is the canonical repository-safe task validation workflow.

- For implementation-relevant work, an agent must not report completion until CI succeeds for the exact current branch/PR head SHA required by `CI_POLICY.md`.
- Old green runs, another branch, another PR, an older integration SHA or an older `main` SHA do not count.
- CI-neutral-only work is exempt only when every changed path is in the documented ignore set. Commit prefixes such as `docs:` or `chore:` are not exemptions by themselves.
- If required CI fails, fix the root cause on the task/recovery branch and repeat on a fresh SHA.
- Never weaken tests, architecture guards, security/release gates or expected behavior merely to get green.

For watched implementation-relevant changes, prefer the progression:

```text
issue/reservation
  -> agent/recovery branch
  -> branch push
  -> exact-head CI SUCCESS
  -> refresh/reconcile main if needed
  -> PR/handoff
  -> STOP BEFORE MERGE
```

## Owner-authorized integration coordinator

Only a session explicitly authorized by the owner may integrate/merge a named batch into `main`.

For multi-agent work, prefer:

```text
integration/<batch-id>
```

The authorized coordinator must:

1. refresh current `origin/main`;
2. identify the exact authorized participating Issues/PRs/branches/SHAs;
3. integrate all required work without silently dropping commits;
4. resolve semantic/API/test/docs/workflow conflicts deliberately;
5. verify no required task remains only on an agent branch, worktree, stash or unmerged PR;
6. run required combined-tree validation on the exact integration candidate;
7. inspect the final diff for accidental reversions and duplicate competing implementations;
8. freeze and record the integration candidate SHA;
9. merge to `main` only within explicit owner authorization and repository protection requirements;
10. fetch `main` again, record the exact resulting SHA, and require applicable exact-main CI before reporting the integrated tree green.

Authorization to merge one batch is not standing authorization for later batches.

## Definition of `ALL MERGED TO MAIN`

State **ALL MERGED TO MAIN** only after an owner-authorized integration reviewer verifies against current `main` that:

- every required Issue/reservation is terminal or explicitly excluded/superseded;
- every required implementation/docs commit is represented in current `main`;
- no required work exists only on an agent branch, local worktree, stash, draft patch or unmerged PR;
- required branch/PR/integration/main CI evidence is green and fresh where applicable;
- the combined tree contains the intended behavior without unresolved merge markers, accidental reversions, duplicate competing implementations or known semantic/API/test collisions;
- unavailable licensed/native/external evidence is explicitly handed off rather than falsely reported as PASS;
- the exact current `main` SHA is recorded.

Branch deletion, Issue state, PR UI state or stale CI is not sufficient proof.

## AutoCAD product rules

- Never add `Autodesk.*` references or types to `src/QS3D.Core`.
- Keep AutoCAD transactions, `ObjectId`, entities, editors and UI inside `src/QS3D.AutoCAD`.
- Do not claim native AutoCAD runtime PASS from source review or hosted CI alone.
- Do not weaken architecture guards, tests or packaging checks to make CI green.
- Generated QS3D geometry must retain QS3D metadata so BOQ/editing can distinguish it from user-authored entities.
- Startup must remain lightweight; do not create `PaletteSet` UI from `IExtensionApplication.Initialize()`.
- Preserve the supported runtime split: AutoCAD 2021 (`net48`), AutoCAD 2025–2026 (`net8.0-windows`) and AutoCAD 2027 (`net10.0-windows`).

## Required session-close report

Every session must end with separate lines for:

- `PROMPT/LANE STATUS: 100% COMPLETE` or `NOT 100% COMPLETE`;
- `SESSION CAN BE CLOSED/DELETED: YES` or `NO`;
- `MERGED TO MAIN: YES` or `NO`;
- exact Issue/branch/PR/SHA and validation evidence/blockers.

A lane may be fully complete while `MERGED TO MAIN: NO` when the owner asked for implementation/commit/push but did not authorize integration.
