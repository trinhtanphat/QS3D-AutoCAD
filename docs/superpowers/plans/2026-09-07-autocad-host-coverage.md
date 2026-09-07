# AutoCAD Host Coverage 2021-2027 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing QS3D AutoCAD bundle discoverable on AutoCAD 2021-2024 with the existing legacy net48 payload and make native acceptance truthful for every AutoCAD generation 2021-2027, including the AutoCAD 2026 .NET 8/.NET 10 host-runtime transition.

**Architecture:** Keep the existing three physical payload families and broaden only the legacy bundle range from `R24.0` to `R24.0-R24.3`. Expand native-acceptance generations to 2021-2027, map 2021-2024 to the existing legacy provenance payload, and treat AutoCAD 2026's host runtime as `.NET 8 or .NET 10` while continuing to validate that the shipped 2026 payload is the existing .NET 8 build.

**Tech Stack:** PowerShell 7, Autodesk AutoCAD `.bundle` manifest XML, JSON Schema/native evidence JSON, .NET Framework 4.8, .NET 8, .NET 10, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-07-autocad-host-coverage-design.md`

## Global Constraints

- Do not add `Autodesk.*` references or types to `src/QS3D.Core`.
- Preserve exactly three physical payloads: legacy net48, 2025-2026 net8, 2027 net10.
- Legacy bundle range must be exactly `R24.0-R24.3`.
- Modern bundle ranges remain exactly `R25.0-R25.1` and `R26.0-R26.0`.
- AutoCAD 2026 may run the shipped .NET 8-targeted payload under an observed host CLR major 8 or 10; the observed CLR must be recorded from the real host.
- Hosted CI must never create native PASS evidence.
- Do not touch UI modernization PR #55 or MEP PRs #59/#61.
- Do not modify `src/QS3D.AutoCAD/QS3D.AutoCAD.csproj` unless a failing source/build contract proves it necessary.
- No merge to `main` and no release publication in this lane.

---

### Task 1: Lock the new compatibility contract with failing guards

**Files:**
- Modify: `scripts/validate-architecture.ps1`
- Modify: `scripts/validate-native-acceptance-contract.ps1`

**Interfaces:**
- Consumes: current `PackageContents.xml`, native schema/tooling.
- Produces: deterministic guards that require `R24.0-R24.3` and generation support `2021` through `2027`.

- [ ] **Step 1: Change the bundle guard to require the new legacy range**

Replace the existing legacy range assertion:

```powershell
if ($null -eq $net48 -or $net48.RuntimeRequirements.SeriesMax -ne 'R24.0') { throw 'AutoCAD 2021 runtime range must be R24.0.' }
```

with:

```powershell
if ($null -eq $net48 -or $net48.RuntimeRequirements.SeriesMax -ne 'R24.3') {
    throw 'AutoCAD 2021-2024 legacy runtime range must be exactly R24.0-R24.3.'
}
```

Also require the legacy component entry label to identify `AutoCAD 2021-2024` while keeping its module path `./Contents/2021/QS3D.AutoCAD.dll`.

- [ ] **Step 2: Expand native contract guard expectations before changing the implementation**

In `validate-native-acceptance-contract.ps1`, require:

```powershell
foreach ($generation in @('2021','2022','2023','2024','2025','2026','2027')) { ... }
```

and update script-snippet assertions to require:

```powershell
ValidateSet('2021','2022','2023','2024','2025','2026','2027')
```

plus 2026 dual-runtime handling snippets.

- [ ] **Step 3: Commit/push guard-only RED**

Commit message:

```text
test(compat): require AutoCAD 2021-2027 host matrix #82
```

Push the task branch. The exact-head CI is expected to fail in `Architecture and bundle guards` and/or `Native acceptance contract guards` because implementation files still contain the old R24.0-only and 2021/2025/2026/2027-only contract.

- [ ] **Step 4: Verify RED is for the intended compatibility drift**

Inspect the exact workflow run/job. Accept RED only when the failure is caused by the newly tightened host-matrix guard. Any unrelated failure must be diagnosed separately before proceeding.

---

### Task 2: Broaden bundle discovery and native acceptance generation/runtime handling

**Files:**
- Modify: `bundle/QS3D.bundle/PackageContents.xml`
- Modify: `native-acceptance/evidence.schema.json`
- Modify: `scripts/new-native-acceptance.ps1`
- Modify: `scripts/record-native-runtime.ps1`
- Modify: `scripts/validate-native-acceptance.ps1`
- Modify: `scripts/show-native-acceptance-status.ps1`

**Interfaces:**
- Consumes: release provenance runtime rows (`2021` / `.NET Framework 4.8`, `2025-2026` / `.NET 8`, `2027` / `.NET 10`).
- Produces: host-generation acceptance for `2021`-`2027`; 2026 host-runtime expectation `.NET 8 or .NET 10` while package provenance remains `.NET 8`.

- [ ] **Step 1: Broaden the legacy bundle entry**

Change only the legacy component's runtime requirement and user-facing name/description:

```xml
<RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R24.0" SeriesMax="R24.3" />
<ComponentEntry AppName="QS3D AutoCAD 2021-2024" ModuleName="./Contents/2021/QS3D.AutoCAD.dll" AppDescription="QS3D AutoCAD .NET Framework 4.8 legacy host for AutoCAD 2021-2024" LoadReasons="LoadOnCommandInvocation">
```

Do not duplicate the DLL or create year-specific binaries.

- [ ] **Step 2: Expand the evidence schema generations and host runtime enum**

Use:

```json
"generation": { "enum": ["2021", "2022", "2023", "2024", "2025", "2026", "2027"] },
"expectedRuntimeFamily": { "enum": [".NET Framework 4.8", ".NET 8", ".NET 10", ".NET 8 or .NET 10"] }
```

No schema version bump is required because this is an additive enum expansion within the existing evidence shape.

- [ ] **Step 3: Expand session creation and separate package target from host runtime expectation**

Change `HostGeneration` to:

```powershell
[ValidateSet('2021','2022','2023','2024','2025','2026','2027')]
```

Compute values independently:

```powershell
$payloadRuntime = switch ($HostGeneration) {
    { $_ -in @('2021','2022','2023','2024') } { '.NET Framework 4.8'; break }
    '2027' { '.NET 10' }
    default { '.NET 8' }
}
$expectedHostRuntime = switch ($HostGeneration) {
    { $_ -in @('2021','2022','2023','2024') } { '.NET Framework 4.8'; break }
    '2026' { '.NET 8 or .NET 10' }
    '2027' { '.NET 10' }
    default { '.NET 8' }
}
```

Map provenance as:

```powershell
if ($HostGeneration -in @('2021','2022','2023','2024')) {
    $_.autoCAD -eq '2021' -and $_.managedRuntime -eq $payloadRuntime
}
elseif ($HostGeneration -eq '2027') {
    $_.autoCAD -eq '2027' -and $_.managedRuntime -eq $payloadRuntime
}
else {
    $_.autoCAD -eq '2025-2026' -and $_.managedRuntime -eq $payloadRuntime
}
```

Store `expectedRuntimeFamily = $expectedHostRuntime` in evidence.

- [ ] **Step 4: Make CLR recording accept only the correct majors for each generation**

Use:

```powershell
$allowedMajors = switch ([string]$evidence.host.generation) {
    { $_ -in @('2021','2022','2023','2024') } { @(4); break }
    '2025' { @(8) }
    '2026' { @(8, 10) }
    '2027' { @(10) }
    default { @() }
}
$status = if ($actualMajor -in $allowedMajors) { 'pass' } else { 'fail' }
```

The runtime check notes must list both allowed majors for AutoCAD 2026 and the concrete observed version.

- [ ] **Step 5: Make final native validation use the same exact matrix**

Set:

```powershell
$supportedGenerations = @('2021','2022','2023','2024','2025','2026','2027')
```

Use `.NET Framework 4.8` for 2021-2024, `.NET 8` for 2025, `.NET 8 or .NET 10` for 2026 and `.NET 10` for 2027. Validate observed CLR major against `@(4)`, `@(8)`, `@(8,10)`, `@(10)` respectively.

Keep the default production-required matrix `@('2025','2026','2027')`; legacy 2021-2024 remains a separately requested native matrix so adding compatibility does not silently expand production release policy.

- [ ] **Step 6: Expand status tooling generation support**

Update `show-native-acceptance-status.ps1` `supportedGenerations` to all seven years. Keep its default display matrix modern (`2025`,`2026`,`2027`) unless legacy generations are explicitly requested.

- [ ] **Step 7: Run focused guards locally/through branch CI**

Commands in CI:

```powershell
./scripts/validate-architecture.ps1
./scripts/validate-native-acceptance-contract.ps1
```

Expected: both PASS after implementation.

---

### Task 3: Prove fail-closed behavior for the full legacy range and document the matrix

**Files:**
- Modify: `scripts/test-native-acceptance-failclosed.ps1`
- Modify: `README.md`
- Modify: `docs/BUILD.md`
- Modify: `docs/NATIVE-ACCEPTANCE.md`
- Modify: `docs/superpowers/specs/2026-09-07-autocad-host-coverage-design.md` only if implementation findings require a factual correction.

**Interfaces:**
- Consumes: generation/runtime rules from Task 2.
- Produces: synthetic rejection coverage for all host generations and user/developer documentation matching the shipped bundle.

- [ ] **Step 1: Expand fail-closed legacy smoke to 2021-2024**

Create synthetic pending evidence for each legacy generation:

```powershell
$legacyPaths = @()
foreach ($generation in @('2021','2022','2023','2024')) {
    $path = Join-Path $root "AutoCAD-$generation.json"
    $evidence = New-SyntheticPendingEvidence -Generation $generation -Runtime '.NET Framework 4.8' -Clr '4.0.30319.42000'
    $evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding utf8NoBOM
    $legacyPaths += $path
}
Assert-PendingRejected -EvidencePaths $legacyPaths -RequiredGenerations @('2021','2022','2023','2024')
```

For the modern synthetic 2026 row, use `expectedRuntimeFamily = '.NET 8 or .NET 10'`; one synthetic run can use CLR 10 to exercise the transition path while remaining pending and therefore rejected.

- [ ] **Step 2: Update README support statement**

Document:

```text
AutoCAD 2021-2024: one legacy .NET Framework 4.8 payload built against the AutoCAD 2021 managed SDK, bundle series R24.0-R24.3.
AutoCAD 2025: .NET 8 payload.
AutoCAD 2026: same .NET 8-targeted payload; native acceptance records whether the actual host CLR is .NET 8 or .NET 10 (2026.1.2+ transition).
AutoCAD 2027: .NET 10 payload.
```

Do not claim any newly added generation native-qualified without real-host evidence.

- [ ] **Step 3: Update BUILD and NATIVE-ACCEPTANCE docs**

State that packaging still builds only three payloads and explain why 2022-2024 reuse the 2021 SDK binary. Update native acceptance examples so legacy qualification may request:

```powershell
-RequiredGenerations @('2021','2022','2023','2024')
```

and AutoCAD 2026 evidence must record the concrete observed CLR after the plugin loads.

- [ ] **Step 4: Commit/push implementation GREEN**

Commit message:

```text
feat(compat): support AutoCAD 2021-2024 legacy hosts #82
```

Before push, confirm the remote task branch still points at the last SHA observed from this session; do not overwrite unexpected concurrent writes.

- [ ] **Step 5: Require exact-head canonical CI SUCCESS**

The canonical workflow must pass at the exact current branch SHA, including:

```text
Build Core
Smoke .NET 8
Smoke .NET 10
Build AutoCAD 2021 host (legacy net48 payload)
Build AutoCAD 2025-2026 host
Build AutoCAD 2027 host
Architecture and bundle guards
Release security policy guards
Main CI/release chain guards
Native acceptance contract guards
Operational readiness tooling guards
Package and verify release contract
Setup install/uninstall runtime smoke
Native acceptance rejection smoke
Authenticode signing plumbing smoke
```

If red, inspect the newest failing job log, fix only issue #82 scope, push a new exact SHA and repeat until green.

- [ ] **Step 6: Refresh main and reconcile if needed**

Fetch current `main`. If it moved, compare only issue #82 relevant paths. If no overlap, preserve the branch candidate. If relevant main moved, reconcile safely on the task branch, push a descendant commit and require fresh exact-head CI.

- [ ] **Step 7: Open PR and update Issue #82 handoff**

PR body must record exact head SHA, exact branch CI run, scope/exclusions, Autodesk compatibility basis, native evidence boundary, and `MERGED TO MAIN: NO` pending separate owner authorization.
