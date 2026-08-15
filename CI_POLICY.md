# CI and integration policy

This file is the repository-level source of truth for CI ownership after multi-agent work.

## Per-agent task CI

`.github/workflows/ci.yml` is the canonical automatic task-validation workflow. It runs on pushes to `agent/**`, `recovery/**`, `integration/**`, pull requests targeting `main`, pushes to `main`, and manual dispatch.

Multiple agents share the workflow definition but every run validates its own exact branch/PR head SHA. Ten independent task branches therefore have ten independent CI results.

A GitHub Issue is coordination only; it has no source tree to build. CI evidence belongs to the branch/PR SHA referenced by the Issue.

## Mandatory completion gate

An implementation agent must not report a task completed or stop as completed until the required CI run for the **exact current branch/PR head SHA** has conclusion `success`.

A green run for an older SHA, another branch, another PR, or `main` does not count. Any new task commit invalidates earlier green evidence for completion.

If CI fails, keep the lane active, fix the real defect on `agent/<agent>/<scope>` or `recovery/<agent>/<scope>`, push a new SHA and repeat. Never weaken architecture, release-security, packaging, compatibility or test guards merely to obtain green status.

Native AutoCAD runtime evidence that cannot be proven by repository-safe CI remains an explicit environment gate; remote CI success must never be relabeled as native PASS.

## Final-tree rule

Canonical progression:

```text
CLAIM_VISIBLE
  -> AGENT_BRANCH
  -> EXACT-HEAD CI
  -> CI_GREEN
  -> PR_READY
  -> INTEGRATION_BRANCH
  -> EXACT-INTEGRATION CI
  -> CI_GREEN
  -> ONE_AUTHORIZED_MERGE_TO_MAIN
  -> EXACT-MAIN CI
  -> CI_GREEN
  -> ALL_DONE
```

CI success is a completion/quality gate, not direct-main authorization.

## Integration

For a multi-agent batch, combine participating work on `integration/<batch-id>`, require green CI for the exact integration head, then perform the reviewed authorized landing. Require green CI again for the exact resulting `main` SHA before reporting `ALL MERGED TO MAIN`.

## Release/native boundary

Release and native-package workflows are not substitutes for per-agent task CI. Keep publication/signing/native qualification semantics separate from ordinary branch/PR validation. Use `.github/workflows/ci.yml` as the mandatory task-completion evidence unless the task explicitly requires an additional licensed/native gate.

## Evidence boundaries

- Source/Core/host build CI is not licensed native AutoCAD runtime proof.
- Native AutoCAD qualification, installer execution, signing and licensed-host evidence remain separate when required.
- Do not promote engineering package artifacts to production/native PASS without their exact acceptance evidence.

## GitHub protection

Repository settings should require the stable `QS3D AutoCAD CI / core-host-and-guards` status for PRs to `main`, require the intended PR/integration path, block force-push and branch deletion, and keep bypass narrow.
