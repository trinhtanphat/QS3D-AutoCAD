# CI and integration policy

This file is the repository-level source of truth for task CI, recovery and final integration.

**Owner policy:** task-scoped, non-destructive CI/verification is part of the normal AI agent/chat-session completion loop. CI ownership does **not** grant release/publish authority and does **not** grant permission to write or merge `main`.

Read `docs/MAIN-WRITE-AUTHORIZATION.md`, `docs/AI-SESSION-WORKFLOW.md` and `docs/AGENT-WORK-REGISTRATION.md` together with this file. `docs/MAIN-WRITE-AUTHORIZATION.md` wins on `main` write permission.

## Per-agent task CI

`.github/workflows/ci.yml` is the canonical repository-safe validation workflow. A lightweight classification job is created for pushes on:

- `agent/**`;
- `recovery/**`;
- `integration/**`;
- `main`.

Pull requests targeting `main` remain path-filtered, and manual dispatch remains available when explicitly permitted.

For task/integration branches, the classifier skips the heavy build/test/package job when the complete branch diff from current `main` is CI-neutral-only. For **every push to `main`**, the classifier must force the heavy job to run regardless of changed paths. This guarantees an exact-main build/test/package artifact for every landed SHA and keeps the downstream engineering prerelease chain from being silently skipped by docs-only merges.

CI concurrency must preserve that guarantee. Stale task/PR runs may be canceled to save runner capacity, but a newer push to `main` must **not** cancel an already-running `main` CI. Back-to-back landed SHAs queue and finish independently so each can upload its exact engineering artifact and trigger its own downstream prerelease.

Every required run validates its own exact checkout SHA. Multiple agents may share the workflow definition, but each task branch has independent CI evidence.

A GitHub Issue is coordination only; it has no source tree to build. CI evidence belongs to the branch/PR SHA referenced by the issue.

## CI-neutral-only exemption

The heavy build/test/package job may be skipped on non-`main` task/integration branches when **all** changed paths are limited to the repository's CI-neutral set, including documentation/Markdown and non-executable housekeeping such as `.gitignore`, `.gitattributes`, `.editorconfig`, license/notice files and issue/PR templates. Docs-only PRs to `main` may also remain excluded by the `pull_request.paths-ignore` trigger.

This exemption is path-based, not commit-message-based. A `docs:` or `chore:` commit still requires normal heavy CI when it touches source, tests, project/build files, dependencies, scripts, workflows, installer, packaging, signing/runtime-affecting configuration, release machinery or any other non-neutral path. Mixed changes always require normal heavy CI.

**There is no heavy-CI exemption after landing on `main`.** Every new `main` SHA must receive full exact-main CI so the verified engineering artifact exists and `engineering-release.yml` can publish the corresponding test prerelease after success.

For CI-neutral-only branch work, record the path classification and any relevant lightweight validation instead of manufacturing unrelated branch build work. After an authorized merge, wait for the mandatory exact-main heavy CI and downstream engineering publication evidence before claiming the landed release chain healthy.

## Mandatory exact-head completion gate

For any task that is not CI-neutral-only, an implementation agent must not report the task complete until the required CI run for the **exact current branch/PR head SHA** has conclusion `success`.

A green run for an older SHA, another branch, another PR, an older integration candidate or `main` does not count. Any new implementation-relevant task commit invalidates earlier green evidence for completion.

For implementation-relevant changes where branch-push CI is configured, use branch CI as the admission gate before a new PR is treated as ready for review. A new PR should not be created merely to obtain the first diagnostics for a branch that can already be tested directly. If `main` moves after branch validation and reconciliation changes the branch tree, fresh exact-head evidence is required.

If CI fails, keep the task active and continue:

1. identify the exact failing run/check and exact tested SHA;
2. inspect the failing job/step/log and diagnose root cause against current source;
3. fix on `agent/<agent>/<scope>` or `recovery/<agent>/<scope>`, never directly on `main`;
4. add/retain deterministic regression coverage when appropriate;
5. commit and push a new SHA;
6. run/observe a fresh relevant attempt;
7. repeat from the newest failure until all required/applicable checks are green.

Never weaken tests, architecture guards, security checks, packaging/release gates or expected behavior merely to obtain green CI.

## Main authorization boundary

Ordinary prompts such as `fix bug`, `update code`, `commit push git`, `continue all`, `implement all`, `run CI`, `fix CI` or `loop until success` never authorize a direct `main` write/merge.

Only explicit owner integration authority may change `main`, for example `merge all to main`, `you are the integration coordinator`, or `allow merge PR #... to main`.

CI success is a quality/completion gate, not merge authorization.

## Canonical progression

For implementation-relevant changes owned by an ordinary session:

```text
CLAIM_ISSUE_OR_PR_VISIBLE
  -> AGENT/RECOVERY_BRANCH
  -> PUSH EXACT TASK HEAD
  -> EXACT-HEAD BRANCH CI
  -> CI_GREEN
  -> REFRESH/RECONCILE CURRENT MAIN IF NEEDED
  -> PR/HANDOFF READY
  -> READY_FOR_INTEGRATION
```

When the owner authorizes integration:

```text
READY_LANES
  -> INTEGRATION_BRANCH
  -> EXACT-INTEGRATION CI
  -> CI_GREEN
  -> INTEGRATION REVIEW
  -> ONE AUTHORIZED FINAL MERGE TO MAIN
  -> EXACT-CURRENT-MAIN FULL CI
  -> CI_GREEN + VERIFIED ENGINEERING ARTIFACT
  -> ENGINEERING PRERELEASE PUBLISHED
  -> ALL_DONE
```

CI-neutral-only work follows the same branch/PR authorization path without requiring the heavy branch job, but after landing it still enters the mandatory exact-current-main full-CI and engineering-publication path.

## Integration and recovery

An authorized integration coordinator must refresh current `main`, enumerate exact participating claims/SHAs, combine every required lane, deliberately resolve semantic conflicts, verify no required implementation remains only on another branch/PR/worktree/stash, run combined validation, inspect for accidental reversions/duplicate competing implementations, freeze the candidate and only then perform the authorized final landing.

If integration or exact-current-main CI is red, recovery remains off `main`: diagnose the exact failure, repair on `recovery/<agent>/<scope>` or an integration recovery branch, fold the repair into the current candidate, and repeat from the newest relevant SHA until green.

Never force-push `main`, reset it backwards, silently overwrite concurrent work, use `ours`/`theirs` blindly to hide semantic conflicts, or use stale CI as proof of the current tree.

## Release/native boundary

Repository CI proves source/build/test/package contracts only. A successful push CI on exact `main` also produces the unsigned engineering candidate consumed by `engineering-release.yml`; the downstream test prerelease is still not licensed native AutoCAD runtime proof.

- AutoCAD 2021 native qualification uses its real legacy host/runtime lane when required.
- AutoCAD 2025/2026/2027 native qualification remains separate when required.
- Authenticode production signing and real commercial credentials/services remain separate release gates.
- Engineering package artifacts are not production/native PASS without their exact acceptance evidence.
- Production `v*` releases remain separately gated by native acceptance and signing; they are not created merely because `main` CI succeeds.
- Unavailable native/local evidence must never be claimed as PASS.

## Completion/session-close gate

Every AI/chat session must report:

- `PROMPT/LANE STATUS: 100% COMPLETE` or `NOT 100% COMPLETE`;
- `SESSION CAN BE CLOSED/DELETED: YES` or `NO`;
- `MERGED TO MAIN: YES` or `NO`;
- issue/PR/branch references, exact implementation SHA(s), tests/checks/CI executed and remaining blockers.

If required/applicable CI is red and actionable work remains, continue diagnose -> fix -> push -> fresh run until green instead of stopping at a checkpoint.

A lane can be complete with `MERGED TO MAIN: NO` when the owner requested implementation/commit/push but did not authorize integration.

## GitHub protection

Repository policy should be backed by branch protection/rulesets for `main` when available: require the intended PR/integration path, block force-push and deletion, and require stable implementation status checks where appropriate. Docs-only PRs may remain exempt from heavy PR CI, but every landed `main` SHA is still required by repository workflow policy to run full exact-main CI afterward.

Markdown policy does not itself configure repository settings. When protection state matters, verify the effective GitHub configuration rather than assuming policy text proves enforcement.
