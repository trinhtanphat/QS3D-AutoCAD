# CI and integration policy

This file is the repository-level source of truth for CI ownership after multi-agent work.

**Owner policy — 2026-08-14:** task-scoped, non-destructive CI/verification is part of the normal AI agent/chat-session completion loop. CI ownership does **not** grant release/publish authority and does **not** grant permission to write or merge `main`.

Read `docs/AI-SESSION-WORKFLOW.md` and `docs/AGENT-WORK-REGISTRATION.md` together with this file.

## Final-tree rule

CI evidence is meaningful only for the exact tree it tested. A green run for an older commit does not prove a newer branch, integration candidate or `main`.

Ordinary prompts such as `fix bug`, `update code`, `commit push git`, `continue all`, `implement all`, `run CI`, `fix CI` or `loop until success` never authorize a direct `main` write/merge.

Only explicit owner integration authority may change `main`, for example `merge all to main`, `you are the integration coordinator`, or `allow merge PR #... to main`.

## Task-scoped CI loop

A session that owns a registered lane may run/observe/retry applicable non-destructive CI/checks for its agent/recovery branch, PR or authorized integration candidate.

When CI is red:

1. identify the exact failing run/check and exact tested SHA;
2. inspect the failing job/step/log and diagnose root cause against current source;
3. fix on `agent/<agent>/<scope>` or `recovery/<agent>/<scope>`, never directly on `main`;
4. add/retain deterministic regression coverage when appropriate;
5. commit and push;
6. run/observe a fresh relevant attempt;
7. repeat from the newest failure until all required/applicable lane checks are green.

Never weaken tests, architecture guards, security checks, packaging/release gates or expected behavior merely to obtain green CI.

For docs-only changes, intentionally skipped code/release jobs are acceptable when no applicable docs CI exists. Record what did and did not run; do not manufacture a release run solely for documentation.

## Canonical progression

For an ordinary session without `main` authority:

```text
CLAIM_ISSUE_OR_PR_VISIBLE
  -> AGENT_BRANCH_READY
  -> BRANCH/PR_VALIDATION
  -> CI_GREEN_FOR_LANE
  -> READY_FOR_INTEGRATION
```

If the owner later authorizes integration:

```text
READY_LANES
  -> INTEGRATION_BRANCH
  -> INTEGRATION_REVIEW
  -> ONE_AUTHORIZED_FINAL_MERGE_TO_MAIN
  -> EXACT-CURRENT-MAIN CI
  -> CI_GREEN
  -> ALL_DONE
```

A session may finish its assigned lane at `READY_FOR_INTEGRATION` when the prompt did not authorize `main`, provided the implementation is complete, no known in-scope defect remains, required/applicable validation is green and handoff is self-contained. It must report `MERGED TO MAIN: NO`.

## CI recovery remains off main

CI authorization is not direct-main authorization. A CI operator fixes failures on an agent/recovery branch and follows the normal PR/integration path.

An authorized integration coordinator must refresh current `main`, combine all required lanes, deliberately resolve conflicts, run combined validation, inspect for accidental reversions/duplicate implementations, freeze the candidate, perform the explicitly authorized final landing, refresh `main`, record the exact final SHA and continue exact-current-main CI recovery until green.

## Evidence boundaries

- Source/Core CI is not native AutoCAD runtime proof.
- Native AutoCAD qualification, installer execution, signing and licensed-host evidence remain separate when required.
- Unavailable native/local evidence must never be claimed as PASS.

## Completion/session-close gate

Every AI/chat session must report:

- `PROMPT/LANE STATUS: 100% COMPLETE` or `NOT 100% COMPLETE`;
- `SESSION CAN BE CLOSED/DELETED: YES` or `NO`;
- `MERGED TO MAIN: YES` or `NO`;
- issue/PR/branch references, exact implementation SHA(s), tests/checks/CI executed and remaining blockers.

If required/applicable CI is red and actionable work remains, continue diagnose -> fix -> push -> fresh run until green instead of stopping at a checkpoint.

## GitHub protection

Repository policy should be backed by branch protection/rulesets for `main` when available: require the intended PR/integration path, block force-push and branch deletion, and require appropriate status checks. Markdown policy does not itself configure those repository settings.
