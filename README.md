# JiggleForge

[English version](README.en.md)

JiggleForge 是一个面向《绝区零》XXMI/ZZMI 替换 Mod 的 Windows 桌面应用，可以把替换 Mod 转换成支持鼠标拖动变形的 Mod。

## 功能

- 安装、更新、检查和卸载全局 JiggleForge 运行时。
- 不扫描普通 Mod；应用会记录实际打开并成功适配过的项目，并在当前 ZZMI 中只检索带有 JiggleForge 标记的 Mod，之后可以从首页直接重新打开。
- 扫描 Mod 中的 DrawIndexed 命令，并直接在原 Mod 文件夹内完成适配。
- 为每个 Draw 分配稳定身份，可以修改别名或关闭该 Draw 的变形。
- 支持 Draw 分组和有向依赖关系，让相关部位共同变形。
- 支持每组独立物理参数、全局默认参数、多个拖动键、短按拍打和滚轮深度控制。
- 支持 DDS Mask：白色区域完全变形，黑色区域不变形；没有 Mask 时默认权重为 `1.0`。
- 提供可选的游戏内 Draw 检测器，用于识别较小或重叠的部位。
- 首次适配前自动创建 `JiggleForge.original.zip`，可以从概览页恢复原始 Mod。
- 启动时检查 GitHub 最新稳定版；可以立即一键更新，也可以暂不更新并在标题栏保留提醒。
- 在“设置”中可以随时检查更新、重新安装最新版本并校验当前安装文件。
- 首次启动先选择简体中文或 English；之后可随时在“设置”中切换显示语言。

## 运行要求

- Windows 10 19041 或更高版本，或 Windows 11。
- 已安装 XXMI/ZZMI 和受支持的游戏环境。
- JiggleForge 自包含发布包，不需要另外安装 .NET。

游戏更新可能会改变 Shader Hash。如果某个 pass 不再被识别，需要先更新[场景 VS Hash 对照表](docs/development/shader-hash-matrix.zh-CN.md)，再适配新的 Mod。

## 快速开始

1. 从 [Releases](https://github.com/wlytsgd2/JiggleForge/releases) 下载并解压最新自包含版本。
2. 启动 `JiggleForge.exe`，先选择显示语言，再按首次使用向导完成设置。
3. 打开“设置”，选择包含 `Mods` 和 `ShaderFixes` 的 ZZMI 根目录，然后安装或更新运行时。
4. 把一个具体的 Mod 文件夹拖入首页，或手动选择文件夹。适配成功后，该项目会保存在首页记录中供以后直接打开。
5. 检查 Draw、分组、Mask、依赖关系和物理参数，然后点击“应用配置”。
6. 回到游戏按 `F10`。
7. 按住配置好的拖动键可以拖动模型；快速点按会沿三角形法线产生拍打效果。需要滚轮控制深度时，在“设置”页面启动 WheelBridge。

## 应用更新

应用启动后会检查 GitHub 最新稳定版。发现新版本时可以立即更新或暂不更新；暂不更新后，窗口左上角会持续显示新版本提醒。

一键更新会先下载 ZIP 和对应的 SHA-256 文件，校验通过后才会关闭应用并替换程序文件。更新失败时，独立更新器会尽可能恢复更新前的文件。也可以在“设置”的“应用更新与校验”中手动检查更新或校验当前安装。

`v0.1.1` 及更早版本不包含更新器，需要手动安装一次 `v0.1.2` 或更高版本；之后即可使用一键更新。

应用会直接修改选中的 Mod 文件夹。首次适配时会在该文件夹中生成 `JiggleForge.txt`、运行时文件和原始备份压缩包。

## 备份与恢复

备份文件是 Mod 根目录中的 `JiggleForge.original.zip`，只包含 JiggleForge 修改过的源文件和适配前已经存在的相关文件，不会复制整个 Mod。

恢复原始 Mod 后，备份压缩包仍会保留，因此可以重复恢复。由旧版本生成且没有该备份文件的 Mod，当前应用无法保证逐字节恢复；重新应用前请自行复制一份。

## 重要规则

- 同一个组中的 Draw 共用变形状态。
- 有向边 `A -> B` 表示拖动 A 组时也会影响 B 组。
- 没有 Mask 时，整个 Draw 都参与变形。
- 运行时是全局安装的，但每个 Mod 仍需要单独适配。
- 安装或更新运行时前应关闭游戏；适配完成后回到游戏按 `F10` 测试。
- 不同角色、场景、LOD、半透明 pass 和第三方替换 Mod 可能使用不同的 Shader 布局，不能保证全部自动兼容。

## 文档

- [快速入门](docs/user-guide/quick-start.zh-CN.md)
- [备份与恢复](docs/user-guide/backup.md)
- [配置说明](docs/user-guide/configuration.zh-CN.md)
- [故障排查](docs/user-guide/troubleshooting.zh-CN.md)
- [场景 VS Hash 对照表](docs/development/shader-hash-matrix.zh-CN.md)
- [构建说明](docs/development/building.md)

## 项目结构

- `app/JiggleForge` — WinUI 3 桌面应用。
- `src/JiggleForge.Core` — 扫描、配置、补丁和运行时生成。
- `src/JiggleForge.WheelBridge` — 滚轮输入桥接程序。
- `src/JiggleForge.Updater` — 独立应用更新器。
- `StandaloneShaderFixes` — 全局运行时和支持的 VS 替换源文件。
- `tests` — 自动化测试。
- `docs` — 用户和开发文档。

## 许可与声明

源代码采用 [GNU GPL-3.0-only](LICENSE) 许可。项目品牌和第三方内容请参阅 [BRANDING.md](BRANDING.md) 与 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

JiggleForge 是独立的社区工具，与游戏发行商、XXMI 和 3DMigoto 没有隶属关系。请遵守游戏、Mod 作者和发布平台的相关规则。
