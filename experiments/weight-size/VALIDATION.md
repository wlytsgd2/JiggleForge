# Reproducing the WeightSize checks / 重现测试

All commands run from the repository root. They only write test artifacts, not
the game installation. Requirements: Windows x64, Node.js, .NET 8 SDK, Windows
SDK (FXC), and MSVC C++ build tools. None of these are player prerequisites.

以下命令从仓库根目录执行，只写入测试产物，不安装游戏运行时。开发环境需要
Windows x64、Node.js、.NET 8 SDK、Windows SDK（FXC）和 MSVC C++ 工具；
玩家使用打好的包不需要这些开发工具。

## Frozen runtime / 冻结运行时

`payload/` contains the exact 51 verified text files used by both app and manual
packages. `runtime-manifest.json` records their hashes. `verify.cjs` defaults to
this directory, creates the r11 comparison baseline if needed, and writes
`artifacts/weight-size-r1/validation/`. Set the environment variable `FXC` to a
compiler path to override discovery via PATH / installed Windows SDK versions.

`payload/` 是应用与手动包共用的 51 个已验证文本文件；清单保存逐文件哈希。
验证器默认检查这个目录，自动准备 r11 对照；若有多个 SDK，可用 `FXC` 指定编译器。

```powershell
node experiments/weight-size/verify.cjs
node experiments/weight-size/tests.cjs
```

The CPU tests always include synthetic cases and the repository's native lab
model. Two additional local character streams are optional: set
`WEIGHT_SIZE_CHARACTER_ASSETS` to their `character-assets.js` file when available.
Missing optional streams are explicitly listed under `skipped` in numeric.json;
they are not silently counted as passed. These local model files are not part of
the runtime package. The baseline clean checkout produces 40 GPU fixtures; with
the two additional character models it produces 42.

CPU 测试始终运行合成用例和仓库已有模型。额外两个本地角色模型不是必需项；
可以用 `WEIGHT_SIZE_CHARACTER_ASSETS` 指定它们的文件。缺少时报告会明确标注跳过，
不会当作通过；干净检出生成 40 个 GPU 用例，补充两个角色后为 42 个。

From an **x64 Native Tools Command Prompt for VS** (not plain PowerShell):

```bat
cl /nologo /EHsc /std:c++17 /O2 experiments\weight-size\gpu.cpp /Fo:artifacts\weight-size-r1\validation\weight_size_gpu.obj /Fe:artifacts\weight-size-r1\validation\weight_size_gpu.exe
node experiments/weight-size/gpu-verify.cjs
```

This tests WARP and the local hardware D3D11 driver without launching/injecting
the game. Hardware failure is not silently replaced by WARP. To investigate a
rebuild instead, use `node experiments/weight-size/build.cjs` and then
`node experiments/weight-size/verify.cjs --runtime artifacts/weight-size-r1/runtime`.
Do not overwrite a packaged runtime revision; use a new revision for changes.

以上测试在独立 D3D11 进程中运行，不启动或注入游戏。硬件测试失败不会偷偷切成 WARP。
开发时可显式检查重建目录；已经发布的运行时版本不能原地改写。

## Application package / 应用包

```powershell
node experiments/weight-size/validate-release.cjs `
  --package artifacts/JiggleForge-v0.1.26-beta.3 `
  --payload experiments/weight-size/payload `
  --manifest experiments/weight-size/runtime-manifest.json `
  --zip artifacts/JiggleForge-win-x64-v0.1.26-beta.3.zip `
  --version 0.1.26-beta.3 `
  --output artifacts/weight-size-r1/release-validation
```

The verifier checks every frozen file, self-contained dependencies, app version,
all integrity-manifest entries, the archive checksum and ZIP-to-directory parity.
It then compiles a small host referencing the **packaged Core DLL**, verifies that
the loaded DLL is byte-identical, and exercises its real installation API only
inside a newly created GUID test directory. It checks install, option changes,
custom physics/keys, reinstall, and an unrelated fixture Mod remaining unchanged.
It never starts the app GUI, wheel helper or game. Reports distinguish this from
actual in-game testing. Fixture output is retained for inspection.

应用包验证还会用包内真正的 Core DLL 在新建测试目录执行安装、开关、参数与按键修改、
重装，确认无关的测试 Mod 不被修改。不会打开应用界面、滚轮辅助进程或游戏，也不会
访问真实 ZZMI 安装目录；这些检查不等于实机游戏验证。
