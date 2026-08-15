# Build and local qualification

## Prerequisites

- Visual Studio 2022 or a current .NET SDK toolchain
- .NET Framework 4.8 reference assemblies for the AutoCAD 2021 legacy host
- .NET 8 SDK for AutoCAD 2025–2026
- .NET 10 SDK for AutoCAD 2027

The project uses Autodesk-owned NuGet packages for compile-time AutoCAD API references:

- `AutoCAD.NET` 24.0.0 for the AutoCAD 2021 / .NET Framework 4.8 host
- `AutoCAD.NET` 25.0.1 for the .NET 8 host
- `AutoCAD.NET` 26.0.0 for the .NET 10 host

Their runtime assets are excluded from QS3D output because AutoCAD supplies the Autodesk assemblies at runtime.

## Core

```powershell
dotnet build src/QS3D.Core/QS3D.Core.csproj -c Release
dotnet run --project tests/QS3D.Core.SmokeTests/QS3D.Core.SmokeTests.csproj -c Release -f net8.0
dotnet run --project tests/QS3D.Core.SmokeTests/QS3D.Core.SmokeTests.csproj -c Release -f net10.0
```

`QS3D.Core` targets `net48`, `net8.0` and `net10.0`. The net48 target is the compatibility surface consumed by the AutoCAD 2021 host. Modern release/update trust primitives that require newer framework APIs remain isolated from that legacy target rather than weakening the modern implementations.

## AutoCAD host

```powershell
dotnet build src/QS3D.AutoCAD/QS3D.AutoCAD.csproj -c Release -f net48
dotnet build src/QS3D.AutoCAD/QS3D.AutoCAD.csproj -c Release -f net8.0-windows
dotnet build src/QS3D.AutoCAD/QS3D.AutoCAD.csproj -c Release -f net10.0-windows
```

These build the AutoCAD 2021, AutoCAD 2025–2026 and AutoCAD 2027 payloads respectively. They are suitable for source/API compatibility evidence in CI; they do not replace testing inside the real AutoCAD host.

## Package and Setup.exe

```powershell
./scripts/package.ps1 -Version 0.1.0
./scripts/verify-artifacts.ps1 -Version 0.1.0
```

The packaging script refuses tracked working-tree changes by default, builds all three runtime families, stages one `QS3D.bundle`, and writes:

```text
artifacts/QS3D-AutoCAD-0.1.0.zip
artifacts/QS3D-AutoCAD-0.1.0-Setup.exe
artifacts/RELEASE-PROVENANCE.json
artifacts/SHA256SUMS.txt
```

The bundle contains:

```text
Contents/2021/QS3D.AutoCAD.dll          # net48 / AutoCAD R24.0
Contents/2025-2026/QS3D.AutoCAD.dll     # net8.0-windows / R25.0-R25.1
Contents/2027/QS3D.AutoCAD.dll          # net10.0-windows / R26.0
```

`RELEASE-PROVENANCE.json` records the exact git commit, source dirty state, three-family runtime matrix, signing state, artifact byte lengths and SHA-256 hashes. `verify-artifacts.ps1` recomputes those values and verifies `SHA256SUMS.txt`.

The setup executable is a self-contained Windows x64 application with the bundle embedded inside it. Close AutoCAD before install, upgrade or uninstall. Setup stages and validates the candidate before replacing the installed bundle and attempts rollback if replacement fails. The installer runtime smoke also verifies that the installed bundle contains the R24.0/2021 payload as well as the modern payloads.

To remove the plugin:

```powershell
./QS3D-AutoCAD-0.1.0-Setup.exe --uninstall
```

The PowerShell install/uninstall scripts under `installer/` remain available for development and troubleshooting.

## Authenticode signing

Engineering packages are unsigned by default. To exercise real signing locally, provide a real PFX and password:

```powershell
./scripts/package.ps1 `
  -Version 0.1.0 `
  -SigningPfxPath C:\Secure\qs3d-signing.pfx `
  -SigningPassword $env:QS3D_SIGNING_PASSWORD `
  -TimestampUrl https://your-rfc3161-timestamp-service.example

./scripts/verify-artifacts.ps1 -Version 0.1.0 -RequireSigned
```

Do not commit a PFX/private key/password. The tag release workflow requires GitHub secrets `QS3D_SIGNING_PFX_BASE64` and `QS3D_SIGNING_PFX_PASSWORD`, repository variable `QS3D_NATIVE_ACCEPTED_SHA`, and optionally `QS3D_TIMESTAMP_URL`. See `docs/RELEASE-SECURITY.md`.

## Native acceptance

A successful source CI run is not a native AutoCAD qualification. The default formal release matrix remains AutoCAD 2025, 2026 and 2027. AutoCAD 2021 is supported through a separate legacy evidence lane so a 2021 qualification cannot be confused with the modern production matrix.

For every host being qualified, test a bundle built from one exact commit and verify:

1. AutoCAD discovers the bundle without manual `NETLOAD`.
2. `QS3D` command-invocation autoload opens the palette.
3. Every modelling command creates expected geometry and participates in AutoCAD undo/redo.
4. Level/Grid assign, move, bind, snap, rename/resequence, clear and dependency-safe delete behavior matches visible geometry/metadata semantics.
5. JIG previews, live dimensions/orientation and cancel safety work in the actual host.
6. `QS3DBOQ` sees only QS3D-tagged geometry and reports correct quantities.
7. Save, close and reopen the DWG; project state, placement references and XData must persist.
8. Restart AutoCAD and repeat command invocation to verify autoload behavior.
9. Install, upgrade and `--uninstall` the generated Setup.exe.
10. Record exact commit, AutoCAD build and artifact hashes before any native-accepted SHA is approved.

Use `docs/NATIVE-ACCEPTANCE.md` for the evidence commands, including the separate AutoCAD 2021 validation flow.
