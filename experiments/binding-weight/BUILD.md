# Bone-weight beta runtime / 骨骼权重测试运行时

This directory contains the experimental runtime and offline lab used in
`v0.1.26-beta.2`. It does not replace the stable `StandaloneShaderFixes` tree.
The bone switch changes vertex eligibility/weighting only. Both modes use the
configured Maximum offset; there is no additional 0.03 cap.

## Build the same runtime and application

Requirements: Node.js, the repository's normal Windows/.NET/WinUI build tools.
From the repository root:

```powershell
node experiments/binding-weight/build.cjs
powershell -NoProfile -ExecutionPolicy Bypass -File build/CreateReleasePackage.ps1 -VersionOverride 0.1.26-beta.2 -RuntimePayloadSource artifacts/binding-weight-runtime/JiggleForge-SharedWeight-Test-r11-options
```

`JIGGLEFORGE_EXPERIMENT_OUTPUT` can override the intermediate output directory.
The release script copies this generated runtime into `Runtime/`, and the lab
into `App/Lab/`. Without `-RuntimePayloadSource`, normal builds continue using
the stable runtime. An application update never implicitly enables this beta
for stable users.

For installation and limitations, see [中文](TEST-BUILD.zh-CN.md) /
[English](TEST-BUILD.en.md). Close the game before installing or switching
runtimes; do not rely on F10 to discard old shader/resource caches.

## Source layout

- `hooks.ini`, `binding_*.hlsl`: actual skin-stream binding, nearest vertex,
  frozen bone IDs/weights, and vertex-side eligibility.
- `demand.ini`, `demand.cjs`: visible-cursor scheduling and sleep cancellation.
- `diagnostic.*`, `diagnostic_export.cjs`: optional diagnostics, off by default.
- `build.cjs`: overlays the beta features onto the stable runtime without
  changing character Mod files.
- `lab/`: offline Three.js visualizer and JavaScript ports of spatial/motion
  formulas; local fixtures are not a reconstruction of the game's renderer.

The model fixture is a game asset, not original JiggleForge code; applicable
rights remain with its rights holders. Three.js's MIT license is included in
`lab/vendor/THREE-LICENSE.txt`. Project code is GPL-3.0-only.

本目录为测试版对应源码，不包含开发者的本机诊断导出、个人路径或安装回滚脚本。
默认构建仍使用正式运行时；复现测试版需要按上面的命令指定生成的运行时目录。
