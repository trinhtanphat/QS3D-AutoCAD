# AutoCAD full integration implementation plan — 2026-09-07

**Goal:** integrate #55 + #59 + #61 + #83 onto current main without losing September release/governance hardening.

## Task 1 — Build the current-main carrier

- Start from #83 head `3d6c708df766b0879cc8b6c82cbd952fc811902d`, which is based on current main.
- Add `.gitmodules` and gitlink `external/QS3D-Platform@e029d4ba0de6ffe80575f7aed96affa1db1b9b33` from #61.
- Carry the exact #61 source blobs for modern workspace UI, MEP commands/profile UI, AutoCAD project reference, MEP validators, MEP docs and MEP-aware packaging.

## Task 2 — Resolve the only semantic overlaps

- Merge CI so it keeps current-main `classify`, mandatory full main CI, main-safe non-cancelling concurrency and `validate-main-ci-release-chain.ps1`, while adding recursive submodule checkout, exact Platform pin verification and both MEP guards.
- Merge bundle manifest so legacy discovery remains exactly `R24.0-R24.3` / `QS3D AutoCAD 2021-2024`, modern ranges remain unchanged, and all six MEP commands are DemandLoad triggers in all three payloads.
- Preserve #83 native-acceptance/tooling/docs rather than restoring stale August versions.

## Task 3 — Exact integration verification

- Push one integration commit with parents/current provenance documented.
- Require fresh integration-branch CI on the exact SHA.
- On failure, diagnose the first actual failing gate and patch only the integration carrier.
- Revalidate current main before opening the PR; if relevant main drift occurs, reconcile and rerun exact-head CI.

## Task 4 — Final PR handoff

- Open one final PR from `integration/autocad-full-parity-20260907-v1` to `main`.
- Require fresh PR-triggered CI on the exact final SHA.
- Record the exact SHA/run IDs and state that native AutoCAD qualification remains external.
- Do not merge `main` without separate explicit owner authorization.
