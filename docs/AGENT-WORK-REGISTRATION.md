# Agent work registration and integration

**Owner rule — 2026-08-14:** every AI agent/chat session must register its lane before substantive implementation, while ordinary agents/sessions keep claim/status/implementation writes off `main` unless the owner explicitly grants integration authority.

This file is the canonical reservation/integration contract. Read `docs/AI-SESSION-WORKFLOW.md` and `CI_POLICY.md` together with it.

## Active ownership and claims

Existing `ACTIVE` / `BLOCKED` claim files under `docs/agent-work-claims/` remain valid and must be respected. For new work, prefer a visible GitHub issue dedicated to the lane; a dedicated claim PR is also acceptable. A claim Markdown file may live on the agent/claim branch and be included in its PR, but publishing it to `main` is not required.

A chat message, local patch, private note or unpushed branch is not a reservation.

Recommended states: `ACTIVE`, `BLOCKED`, `READY_FOR_INTEGRATION`, `COMPLETED`, `RELEASED`.

## Mandatory sequence before implementation

1. Refresh current `origin/main` and inspect relevant recent commits.
2. Read `AGENTS.md`, `CI_POLICY.md`, `docs/AI-SESSION-WORKFLOW.md`, this file, existing active/blocking claim files, and open issues/PRs touching the same surfaces.
3. Choose a non-overlapping lane.
4. Create/update a visible claim issue/PR before substantive implementation.
5. Record stable agent/session ID, timestamp, baseline `main` SHA, exact scope, expected files/symbols/tests, exclusions, acceptance criteria, validation/CI plan, intended implementation branch and any local/external prerequisites.
6. Resolve overlap before material writes.
7. Create `agent/<agent-id>/<scope>` or `recovery/<agent-id>/<scope>` for CI repair.
8. Publish a concrete plan in the claim issue/PR.
9. Implement only the reserved lane on that branch.

There is no claim-only/status-only direct-`main` exception.

## Main authorization boundary

`fix bug`, `update code`, `commit push git`, `continue all`, `implement all`, `run CI`, `fix CI`, `loop until success` and similar ordinary prompts never authorize direct writes or merges to `main`.

Only an explicit owner instruction granting integration authority for that operation may change `main`, such as `merge all to main`, `you are the integration coordinator`, or `allow merge PR #... to main`.

CI ownership never implies integration authority.

## Implementation branch discipline

Each agent/session must refresh `main` as needed, stay inside the reserved scope, use coherent commits, add deterministic regression coverage for behavioral changes where applicable, run relevant local/static/unit/smoke/preflight checks, push the implementation branch, open/update its PR/handoff, and record exact implementation SHAs plus executed evidence.

Never force-push `main`, reset it backwards or silently overwrite another agent's work.

## Mandatory task-CI handoff

After the final intended branch change, classify the diff against `CI_POLICY.md` and `.github/workflows/ci.yml`.

- If every changed path is CI-neutral-only, full build CI is not required; record the exact head SHA, changed-path classification and relevant lightweight validation.
- Otherwise the task remains active until `.github/workflows/ci.yml` succeeds for the exact current branch/PR head SHA.
- Old green runs, another branch/PR, or an older SHA do not satisfy completion.
- A new implementation-relevant commit invalidates previous green evidence and requires fresh exact-head CI.
- GitHub Issues are coordination surfaces only; required CI evidence must point to a branch/PR SHA.

When required CI/checks are red:

1. bind the diagnosis to the exact run and exact tested SHA;
2. inspect the failing job/step/log and find the root cause against current source;
3. fix on the agent/recovery branch, not on `main`;
4. add/retain regression coverage where appropriate;
5. commit and push;
6. run/observe a fresh relevant attempt;
7. repeat from the newest failure until all required/applicable checks for the lane are green.

Do not weaken tests, architecture guards, security/release gates or expected behavior to obtain green CI.

## Multi-agent integration

An explicitly authorized coordinator may combine participating work on `integration/<batch-id>` and must:

1. refresh current `origin/main` immediately before freezing the batch;
2. enumerate exact participating issues/claims/branches/PRs and implementation SHAs;
3. merge/rebase/cherry-pick or semantically reproduce every required lane without silently dropping work;
4. deliberately resolve source/API/docs/test/workflow conflicts instead of choosing `ours`/`theirs` blindly;
5. verify no required implementation remains only on another branch, local worktree, stash or unmerged PR;
6. inspect the combined diff for accidental reversions and duplicate competing implementations;
7. require green CI for the exact integration head when the batch is implementation-relevant;
8. freeze the candidate and perform one explicitly authorized final PR/landing to `main`;
9. refresh `main`, record the exact final SHA and require green exact-main CI when implementation-relevant;
10. continue recovery from the newest exact failure until the current final tree is green.

Ordinary agents do not independently merge themselves into `main`.

## `ALL MERGED TO MAIN`

Report `ALL MERGED TO MAIN` only after an authorized integration reviewer verifies current `main` contains every required repository-safe implementation, no required code remains only off-main, the combined tree has no unresolved collisions/reversions, all participating repository-safe claims are terminal or explicitly superseded, required combined validation is acceptable, and the exact current `main` SHA plus exact-main CI evidence are recorded.

Issue/PR state, branch deletion or old green CI are not sufficient proof. Open issues that require external credentials, GitHub settings or licensed native AutoCAD evidence may remain open and must be reported as external/native blockers rather than falsely closed.

## Prompt/lane completion and session deletion

Every AI/chat session must finish with:

- `PROMPT/LANE STATUS: 100% COMPLETE` or `NOT 100% COMPLETE`;
- `SESSION CAN BE CLOSED/DELETED: YES` or `NO`;
- `MERGED TO MAIN: YES` or `NO`;
- issue/PR/branch references, exact implementation SHA(s), validation/CI results and remaining blockers.

If the prompt did not authorize integration, the lane may be `100% COMPLETE` with `MERGED TO MAIN: NO` once its branch/PR is fully implemented, all lane-responsible validation is green, no known in-scope defect remains and the repository-side handoff is self-contained.

If the prompt explicitly includes integration to `main`, 100% completion additionally requires verified final integration and the exact-main evidence required by `CI_POLICY.md`.

If the lane is not 100% complete and actionable work remains within the session's tools/permissions/scope, continue the plan -> implement/fix -> validate/CI -> diagnose -> repair loop instead of stopping at a checkpoint. External/local blockers must be recorded precisely; unavailable evidence must never be claimed as PASS.
