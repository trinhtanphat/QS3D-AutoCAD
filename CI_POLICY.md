# CI and integration policy

This file is the repository-level source of truth for CI ownership after multi-agent work.

## Final-tree rule

CI evidence is meaningful only for the exact tree it tested. A green run for an older commit does not prove a newer `main`.

Canonical progression:

```text
CLAIM_VISIBLE
  -> AGENT_BRANCHES_READY
  -> INTEGRATION_BRANCH
  -> INTEGRATION_REVIEW
  -> ONE_FINAL_MERGE_TO_MAIN
  -> EXACT-MAIN CI
  -> CI_GREEN
  -> ALL_DONE
```

Normal implementation agents do not land implementation directly to `main`. The same rule applies to an agent/session designated to run or repair CI.

## CI recovery

When CI is red:

1. identify the exact failing run and exact tested SHA;
2. diagnose the root cause against current source rather than changing expectations merely to silence a test;
3. reserve the repair lane if it is not already owned;
4. implement the repair on `recovery/<agent>/<scope>` or `agent/<agent>/<scope>`;
5. add/retain deterministic regression coverage;
6. integrate the repair into the current `integration/<batch-id>` candidate or a dedicated recovery integration branch;
7. refresh current `main`, perform one reviewed final landing, and run/observe CI for that new exact tree;
8. repeat only from the newest relevant failure until the current candidate is green.

**CI authorization is not direct-main authorization.** Do not commit/push source, tests, scripts, workflows, packaging or release fixes straight to `main` merely because the session owns the CI loop.

## Evidence boundaries

- Source/Core CI is not native AutoCAD runtime proof.
- Native AutoCAD qualification, installer execution, signing and licensed-host evidence remain separate when required.
- Never weaken a product/architecture/security/release guard solely to obtain a green result.

## GitHub protection

Repository policy should be backed by GitHub branch protection/rulesets for `main` when available: require the intended PR/integration path, block force-push and branch deletion, and require appropriate status checks. Repository Markdown cannot itself configure those GitHub settings.
