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

Package the published application together with the contents of `StandaloneShaderFixes`. Do not include local FrameAnalysis dumps, generated Mod folders, test captures, or personal configuration files. The application creates per-Mod generated resources when a user imports a Mod.

## Runtime validation

The shader validation script checks HLSL compilation and compares the CPU reference model with the GPU-compatible runtime path. Warnings emitted for decompiled game shaders can be expected; compilation errors and parity failures are blocking issues.

