# AutoCAD host coverage 2021-2027 design

Issue: #82
Baseline: `main@e07a77b5cb106ea76d147f0b96b3434db36f896c`
Branch: `agent/chatgpt-gpt56sol/autocad-host-coverage-20260907`

## Goal

Make the existing QS3D AutoCAD product discoverable on AutoCAD 2021, 2022, 2023 and 2024 using the already-built legacy .NET Framework 4.8 payload, while preserving the existing AutoCAD 2025-2026 .NET 8 payload and AutoCAD 2027 .NET 10 payload. Harden the native acceptance model so AutoCAD 2026.1.2+ (whose host moved to .NET 10) cannot be represented as native-qualified without real-host evidence.

This is a compatibility/packaging lane. It does not redesign QS3D UI, MEP behavior, modelling behavior or host-neutral Core.

## Current constraints and evidence

- Autodesk documents AutoCAD 2021-2024 as .NET Framework 4.8 hosts and lists older managed SDKs as supported by later R24.x releases; AutoCAD 2024 (`24.3`) supports the AutoCAD 2021 SDK.
- The repository already builds a dedicated AutoCAD 2021 `net48` payload and packages it under `Contents/2021`.
- The current bundle manifest restricts that payload to `R24.0` only.
- AutoCAD 2025 (`25.0`) and AutoCAD 2026 (`25.1`) use the existing .NET 8 payload.
- AutoCAD 2027 (`26.0`) uses the existing .NET 10 payload.
- Autodesk's AutoCAD 2026.1.2 update moved the host runtime to .NET 10 and states .NET 8-targeted custom applications should generally continue to function except where .NET 10 introduces an incompatibility. Therefore source/hosted CI cannot prove that update native-safe.

## Approaches considered

### A. One binary per AutoCAD year

Build separate 2021, 2022, 2023 and 2024 net48 binaries and separate 2025/2026/2027 binaries.

Rejected because Autodesk explicitly supports older R24 managed SDKs on later R24 releases, so four legacy copies add build/package/signing surface without adding meaningful isolation. It would also increase maintenance and native qualification work.

### B. One legacy R24.x payload + existing modern payloads (chosen)

Keep the existing three payload families:

- legacy `net48` payload built against AutoCAD 2021 SDK, discovered for `R24.0-R24.3` (AutoCAD 2021-2024);
- modern `net8.0-windows` payload for `R25.0-R25.1` (AutoCAD 2025-2026);
- `net10.0-windows` payload for `R26.0` (AutoCAD 2027).

Add deterministic guards and native acceptance generations for 2022/2023/2024. Treat AutoCAD 2026.1.2+ as the same package generation but require its recorded observed CLR/runtime evidence before native PASS.

Chosen because it follows Autodesk's compatibility table, changes no modelling source, and minimizes package/runtime divergence.

### C. Split AutoCAD 2026.1.2+ into a new .NET 10 package immediately

Rejected for this lane. Autodesk says .NET 8-targeted apps should generally continue to run under the 2026.1.2 .NET 10 host, and the repository has no current source/native evidence that a second 2026 binary is required. Creating one now would be speculative and would expand project/package/signing complexity.

## Architecture

No Autodesk dependency moves into `QS3D.Core`.

The bundle remains three physical payloads. Only the legacy runtime range broadens:

```text
R24.0-R24.3  -> Contents/2021/QS3D.AutoCAD.dll  -> net48
R25.0-R25.1  -> Contents/2025-2026/QS3D.AutoCAD.dll -> net8.0-windows
R26.0        -> Contents/2027/QS3D.AutoCAD.dll  -> net10.0-windows
```

The legacy folder name remains `2021` because it identifies the build SDK/payload family, not the only supported host year. User-facing labels/docs will say `2021-2024` to avoid implying the bundle supports only 2021.

## Native acceptance model

The acceptance schema/tooling will allow generations `2021`, `2022`, `2023`, `2024`, `2025`, `2026`, `2027`.

Expected runtime families:

- 2021-2024 -> `.NET Framework 4.8`
- 2025 -> `.NET 8`
- 2026 -> `.NET 8` package target; observed runtime must still be recorded from the real host. For 2026.1.2+ the host may report .NET 10, and native qualification must record that fact rather than infer PASS from the package target.
- 2027 -> `.NET 10`

The evidence model will distinguish package target/expected payload from observed CLR/runtime enough that 2026.1.2+ cannot be silently mislabeled as a .NET 8 host. Hosted CI remains fail-closed and never writes native PASS.

## Components/files

Expected implementation surfaces:

- `bundle/QS3D.bundle/PackageContents.xml` — broaden legacy series max to `R24.3`, improve payload label.
- `scripts/validate-architecture.ps1` — lock exact runtime ranges and payload mapping.
- `native-acceptance/evidence.schema.json` — add 2022/2023/2024 generations and allow truthful observed runtime family handling.
- `scripts/new-native-acceptance.ps1` and related native acceptance validators/status tooling — map 2021-2024 to the legacy provenance row and make 2026 runtime observation explicit.
- `native-acceptance/required-checks.json` only if a dedicated 2026 runtime-transition check materially improves fail-closed behavior.
- `README.md`, `docs/BUILD.md`, `docs/NATIVE-ACCEPTANCE.md` — document the actual matrix and native boundary.
- focused deterministic regression guard(s) when existing architecture validation is insufficient.

`src/QS3D.AutoCAD/QS3D.AutoCAD.csproj` is intentionally excluded unless implementation evidence proves a project-level change is required.

## Error handling and safety

- Manifest validation must fail if the legacy range is narrower/wider than `R24.0-R24.3` or if modern ranges drift unexpectedly.
- Native acceptance creation must reject unsupported host generations.
- Provenance matching must map 2022-2024 to the existing legacy runtime payload rather than inventing artifact rows.
- 2026 native evidence must not be auto-PASS based on target framework or hosted CI.
- No release publication or native qualification is performed by this lane.

## Testing

1. Add/update deterministic source guards so the old `R24.0`-only manifest fails validation (RED) and `R24.0-R24.3` passes (GREEN).
2. Validate acceptance tooling recognizes 2022/2023/2024 and maps them to legacy provenance.
3. Validate unsupported generations and mismatched runtime/provenance remain fail-closed.
4. Run the repository's existing architecture, native-acceptance-contract, package/artifact, installer and release-policy checks through canonical branch CI.
5. Require exact current branch-head CI SUCCESS before opening the final PR.
6. Record real AutoCAD native execution as pending/external; do not claim native PASS without evidence.

## Scope exclusions

- No changes to UI modernization PR #55 or MEP PRs #59/#61.
- No modelling-command changes.
- No Core Autodesk references.
- No universal BricsCAD+AutoCAD installer in this lane; the AutoCAD repo's own setup remains the AutoCAD delivery mechanism. A cross-repository universal installer would be a separate integration subsystem and should only be done under explicit cross-repo coordination.
- No merge to `main` without explicit owner authorization.
- No production release publishing.
