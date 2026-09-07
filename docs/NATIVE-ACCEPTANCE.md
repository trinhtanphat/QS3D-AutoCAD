# Native AutoCAD acceptance

Hosted CI proves source/API/package contracts. It does **not** prove that QS3D runs correctly inside licensed AutoCAD.

The default formal release matrix remains AutoCAD 2025, 2026 and 2027. AutoCAD 2021, 2022, 2023 and 2024 are supported through a separate legacy evidence matrix because they share the R24.0-R24.3 / .NET Framework 4.8 payload family. The same fail-closed rules apply to both matrices: source compilation or packaged DLL presence never creates native PASS evidence.

AutoCAD 2026 has an additional runtime boundary. QS3D ships the existing .NET 8-targeted 2025-2026 payload. Depending on AutoCAD 2026 update level, the real host may report CLR major 8 or 10. Native evidence must record the concrete CLR observed after QS3D loads; hosted CI must not infer that result from the target framework.

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

For legacy qualification use the same exact candidate and create a separate session for every R24.x host you intend to qualify:

```powershell
./scripts/new-native-acceptance.ps1 -Version 0.1.0 -HostGeneration 2021 -AcadExe 'C:\Program Files\Autodesk\AutoCAD 2021\acad.exe' -Operator 'Your Name'
./scripts/new-native-acceptance.ps1 -Version 0.1.0 -HostGeneration 2022 -AcadExe 'C:\Program Files\Autodesk\AutoCAD 2022\acad.exe' -Operator 'Your Name'
./scripts/new-native-acceptance.ps1 -Version 0.1.0 -HostGeneration 2023 -AcadExe 'C:\Program Files\Autodesk\AutoCAD 2023\acad.exe' -Operator 'Your Name'
./scripts/new-native-acceptance.ps1 -Version 0.1.0 -HostGeneration 2024 -AcadExe 'C:\Program Files\Autodesk\AutoCAD 2024\acad.exe' -Operator 'Your Name'
```

By default these write files such as:

```text
artifacts/native-acceptance/AutoCAD-2021.json
artifacts/native-acceptance/AutoCAD-2022.json
artifacts/native-acceptance/AutoCAD-2023.json
artifacts/native-acceptance/AutoCAD-2024.json
artifacts/native-acceptance/AutoCAD-2025.json
artifacts/native-acceptance/AutoCAD-2026.json
artifacts/native-acceptance/AutoCAD-2027.json
```

The session creator verifies release provenance first, maps 2021-2024 to the single legacy provenance payload, records the exact source SHA/artifact hashes, reads the real `acad.exe` product/file version and creates every native check as `pending`. It never creates a passing check.

## Record the runtime observed inside AutoCAD

After `QS3D` has actually loaded, run `QS3DABOUT` and record the CLR version it reports.

AutoCAD 2021-2024 should report the .NET Framework CLR 4 family, commonly a value such as `4.0.30319.42000`:

```powershell
./scripts/record-native-runtime.ps1 `
  -EvidencePath artifacts/native-acceptance/AutoCAD-2024.json `
  -ObservedClrVersion 4.0.30319.42000 `
  -Notes 'QS3DABOUT in the tested AutoCAD 2024 session reported CLR 4.0.30319.42000.'
```

AutoCAD 2025 should report CLR major 8:

```powershell
./scripts/record-native-runtime.ps1 `
  -EvidencePath artifacts/native-acceptance/AutoCAD-2025.json `
  -ObservedClrVersion 8.0.22 `
  -Notes 'QS3DABOUT in the tested AutoCAD 2025 session reported CLR 8.0.22.'
```

For AutoCAD 2026, record the actual value. CLR major 8 is valid for the original .NET 8 host; CLR major 10 is also valid for updated 2026 hosts such as the 2026.1.2+ runtime transition. This runtime check proves only that the real host/runtime family matches the supported transition boundary; all other native checks still have to pass.

```powershell
./scripts/record-native-runtime.ps1 `
  -EvidencePath artifacts/native-acceptance/AutoCAD-2026.json `
  -ObservedClrVersion 10.0.0 `
  -Notes 'QS3DABOUT in this updated AutoCAD 2026 session reported CLR 10.0.0; exact product/file version is stored in the evidence file.'
```

AutoCAD 2027 must report CLR major 10.

The recorder marks `runtime_identity` pass only when AutoCAD 2021-2024 reports CLR major 4, AutoCAD 2025 reports major 8, AutoCAD 2026 reports major 8 or 10, and AutoCAD 2027 reports major 10. A mismatch is recorded as failure and the script exits with an error.

## Record explicit PASS/FAIL results

Use the stable check ids from `native-acceptance/required-checks.json`. Each non-pending result requires evidence notes:

```powershell
./scripts/record-native-result.ps1 `
  -EvidencePath artifacts/native-acceptance/AutoCAD-2024.json `
  -CheckId bundle_command_autoload `
  -Status pass `
  -Notes 'Fresh AutoCAD 2024 start; typed QS3D; the R24.x legacy bundle payload lazy-loaded and workspace opened without NETLOAD.'
```

Use `fail` for an observed defect and `blocked` when the environment cannot execute the check. Do not mark a check pass merely because hosted CI compiled related source.

Required coverage includes installer exactness, bundle autoload, palette startup, runtime identity, all modelling/edit/BOQ flows, JIG previews/dimensions/cancel safety, browser synchronization, Level/Grid dependency behavior, undo/redo, save/reopen persistence, restart behavior, upgrade/uninstall, artifact provenance, Ribbon/native visual gates, and every required MEP surface below.

### Required MEP acceptance rows

For every host session being qualified, record all nine MEP rows explicitly; none may be inferred from hosted MEP source guards:

- `mep_takeoff_recognition_quantity` — run `QS3DMEPTAKEOFF` on representative supported MEP geometry and verify recognition, quantity/unit parity, plus fail-closed unsupported or ambiguous handling.
- `mep_clash_classification` — run `QS3DMEPCLASH` on representative hard and clearance cases and verify deterministic pair/type/severity/geometry classification.
- `mep_clash_locate_selection_safety` — run `QS3DMEPCLASHLOCATE` for an exact pair and verify PICKFIRST selection; stale, unknown, ambiguous or partially resolved references must fail closed without replacing prior valid selection or review state.
- `mep_exact_clash_review` — run `QS3DMEPEXACTCLASH` and compare native-geometry clash results with representative known cases.
- `mep_zoom_selection_review` — run `QS3DMEPZOOMSELECTION` and verify only the intended visible/selected objects in the owning drawing are affected.
- `mep_review_palette_lifecycle` — run `QS3DMEPREVIEW`, exercise the modeless review palette, and verify focus/lifecycle behavior and active-document affinity.
- `mep_recognition_profile_persistence` — edit/save a recognition profile, restart AutoCAD, verify persistence, and verify invalid/corrupt profile input fails closed within documented bounds.
- `mep_multidwg_document_affinity` — with multiple drawings open, verify takeoff, clash, Locate, review and profile operations do not leak selection/results across DWGs.
- `mep_readonly_nonmutation` — verify analysis/review/Locate/profile-read paths do not create or mutate unrelated DWG entities, QS3D XData/XRecords, semantic project state, or unintended sidecar/project state.

Use `scripts/record-native-result.ps1` for each row with concrete notes from the licensed host session. A fully passing evidence file must contain every required check exactly once; leaving a MEP row pending, blocked, failed or absent causes final validation to reject the session.

## Ribbon acceptance procedure

The source uses a runtime bridge instead of a compile-time `AdWindows.dll` reference. Hosted CI can prove only that the bridge compiles without importing `Autodesk.Windows`; it cannot prove the installed AutoCAD UI assembly exposes the expected runtime types/properties.

For every native host being qualified, including any legacy 2021-2024 host:

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

Validate the complete AutoCAD 2021-2024 legacy matrix separately:

```powershell
./scripts/validate-native-acceptance.ps1 `
  -Version 0.1.0 `
  -RequiredGenerations @('2021','2022','2023','2024')
```

You may request an individual legacy generation when intentionally qualifying only that host, but that must not be represented as qualification of the other legacy generations.

The validator refuses a requested generation set unless:

- exactly one evidence session exists for every requested host generation;
- all evidence files bind to the exact same requested release version/source SHA/artifact hashes;
- expected runtime-family labels and observed CLR majors match the generation rules, including AutoCAD 2026 major 8-or-10 handling;
- every required check exists exactly once;
- every required check is explicitly `pass` with non-empty notes and a timestamp;
- every session has a distinct session id.

Only after those rules pass does it write:

```text
artifacts/native-acceptance/NATIVE-ACCEPTANCE-SUMMARY.json
artifacts/native-acceptance/NATIVE-ACCEPTED-SHA.txt
```

These are **review artifacts**, not an automatic release authorization. The validator does not change GitHub variables, create tags, publish releases or set `QS3D_NATIVE_ACCEPTED_SHA`. The default production release policy still depends on the intentionally selected formal release matrix and release-owner review; a standalone or partial legacy PASS must not silently replace the required 2025/2026/2027 production evidence.

## Hosted-CI boundary

CI runs `validate-native-acceptance-contract.ps1` to parse/guard the evidence tooling and `test-native-acceptance-failclosed.ps1` to prove that synthetic sessions with `pending` checks are rejected. The fail-closed smoke covers both the default 2025/2026/2027 matrix and the complete separate AutoCAD 2021/2022/2023/2024 legacy matrix. The synthetic 2026 case exercises the CLR-10 transition path but remains `pending`, so hosted CI still cannot create native PASS evidence.

## Evidence handling

Evidence contains operator/machine information and local AutoCAD paths. Generated sessions live under ignored `artifacts/` by default. Attach/share them only through the release or issue process appropriate for the project; do not silently commit workstation-specific evidence into source history.
