# Build and local qualification

## Prerequisites

- Visual Studio 2022 or a current .NET SDK toolchain
- .NET 8 SDK for AutoCAD 2025–2026
- .NET 10 SDK for AutoCAD 2027

The project uses Autodesk-owned NuGet packages for compile-time AutoCAD API references:

- `AutoCAD.NET` 25.0.1 for the .NET 8 host
- `AutoCAD.NET` 26.0.0 for the .NET 10 host

Their runtime assets are excluded from QS3D output because AutoCAD supplies the Autodesk assemblies at runtime.

## Core

```powershell
dotnet build src/QS3D.Core/QS3D.Core.csproj -c Release
dotnet run --project tests/QS3D.Core.SmokeTests/QS3D.Core.SmokeTests.csproj -c Release -f net8.0
dotnet run --project tests/QS3D.Core.SmokeTests/QS3D.Core.SmokeTests.csproj -c Release -f net10.0
```

## AutoCAD host

```powershell
dotnet build src/QS3D.AutoCAD/QS3D.AutoCAD.csproj -c Release -f net8.0-windows
dotnet build src/QS3D.AutoCAD/QS3D.AutoCAD.csproj -c Release -f net10.0-windows
```

These host builds are suitable for source/API compatibility evidence in CI; they do not replace testing inside AutoCAD.

## Package and Setup.exe

```powershell
./scripts/package.ps1 -Version 0.1.0
./scripts/verify-artifacts.ps1 -Version 0.1.0
```

The packaging script refuses tracked working-tree changes by default, builds both host generations, stages `QS3D.bundle`, and writes:

```text
artifacts/QS3D-AutoCAD-0.1.0.zip
artifacts/QS3D-AutoCAD-0.1.0-Setup.exe
artifacts/RELEASE-PROVENANCE.json
artifacts/SHA256SUMS.txt
```

`RELEASE-PROVENANCE.json` records the exact git commit, source dirty state, runtime matrix, signing state, artifact byte lengths and SHA-256 hashes. `verify-artifacts.ps1` recomputes those values and verifies `SHA256SUMS.txt`.

The setup executable is a self-contained Windows x64 application with the bundle embedded inside it. Close AutoCAD before install, upgrade or uninstall. Setup stages and validates the candidate before replacing the installed bundle and attempts rollback if replacement fails.

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

A successful source CI run is not a native AutoCAD qualification. For AutoCAD 2025, 2026 and 2027, test a bundle built from the exact commit and verify:

1. AutoCAD discovers the bundle without manual `NETLOAD`.
2. `QS3D` command-invocation autoload opens the palette.
3. Every modelling command creates expected geometry and participates in AutoCAD undo/redo.
4. Level/Grid assign, move, bind, clear and dependency-safe delete behavior matches visible geometry/metadata semantics.
5. `QS3DBOQ` sees only QS3D-tagged geometry and reports correct quantities.
6. Save, close and reopen the DWG; project state, placement references and XData must persist.
7. Restart AutoCAD and repeat command invocation to verify autoload behavior.
8. Install, upgrade and `--uninstall` the generated Setup.exe.
9. Record exact commit, AutoCAD build and artifact hashes before setting `QS3D_NATIVE_ACCEPTED_SHA`.
