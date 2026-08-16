# Pull request

## Scope

Issue: <!-- #123; reuse the existing task issue instead of creating a duplicate -->
Task branch: <!-- agent/<agent-id>/<scope>, recovery/<agent-id>/<scope>, or integration/<batch-id> when explicitly authorized -->
Baseline `main` SHA: <!-- exact 40-hex SHA used to start/reconcile this work -->
Head SHA: <!-- exact candidate SHA validated by applicable CI -->
Pre-push remote task-branch SHA: <!-- exact remote head observed immediately before the final fast-forward-safe push -->

Summarize the behavior changed and the user-visible reason for the change. Keep unrelated work out of this PR.

## Multi-agent safety

- [ ] I performed only the minimum collision check needed and did not take over another active lane.
- [ ] This PR stays inside the registered Issue/claim scope and exclusions.
- [ ] I refreshed current `main` before final handoff and reconciled any relevant landed work without silently dropping it.
- [ ] I verified the remote task-branch head immediately before the final push and did not overwrite a concurrent commit.
- [ ] The final diff contains no accidental reversions, duplicate competing implementations or unresolved merge markers.
- [ ] I did not use `ours`/`theirs` blindly or force/reset shared `main` or a published task branch.

## Validation

- [ ] CI-neutral-only: every changed path is in the documented ignore set; lightweight validation/path classification is recorded below.
- [ ] Implementation-relevant: branch/PR CI is successful on the exact current head SHA required by `CI_POLICY.md`.
- [ ] Any failure was fixed at its root cause; no assertion/check/security/release guard was weakened merely to get green.

Validation evidence:
<!-- exact run/check IDs, tested SHA, local/static/unit/smoke/preflight results -->

## AutoCAD runtime evidence

Target: <!-- AutoCAD 2021 / AutoCAD 2025-2026 / AutoCAD 2027 / Core-only / docs-only / not applicable -->
Licensed runtime status: <!-- PASS with exact evidence, PENDING_NATIVE, or not applicable -->

- [ ] I did not claim native AutoCAD runtime PASS from source review or hosted CI alone.
- [ ] If native/runtime validation is still required, this PR explicitly says `PENDING_NATIVE` and identifies what remains.

## Commit/push handoff

- [ ] The actual repository change is committed and pushed on the task branch; a ZIP/installer/artifact is not being used as a substitute for source/docs commit/push.
- [ ] The Issue/PR records the exact branch and head SHA.
- [ ] Related implementation, regression guards, docs and handoff changes are grouped into coherent request/lane-scoped commits.

## Merge authorization

A green PR does **not** authorize its own merge. Normal agents stop before `main`; merge/integration requires explicit owner authorization under `docs/MAIN-WRITE-AUTHORIZATION.md`.

If this is an authorized integration PR, name the owner authorization and exact participating Issues/PRs/branches/SHAs here:
<!-- integration authorization / batch inventory -->
