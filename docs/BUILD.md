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

The host build intentionally fails with an explicit error if the corresponding Autodesk SDK directory is not configured.

## Package

```powershell
./scripts/package.ps1 -Version 0.1.0
```

The generated zip is written under `artifacts/` and contains `QS3D.bundle` with separate 2025–2026 and 2027 payloads.
