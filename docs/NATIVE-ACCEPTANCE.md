# Native AutoCAD acceptance

Hosted CI proves source/API/package contracts. It does **not** prove that QS3D runs correctly inside licensed AutoCAD. Native acceptance is a separate evidence workflow for AutoCAD 2025, 2026 and 2027.

## Preconditions

1. Start from one exact clean commit that has green hosted CI.
2. Produce one candidate with `scripts/package.ps1 -Version <version>` and verify it with `scripts/verify-artifacts.ps1 -Version <version>`.
3. Keep that same `artifacts/RELEASE-PROVENANCE.json`, ZIP and Setup.exe while testing all three AutoCAD generations. Rebuilding or changing source invalidates the evidence chain.
4. Use real installed/licensed AutoCAD. Do not substitute mocked Autodesk assemblies or manual `NETLOAD` for the bundle discovery/autoload tests.

A signed candidate can be required by passing `-RequireSignedCandidate` to the session creator/final validator. Engineering qualification may be performed on an unsigned candidate, but that does not count as production Authenticode evidence.

## Create one evidence session per host

Close AutoCAD before Setup operations, then create a session for each installed `acad.exe`:

```powershell
./scripts/new-native-acceptance.ps1 `
  -Version 0.1.0 `
  -HostGeneration 2025 `
  -AcadExe 'C:\Program Files\Autodesk\AutoCAD 2025\acad.exe' `
  -Operator 'Your Name'

./scripts/new-native-acceptance.ps1 `
  -Version 0.1.0 `
  -HostGeneration 2026 `
  -AcadExe 'C:\Program Files\Autodesk\AutoCAD 2026\acad.exe' `
  -Operator 'Your Name'

./scripts/new-native-acceptance.ps1 `
  -Version 0.1.0 `
  -HostGeneration 2027 `
  -AcadExe 'C:\Program Files\Autodesk\AutoCAD 2027\acad.exe' `
  -Operator 'Your Name'
```

By default these write:

```text
artifacts/native-acceptance/AutoCAD-2025.json
artifacts/native-acceptance/AutoCAD-2026.json
artifacts/native-acceptance/AutoCAD-2027.json
```

The script verifies release provenance first, records the exact source SHA/artifact hashes, reads the real `acad.exe` product/file version and creates every native check as `pending`. It never creates a passing check.

## Record the runtime observed inside AutoCAD

After `QS3D` has actually loaded, run `QS3DABOUT` and record the CLR version it reports:

```powershell
./scripts/record-native-runtime.ps1 `
  -EvidencePath artifacts/native-acceptance/AutoCAD-2025.json `
  -ObservedClrVersion 8.0.22 `
  -Notes 'QS3DABOUT in the tested AutoCAD 2025 session reported CLR 8.0.22.'
```

The recorder marks `runtime_identity` pass only when AutoCAD 2025/2026 reports CLR major 8 and AutoCAD 2027 reports CLR major 10. A mismatch is recorded as failure and the script exits with an error.

## Record explicit PASS/FAIL results

Use the stable check ids from `native-acceptance/required-checks.json`. Each non-pending result requires evidence notes:

```powershell
./scripts/record-native-result.ps1 `
  -EvidencePath artifacts/native-acceptance/AutoCAD-2025.json `
  -CheckId bundle_command_autoload `
  -Status pass `
  -Notes 'Fresh AutoCAD start; typed QS3D; bundle lazy-loaded and workspace opened without NETLOAD.'
```

Use `fail` for an observed defect and `blocked` when the environment cannot execute the check. Do not mark a check pass merely because hosted CI compiled related source.

Required coverage includes installer exactness, bundle autoload, palette startup, runtime identity, all modelling/edit/BOQ flows, browser synchronization, Level/Grid dependency behavior, undo/redo, save/reopen persistence, restart behavior, upgrade/uninstall, artifact provenance, and the Ribbon/native visual gates defined by issue #4.

## Ribbon acceptance procedure

The source uses a runtime bridge instead of a compile-time `AdWindows.dll` reference. Hosted CI can prove only that the bridge compiles without importing `Autodesk.Windows`; it cannot prove the installed AutoCAD UI assembly exposes the expected runtime types/properties.

For **each** AutoCAD 2025, 2026 and 2027 session:

1. Start AutoCAD fresh with the exact installed candidate and confirm the normal `QS3D` palette/workspace still opens.
2. Run `QS3DRIBBON`.
3. Confirm the command reports the QS3D Ribbon as ready/created; a message saying Ribbon is unavailable is a native failure for `ribbon_surface`, even though the palette remains usable.
4. Confirm exactly one visible `QS3D` tab exists after running `QS3DRIBBON` repeatedly; no duplicate tabs/panels should be created.
5. Confirm the tab exposes the intended **Model**, **References**, and **Review** command groups.
6. Exercise representative buttons from every group and confirm they dispatch the intended QS3D command. At minimum test a modelling command, Level/Grid reference command, `QS3D`, `QS3DEDIT`, `QS3DBOQ`, and `QS3DABOUT`.
7. Switch AutoCAD workspace/theme where applicable and confirm Ribbon reconciliation still works without breaking model commands.
8. Test the supported high-DPI configuration(s) used for release qualification and check label visibility, clipping, button usability and docked QS3D workspace interaction.

Record the result explicitly:

```powershell
./scripts/record-native-result.ps1 `
  -EvidencePath artifacts/native-acceptance/AutoCAD-2025.json `
  -CheckId ribbon_surface `
  -Status pass `
  -Notes 'QS3DRIBBON created one QS3D tab; Model/References/Review buttons dispatched expected commands with no duplicate tab after repeated reconciliation.'

./scripts/record-native-result.ps1 `
  -EvidencePath artifacts/native-acceptance/AutoCAD-2025.json `
  -CheckId ribbon_visual_qa `
  -Status pass `
  -Notes 'Verified supported light/dark workspace and release DPI configuration; labels/buttons remained usable with no clipping observed.'
```

`ribbon_surface` and `ribbon_visual_qa` remain mandatory. A source compile, reflection type strings, or a successful palette launch is **not** Ribbon native acceptance.

## Final validation

When all three hosts have been tested against the same candidate:

```powershell
./scripts/validate-native-acceptance.ps1 -Version 0.1.0
```

The validator refuses the candidate unless:

- exactly one AutoCAD 2025, 2026 and 2027 session exists;
- all three evidence files bind to the exact same release version/source SHA/artifact hashes;
- the observed CLR major matches the expected runtime family;
- every required check exists exactly once;
- every required check is explicitly `pass` with non-empty notes and a timestamp;
- each session has a distinct session id.

Only after those rules pass does it write:

```text
artifacts/native-acceptance/NATIVE-ACCEPTANCE-SUMMARY.json
artifacts/native-acceptance/NATIVE-ACCEPTED-SHA.txt
```

These are **review artifacts**, not an automatic release authorization. The validator does not change GitHub variables, create tags, publish releases or set `QS3D_NATIVE_ACCEPTED_SHA`. A release owner must review the native evidence before setting that repository variable to the exact full SHA.

## Hosted-CI boundary

CI runs `validate-native-acceptance-contract.ps1` to parse/guard the evidence tooling and `test-native-acceptance-failclosed.ps1` to prove that three synthetic sessions with pending checks are rejected. The synthetic rejection test never creates PASS evidence and is not native runtime evidence.

## Evidence handling

Evidence contains operator/machine information and local AutoCAD paths. Generated sessions live under ignored `artifacts/` by default. Attach/share them only through the release or issue process appropriate for the project; do not silently commit workstation-specific evidence into source history.
