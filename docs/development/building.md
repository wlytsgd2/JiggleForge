# Building from source

## Requirements

- Windows 10 19041 or later (Windows 11 supported).
- .NET 8 SDK with the Windows desktop workload.
- A recent PowerShell for the runtime validation scripts.

## Build and test

From the repository root:

```powershell
dotnet restore JiggleForge.slnx
dotnet build app\JiggleForge\JiggleForge.csproj -c Release -p:Platform=x64
dotnet test JiggleForge.slnx -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\shaders\ValidateGpuRuntime.ps1
```

The application is a self-contained Windows desktop project. Build output is written to the normal `bin`/`obj` directories and is intentionally ignored by Git.

## Packaging

Create the self-contained package from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\CreateReleasePackage.ps1
```

The script publishes the app, includes the updater and user documentation, generates `JiggleForge.manifest.sha256`, and creates both the ZIP and its `.sha256` sidecar under `artifacts`.

All official packages use this stable layout:

```text
JiggleForge/
├─ JiggleForge.exe
├─ App/
├─ Runtime/
├─ docs/
├─ README.md
└─ LICENSE
```

`JiggleForge.exe` at the package root is the user-facing launcher. .NET and WinUI files stay under `App`, while files installed into ZZMI stay under `Runtime`. Do not publish the raw `dotnet publish` directory directly. `CreateReleasePackage.ps1` validates the required layout and fails if a required launcher, application, updater, runtime, or documentation file is missing.

Upload both files to a GitHub Release whose tag is `v<Version>`. The asset names must remain `JiggleForge-win-x64-v<Version>.zip` and `JiggleForge-win-x64-v<Version>.zip.sha256`; the in-app updater locates those names. Do not include local FrameAnalysis dumps, generated Mod folders, test captures, or personal configuration files.

## Runtime validation

The shader validation script checks HLSL compilation and compares the CPU reference model with the GPU-compatible runtime path. Warnings emitted for decompiled game shaders can be expected; compilation errors and parity failures are blocking issues.
