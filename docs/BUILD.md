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
```

The packaging script builds both host generations, stages `QS3D.bundle`, and writes:

```text
artifacts/QS3D-AutoCAD-0.1.0.zip
artifacts/QS3D-AutoCAD-0.1.0-Setup.exe
```

The setup executable is a self-contained Windows x64 application with the bundle embedded inside it. Run it elevated to install or upgrade QS3D for all users. To remove the plugin:

```powershell
./QS3D-AutoCAD-0.1.0-Setup.exe --uninstall
```

The PowerShell install/uninstall scripts under `installer/` remain available for development and troubleshooting.

## Native acceptance

A successful source CI run is not a native AutoCAD qualification. For AutoCAD 2025, 2026 and 2027, test a bundle built from the exact commit and verify:

1. AutoCAD discovers the bundle without manual `NETLOAD`.
2. `QS3D` command-invocation autoload opens the palette.
3. Every modelling command creates expected geometry and participates in AutoCAD undo/redo.
4. `QS3DBOQ` sees only QS3D-tagged geometry and reports correct quantities.
5. Save, close and reopen the DWG; project state and XData must persist.
6. Restart AutoCAD and repeat command invocation to verify autoload behavior.
7. Install, upgrade and `--uninstall` the generated Setup.exe.
