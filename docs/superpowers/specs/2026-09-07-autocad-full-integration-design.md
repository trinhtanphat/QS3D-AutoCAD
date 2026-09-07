# AutoCAD full integration design — 2026-09-07

Issue: #84
Baseline: `main@e07a77b5cb106ea76d147f0b96b3434db36f896c`
Carrier: `integration/autocad-full-parity-20260907-v1`

## Goal

Produce one current-main integration candidate that combines the source-ready UI modernization (#55), Cubicost/MEP parity adapter (#59), MEP review/profile UX (#61), and AutoCAD 2021-2027 compatibility/native-acceptance work (#83), without reverting current-main release/governance hardening.

## Chosen integration strategy

Use PR #83 head as the carrier base because it is a descendant of current main and already contains all September governance/release-chain changes plus compatibility hardening. Overlay only the top-of-stack source deltas represented by PR #61 (which already contains #55 and #59), then resolve the two true semantic overlaps explicitly:

1. `.github/workflows/ci.yml`: preserve current-main classifier, main-safe concurrency and release-chain guard; add recursive `QS3D-Platform` checkout/exact pin and the two MEP validation steps.
2. `bundle/QS3D.bundle/PackageContents.xml`: preserve `R24.0-R24.3` / `QS3D AutoCAD 2021-2024` while adding all six MEP DemandLoad commands to every payload entry.

All other #61 changed files are non-overlapping with the post-August main hardening and can be carried at their exact proven blob versions.

## Invariants

- Current-main governance/release files stay from September main/#83.
- Exactly three physical AutoCAD payloads remain.
- `external/QS3D-Platform` stays pinned at `e029d4ba0de6ffe80575f7aed96affa1db1b9b33`.
- MEP source, profile provider and modern workspace UI come from exact #61 head `cef52d0d468aef3696ab7e627a17d1140ef3a45f`.
- Compatibility/native-acceptance tooling comes from exact #83 head `3d6c708df766b0879cc8b6c82cbd952fc811902d`.
- Hosted CI is source/package evidence only; it cannot create native AutoCAD PASS.
- No main merge in this lane without separate owner authorization.

## Validation

A single fresh exact-head integration CI must pass Core, all three host builds, both MEP guards, architecture/bundle, release security, main release-chain, native acceptance, operational readiness, packaging/provenance, installer runtime, fail-closed native evidence and signing plumbing. A fresh PR-triggered run on the same final SHA is required before handoff.
