# JiggleForge

JiggleForge 是一个 Windows 桌面应用，用于把 XXMI/3DMigoto 替换 Mod 适配为可配置的鼠标拖动世界坐标变形 Mod。

应用将 Draw 检测、分组、依赖图、Mask、物理参数、运行时安装和滚轮深度输入集中到一个界面中。应用会直接修改选中的 Mod 文件夹，并用 `JiggleForge.txt` 保存项目配置。

> 当前仓库正在准备首个公开版本。正式自包含压缩包将在 Releases 页面提供。

## 功能

- 首次导入后，生成的 Mod 不再依赖原 Mod 文件夹。
- 为每个适配 Draw 分配稳定的状态身份，并支持可视化分组。
- 支持显式有向依赖关系和传递影响。
- 使用世界空间拾取和变形，使身体、轮廓、半透明、深度和材质 pass 可以复用同一变形状态。
- 零位移时保持原始法线、切线和光照，拖动时根据形变场更新表面坐标系。
- 支持 DDS 纹理 Mask；未指定 Mask 时按权重 `1.0` 处理。
- 支持每组独立物理参数、全局默认值、多个拖动键、滚轮深度和可选的游戏内 Draw 检测器。
- 可在应用中安装、检查和更新全局运行时及支持的游戏 VS 替换。

## 运行要求

- Windows 10 19041 或更高版本，或 Windows 11。
- 已安装 XXMI/3DMigoto 和受支持的游戏环境。
- JiggleForge 自包含发布包。压缩包已包含 .NET 运行时，不需要另行安装 .NET。

支持的 VS hash 矩阵见[游戏场景 VS hash 对照表](docs/development/shader-hash-matrix.zh-CN.md)。游戏更新可能改变这些 hash；如果某个 pass 不再被识别，应重新进行 FrameAnalysis hunting，再更新对照表。

## 快速开始

1. 下载并解压最新自包含版本到有写入权限的普通文件夹。
2. 启动 `JiggleForge.exe`。
3. 按首次使用向导完成基本设置；以后可从左侧底部的“使用向导”重新打开。
4. 打开“运行环境”，选择 ZZMI 根目录。
5. 点击“安装或更新运行时”，回到游戏按 `F10`。
6. 将替换 Mod 文件夹拖入应用，或使用文件夹选择器打开。
7. 在 Draw 页面检查 Draw、分组、Mask 和检测器开关。
8. 需要时在“物理参数”和“依赖关系”页面修改配置，然后点击“应用配置”。
9. 回到游戏按 `F10` 重新加载生成资源。
10. 按住配置好的拖动键拖动模型。需要滚轮控制深度时，在“运行环境”页面启动 WheelBridge。

详细步骤见[中文快速入门](docs/user-guide/quick-start.zh-CN.md)。

## 已经适配过的 Mod

使用旧版 JiggleForge 生成的 Mod，应使用当前应用打开一次并重新点击“应用配置”。这会把私有运行资源更新到当前配置格式和命令名称。更新大型运行时前，请先备份能正常运行的 Mod。

## 项目结构

- `app/JiggleForge`：WinUI 3 桌面应用。
- `src/JiggleForge.Core`：项目扫描、配置、补丁和运行时生成。
- `src/JiggleForge.WheelBridge`：滚轮输入桥接程序。
- `StandaloneShaderFixes`：全局运行时和支持的 VS 替换源文件。
- `tests`：单元测试、契约测试和 CPU/GPU 一致性测试。
- `docs/user-guide`：面向用户的说明。
- `docs/development`：架构、配置、hash 对照表和实现说明。

## 构建与测试

```powershell
dotnet test JiggleForge.slnx --configuration Release
dotnet build app/JiggleForge/JiggleForge.csproj -c Release -p:Platform=x64
powershell -ExecutionPolicy Bypass -File tests/shaders/ValidateGpuRuntime.ps1
```

生成自包含 x64 包：

```powershell
dotnet publish app/JiggleForge/JiggleForge.csproj `
  -c Release -p:Platform=x64 -r win-x64 --self-contained true `
  -o artifacts/JiggleForge
```

`artifacts` 会被 Git 忽略。正式压缩包和 SHA-256 校验文件应通过 GitHub Release 发布。

## 文档

- [中文快速入门](docs/user-guide/quick-start.zh-CN.md)
- [配置说明](docs/user-guide/configuration.zh-CN.md)
- [故障排查](docs/user-guide/troubleshooting.zh-CN.md)
- [运行时架构](docs/development/runtime-architecture.zh-CN.md)
- [配置格式](docs/development/config-format.zh-CN.md)
- [游戏场景 VS hash 对照表](docs/development/shader-hash-matrix.zh-CN.md)
- [构建说明](docs/development/building.md)

## 范围与来源

JiggleForge 的输入处理、状态模型、运动解算、世界空间形变场、桌面应用和测试套件均作为独立实现维护。由游戏导出的 VS 替换源文件与应用源码分开保存，并在[来源说明](docs/development/provenance-audit.zh-CN.md)中单独说明。

JiggleForge 是独立的社区工具，与游戏发行商、XXMI 和 3DMigoto 没有隶属关系。使用者应自行遵守游戏、Mod 作者和发布平台的相关规则。

## 许可证与品牌

本仓库中的原创源代码使用 [GNU GPL-3.0-only](LICENSE) 授权。项目名称、Logo、图标、游戏导出的内容和第三方资源分别处理，详见[品牌与第三方内容说明](BRANDING.md)和[第三方内容声明](THIRD-PARTY-NOTICES.md)。

本项目与游戏发行商、XXMI 和 3DMigoto 没有隶属关系。请不要把修改后的版本伪装成官方 JiggleForge 发布版本。
