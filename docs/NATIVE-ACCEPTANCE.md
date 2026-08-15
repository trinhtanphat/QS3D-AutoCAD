# Native AutoCAD acceptance

Hosted CI proves source/API/package contracts. It does **not** prove that QS3D runs correctly inside licensed AutoCAD.

The default formal release matrix remains AutoCAD 2025, 2026 and 2027. AutoCAD 2021 is supported through a separate legacy evidence lane because it uses the R24.0 / .NET Framework 4.8 payload. The same fail-closed rules apply to both lanes: source compilation or packaged DLL presence never creates native PASS evidence.

## Preconditions

1. Start from one exact clean commit that has green hosted CI.
2. Produce one candidate with `scripts/package.ps1 -Version <version>` and verify it with `scripts/verify-artifacts.ps1 -Version <version>`.
3. Keep that same `artifacts/RELEASE-PROVENANCE.json`, ZIP and Setup.exe throughout a qualification lane. Rebuilding or changing source invalidates that evidence chain.
4. Use real installed/licensed AutoCAD. Do not substitute mocked Autodesk assemblies or manual `NETLOAD` for bundle discovery/autoload tests.

A signed candidate can be required by passing `-RequireSignedCandidate` to the session creator/final validator. Engineering qualification may be performed on an unsigned candidate, but that does not count as production Authenticode evidence.

## Create evidence sessions

Close AutoCAD before Setup operations. For the default modern matrix create one session per installed `acad.exe`:

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

For an AutoCAD 2021 legacy qualification use the same exact candidate and create a separate session:

```powershell
./scripts/new-native-acceptance.ps1 `
  -Version 0.1.0 `
  -HostGeneration 2021 `
  -AcadExe 'C:\Program Files\Autodesk\AutoCAD 2021\acad.exe' `
  -Operator 'Your Name'
```

By default these write files such as:

```text
artifacts/native-acceptance/AutoCAD-2021.json
artifacts/native-acceptance/AutoCAD-2025.json
artifacts/native-acceptance/AutoCAD-2026.json
artifacts/native-acceptance/AutoCAD-2027.json
```

The session creator verifies release provenance first, records the exact source SHA/artifact hashes, reads the real `acad.exe` product/file version and creates every native check as `pending`. It never creates a passing check.

## Record the runtime observed inside AutoCAD

After `QS3D` has actually loaded, run `QS3DABOUT` and record the CLR version it reports.

AutoCAD 2021 should report the .NET Framework CLR 4 family, commonly a value such as `4.0.30319.42000`:

```powershell
./scripts/record-native-runtime.ps1 `
  -EvidencePath artifacts/native-acceptance/AutoCAD-2021.json `
  -ObservedClrVersion 4.0.30319.42000 `
  -Notes 'QS3DABOUT in the tested AutoCAD 2021 session reported CLR 4.0.30319.42000.'
```

Example modern session:

```powershell
./scripts/record-native-runtime.ps1 `
  -EvidencePath artifacts/native-acceptance/AutoCAD-2025.json `
  -ObservedClrVersion 8.0.22 `
  -Notes 'QS3DABOUT in the tested AutoCAD 2025 session reported CLR 8.0.22.'
```

The recorder marks `runtime_identity` pass only when AutoCAD 2021 reports CLR major 4, AutoCAD 2025/2026 reports major 8 and AutoCAD 2027 reports major 10. A mismatch is recorded as failure and the script exits with an error.

## Record explicit PASS/FAIL results

Use the stable check ids from `native-acceptance/required-checks.json`. Each non-pending result requires evidence notes:

```powershell
./scripts/record-native-result.ps1 `
  -EvidencePath artifacts/native-acceptance/AutoCAD-2021.json `
  -CheckId bundle_command_autoload `
  -Status pass `
  -Notes 'Fresh AutoCAD 2021 start; typed QS3D; the R24.0 bundle payload lazy-loaded and workspace opened without NETLOAD.'
```

Use `fail` for an observed defect and `blocked` when the environment cannot execute the check. Do not mark a check pass merely because hosted CI compiled related source.

Required coverage includes installer exactness, bundle autoload, palette startup, runtime identity, all modelling/edit/BOQ flows, JIG previews/dimensions/cancel safety, browser synchronization, Level/Grid dependency behavior, undo/redo, save/reopen persistence, restart behavior, upgrade/uninstall, artifact provenance, and Ribbon/native visual gates.

## Ribbon acceptance procedure

The source uses a runtime bridge instead of a compile-time `AdWindows.dll` reference. Hosted CI can prove only that the bridge compiles without importing `Autodesk.Windows`; it cannot prove the installed AutoCAD UI assembly exposes the expected runtime types/properties.

For every native host being qualified, including the separate AutoCAD 2021 lane when it is tested:

1. Start AutoCAD fresh with the exact installed candidate and confirm the normal `QS3D` palette/workspace opens.
2. Run `QS3DRIBBON`.
3. Confirm the command reports the QS3D Ribbon as ready/created; a message saying Ribbon is unavailable is a native failure for `ribbon_surface`, even though the palette remains usable.
4. Confirm exactly one visible `QS3D` tab exists after running `QS3DRIBBON` repeatedly; no duplicate tabs/panels should be created.
5. Confirm the tab exposes the intended **Model**, **References**, and **Review** command groups.
6. Exercise representative buttons from every group and confirm they dispatch the intended QS3D command. At minimum test a modelling command, Level/Grid reference command, `QS3D`, `QS3DEDIT`, `QS3DBOQ`, and `QS3DABOUT`.
7. Switch AutoCAD workspace/theme where applicable and confirm Ribbon reconciliation still works without breaking model commands.
8. Test the supported high-DPI configuration(s) used for that qualification and check label visibility, clipping, button usability and docked QS3D workspace interaction.

Record results explicitly with `record-native-result.ps1`. `ribbon_surface` and `ribbon_visual_qa` remain mandatory for a fully passing host session. A source compile, reflection type strings, or successful palette launch is **not** Ribbon native acceptance.

## Final validation

For the default production qualification, when AutoCAD 2025, 2026 and 2027 have all been tested against the same candidate:

```powershell
./scripts/validate-native-acceptance.ps1 -Version 0.1.0
```

The default validator therefore continues to require exactly one 2025, 2026 and 2027 session.

Validate the AutoCAD 2021 legacy lane separately:

```powershell
./scripts/validate-native-acceptance.ps1 `
  -Version 0.1.0 `
  -RequiredGenerations 2021
```

The validator refuses a requested generation set unless:

- exactly one evidence session exists for every requested host generation;
- all evidence files bind to the exact same requested release version/source SHA/artifact hashes;
- observed CLR majors match each generation's expected runtime family;
- every required check exists exactly once;
- every required check is explicitly `pass` with non-empty notes and a timestamp;
- every session has a distinct session id.

Only after those rules pass does it write:

```text
artifacts/native-acceptance/NATIVE-ACCEPTANCE-SUMMARY.json
artifacts/native-acceptance/NATIVE-ACCEPTED-SHA.txt
```

These are **review artifacts**, not an automatic release authorization. The validator does not change GitHub variables, create tags, publish releases or set `QS3D_NATIVE_ACCEPTED_SHA`. The default production release policy still depends on the intentionally selected formal release matrix and release-owner review; a standalone AutoCAD 2021 legacy PASS must not silently replace the required 2025/2026/2027 production evidence.

## Hosted-CI boundary

CI runs `validate-native-acceptance-contract.ps1` to parse/guard the evidence tooling and `test-native-acceptance-failclosed.ps1` to prove that synthetic sessions with `pending` checks are rejected. The fail-closed smoke covers both the default 2025/2026/2027 matrix and a separate AutoCAD 2021 session. Synthetic rejection tests never create PASS evidence and are not native runtime evidence.

## Evidence handling

Evidence contains operator/machine information and local AutoCAD paths. Generated sessions live under ignored `artifacts/` by default. Attach/share them only through the release or issue process appropriate for the project; do not silently commit workstation-specific evidence into source history.
