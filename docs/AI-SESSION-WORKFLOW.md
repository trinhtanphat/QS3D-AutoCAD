# Mandatory AI agent / chat-session workflow

**Owner policy.** This policy applies to every AI agent and chat session working in this repository. It supersedes older wording that allowed or required an ordinary agent/session to publish claim/status/implementation commits directly to `main`.

`docs/MAIN-WRITE-AUTHORIZATION.md` is authoritative for `main` write permission. `docs/AGENT-WORK-REGISTRATION.md` is authoritative for reservation/integration mechanics. `CI_POLICY.md` is authoritative for CI evidence.

## 1. Register the work before substantive implementation

The first write for a new prompt/lane must be a visible coordination record that does **not** modify `main` directly.

Preferred registration:

1. refresh current `main` and record its exact SHA;
2. perform only the minimum Issue/PR/claim check needed to detect an overlap;
3. create or reuse a visible GitHub Issue for the lane; a dedicated claim PR is also acceptable when appropriate;
4. record a stable agent/session identifier, baseline `main` SHA, exact scope, expected files/symbols/tests, exclusions, acceptance criteria, validation/CI plan and intended branch;
5. resolve overlap before implementation;
6. create a dedicated branch such as `agent/<agent-id>/<scope>` or `recovery/<agent-id>/<scope>` for CI repair.

A chat message, local patch, stash or unpushed branch is not sufficient registration. A claim issue/PR is coordination only and is not permission to merge implementation to `main`.

## 2. Main authorization boundary

Ordinary work prompts do **not** authorize direct writes or merges to `main`.

The following phrases, by themselves, never grant `main` authority: `fix bug`, `update code`, `commit`, `commit push git`, `continue all`, `implement all`, `update docs`, `update md`, `run CI`, `fix CI`, `loop until success`, or equivalent wording.

An agent/session may change `main` only when the owner explicitly grants integration authority for that operation, for example: `merge all to main`, `you are the integration coordinator`, or `allow merge PR #... to main`.

CI ownership is separate from integration authority. A CI-fixing session still works on its own branch/PR and must not bypass the integration path.

## 3. Planning gate

Before implementation, publish a concrete plan in the claim issue/PR or equivalent visible coordination record. The plan must include:

- current problem and acceptance criteria;
- reserved scope and explicit exclusions;
- files/symbols/interfaces likely to change;
- regression and compatibility risks;
- tests/preflights/runtime evidence required;
- applicable CI workflows/checks and success criteria;
- known local-only or external prerequisites.

Do not begin broad implementation first and invent the plan afterward.

## 4. Strict lane non-interference

A normal agent/session owns only the lane it reserved. Other active lanes are out of scope unless the owner explicitly assigns cross-agent coordination.

Use other Issues/PRs/claims only for the minimum collision metadata needed to avoid overlapping paths/symbols/runtime surfaces. Once another owner is identified, stop and choose a different lane or obtain an explicit split/reassignment.

Do not opportunistically review, fix, rerun CI for, merge, close, reassign or take over another agent's work. Do not interpret `continue all` as a repository-wide sweep of unrelated active lanes.

When already-landed work on current `main` overlaps this lane, treat current `main` as implementation truth and reconcile against it rather than duplicating the patch.

## 5. Implementation and bug-fix loop

For the reserved lane:

1. refresh latest `main` before material writes;
2. implement the complete requested scope on the dedicated branch;
3. add or update deterministic regression coverage where applicable;
4. run relevant local/static/unit/smoke/preflight checks;
5. review the diff for accidental reversions, overlap, duplicate implementations and unrelated edits;
6. create coherent request/lane-scoped commits rather than file-by-file noise;
7. push the branch so the implementation is reviewable in GitHub;
8. run/observe applicable exact-head branch CI before a new PR when the changed paths require CI;
9. refresh `main` again; if it moved materially, reconcile safely on the task branch, push the new head and obtain fresh required CI;
10. open or update the PR/handoff with the exact branch/head SHA and evidence;
11. if defects remain, continue the same loop instead of reporting completion.

Never force-push or reset `main`, silently overwrite another agent's work, use `ours`/`theirs` blindly, or weaken tests/architecture/security/release gates merely to obtain a green result.

A downloaded/generated ZIP, installer or other artifact is supplemental evidence only. It does **not** replace repository commit/push of the actual source/docs change.

## 6. CI loop — continue until applicable checks are green

For task-scoped, non-destructive CI that the session is permitted to operate, CI is part of the normal completion loop:

1. run/observe the applicable CI for the branch/PR/integration candidate;
2. bind every diagnosis to the exact run and exact tested SHA;
3. when red, inspect the failing job/step/log and identify the root cause against current source;
4. fix on the same dedicated branch/recovery branch, add regression coverage when appropriate, commit and push;
5. run/observe a fresh relevant CI attempt;
6. repeat from the newest failure until all required/applicable checks for that lane are green.

Old green runs, another branch, another PR or an older SHA are not evidence for the current candidate.

If the repository has no applicable branch/PR CI for a CI-neutral-only documentation change, record the path classification and lightweight validation; do not manufacture a release run solely to make a documentation PR look tested.

If required CI or runtime evidence cannot be executed because of missing permissions, proprietary/local environment, credentials or another external prerequisite, register the blocker/handoff precisely and do not claim unavailable evidence as PASS.

## 7. Multi-agent integration

Only an owner-authorized integration coordinator may assemble/land a named multi-agent batch.

Prefer `integration/<batch-id>`. The coordinator must refresh current `main`, enumerate the exact participating Issues/branches/PRs/SHAs, preserve all required work, deliberately resolve semantic conflicts, inspect for accidental reversions/duplicate implementations, obtain exact-candidate combined validation, freeze the candidate SHA, and merge only within the owner's explicit authorization.

After landing, refresh `main`, record the exact resulting SHA and require the applicable exact-main validation before reporting the integrated tree green.

Authorization for one batch does not carry forward to unrelated later merges.

## 8. Completion gate and session close/delete verdict

Every agent/chat session must end with an explicit verdict for the user's prompt/lane. The final report must state all of the following:

- `PROMPT/LANE STATUS: 100% COMPLETE` or `NOT 100% COMPLETE`;
- `SESSION CAN BE CLOSED/DELETED: YES` or `NO`;
- `MERGED TO MAIN: YES` or `NO`;
- branch and PR/issue references;
- exact implementation commit SHA(s);
- tests/preflights/CI actually executed and their result;
- known remaining bugs, blockers, local/native gates or review items.

`100% COMPLETE` means the assigned scope and acceptance criteria are implemented, no known in-scope defect remains, the actual repository change is committed/pushed and reviewable, and all required/applicable validation that this lane is responsible for is green.

If the prompt did **not** explicitly authorize integration to `main`, a lane may be `100% COMPLETE` and the session may be closed when its branch/PR is fully implemented, validated and handed off. In that case `MERGED TO MAIN` remains `NO`; final integration is a separate coordinator responsibility.

If the prompt explicitly includes merging/integration to `main`, then `100% COMPLETE` additionally requires verified integration into current `main` and the exact-main validation required by repository policy.

If the verdict is `NOT 100% COMPLETE`, continue the implement/fix/CI loop while actionable work remains within the session's tools, permissions and reserved scope. Stop only for a real external/local blocker and record it precisely.

## 9. Multi-agent handoff discipline

Keep the claim/issue/PR updated enough that another authorized agent or coordinator can continue without relying on chat history. Before ending a completed lane, ensure the repository-side handoff contains the exact branch, SHA, scope, validation result and remaining integration responsibility.

Do not close or delete the coordination record until the repository's claim lifecycle says it is safe. Closing the chat session and closing the GitHub claim are separate decisions.
