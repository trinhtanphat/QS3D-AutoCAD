# Build and local qualification

## Prerequisites

- Visual Studio 2022 or a current .NET SDK toolchain
- .NET 8 SDK for AutoCAD 2025–2026
- .NET 10 SDK for AutoCAD 2027
- Autodesk AutoCAD/ObjectARX Managed SDK files containing `AcCoreMgd.dll`, `AcDbMgd.dll` and `AcMgd.dll`

Set one or both environment variables:

```powershell
$env:AUTOCAD_2026_SDK_DIR = 'C:\Autodesk\ObjectARX 2026\inc'
$env:AUTOCAD_2027_SDK_DIR = 'C:\Autodesk\ObjectARX 2027\inc'
```

Use the directory that actually contains the three managed DLLs on your machine.

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

The host build intentionally fails with an explicit error if the corresponding Autodesk SDK directory is not configured. GitHub-hosted CI therefore validates Core and source/package boundaries; native host compilation belongs on the SDK-equipped runner.

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

A successful source CI run is not a native AutoCAD qualification. For each supported runtime generation, test a bundle built from the exact commit and verify:

1. AutoCAD discovers the bundle without manual `NETLOAD`.
2. `QS3D` lazy-loads the plugin and opens the palette.
3. Every modelling command creates expected geometry and participates in AutoCAD undo/redo.
4. `QS3DBOQ` sees only QS3D-tagged geometry and reports correct quantities.
5. Save, close and reopen the DWG; project state and XData must persist.
6. Restart AutoCAD and repeat command invocation to verify autoload behavior.
7. Install, upgrade and `--uninstall` the generated Setup.exe.
