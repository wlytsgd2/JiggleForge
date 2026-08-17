# JiggleForge

[English](README.en.md)

JiggleForge 是一个面向《绝区零》XXMI/ZZMI 的开源 Windows 桌面应用。它为受支持的角色渲染流程加入实时顶点变形，让玩家能够用鼠标拖动角色、用滚轮控制前后深度，并通过快速点击产生拍打效果。

全局运行时可以直接处理受支持的原版角色模型；对于第三方替换 Mod，JiggleForge 还可以分析实际 Draw、生成独立配置，并提供分组、依赖、Mask 和各组物理参数。

> JiggleForge 仍在持续开发。游戏更新、不同场景、LOD、半透明材质、特殊 Shader 和第三方 Mod 都可能影响兼容性。

## 主要功能

- 安装、更新、检查或卸载全局 JiggleForge 运行时。
- 使用鼠标拖动模型、滚轮控制深度，快速点击产生拍打冲量。
- 使用可配置的游戏内总开关（默认 `F7`）关闭变形和主要 GPU 计算。
- 在原 Mod 文件夹中直接完成适配，不要求 Blender 工程或重新导出模型。
- 为每个 Draw 分配稳定身份，可设置别名或单独关闭变形。
- 将多个 Draw 分组，并为每个组设置独立物理参数。
- 使用可传递的有向依赖关系控制部件共同变形。
- 使用 DDS 纹理 Mask 控制 Draw 内各区域的变形权重。
- 使用可选 Draw 检测器在游戏中定位小型或重叠部件。
- 首次适配前自动创建原始备份，并可从应用恢复 Mod。
- 记录已经适配的项目，在当前 ZZMI 中重新发现带 JiggleForge 标记的 Mod。
- 提供简体中文和 English 界面、首次使用向导、应用更新和完整性校验。

## 运行要求

- 64 位 Windows 10 19041 或更高版本，或 Windows 11。
- Windows 版《绝区零》。
- 最新官方版本的 XXMI/ZZMI。
- 完整解压的 JiggleForge 官方自包含发布包。

官方发布包包含所需的 .NET 和 Windows App SDK 运行组件，正常情况下不需要另外安装。不要直接在 ZIP 压缩包内启动应用。

## 发布包目录

完整解压后的目录按用途分开：

```text
JiggleForge/
├─ JiggleForge.exe   # 启动入口；请运行这个文件
├─ App/              # 应用本体、.NET 与 WinUI 组件
├─ Runtime/          # 安装到 ZZMI 的 JiggleForge 运行时
├─ docs/             # 使用文档
├─ README.md
└─ LICENSE
```

请保持目录结构完整，不要单独移动或运行 `App` 中的文件。JiggleForge 主程序以当前用户的普通权限运行；只有启动 WheelBridge 或写入确实需要管理员权限的位置时，Windows 才会单独请求授权。

如果无法或不希望运行桌面应用，也可以从同一 GitHub Release 下载 `JiggleForge-manual-v<版本>.zip`。手动包不含 EXE、DLL 或命令脚本；将其中的 `Mods` 和 `ShaderFixes` 合并到 ZZMI 根目录即可。它默认同时支持鼠标左键和 X 键拖动，但不包含 WheelBridge、图形化配置、Mod 自动适配和自动更新。

## 普通玩家快速开始

1. 从 [GitHub Releases](https://github.com/wlytsgd2/JiggleForge/releases) 下载最新的 `win-x64` 压缩包并完整解压。
2. 运行 `JiggleForge.exe`，选择语言并按照首次使用向导操作。
3. 在“设置”中选择实际用于启动游戏的 ZZMI 根目录。正确目录通常同时包含 `Mods` 和 `ShaderFixes`。
4. 建议先关闭游戏，然后点击“安装或更新运行时”。
5. 启动游戏，或回到游戏按 `F10` 重新加载配置。
6. 将鼠标放在受支持的角色表面，按住配置好的拖动键（默认鼠标左键）并移动鼠标。
7. 需要第三维控制时，在“设置”中启动 WheelBridge，并在按住拖动键时滚动滚轮。

现在已经可以拖动了，但替换 Mod 不一定可以正常拖动。未适配 Mod 只能借助原版几何近似拾取，可能出现无法选中、变形位置不对应或只有部分部位响应。如果想让 Mod 也能准确适配，或者想为每个 Mod 修改不同参数，请继续阅读下面面向 Mod 作者的内容。

## Mod 作者使用方法

以下内容面向希望发布 JiggleForge 适配 Mod 的作者，也适用于希望自行精确配置某个替换 Mod 的进阶用户。JiggleForge 不要求原始 Blender 工程，也不需要重新导出模型；只要原替换 Mod 已经能够通过 XXMI/ZZMI 正常运行，就可以直接开始适配。

### 适配替换 Mod

1. 把一个具体的 Mod 文件夹拖入 JiggleForge 首页，或手动选择该文件夹。
2. 不要拖入整个 `Mods` 目录、ZZMI 根目录或 Mod 管理器的集合目录。
3. JiggleForge 会检查 INI、Draw 命令和资源引用，并在第一次修改前创建备份。
4. 检查识别出的 Draw，必要时使用 Draw 检测器在游戏中确认对应部位。
5. 设置 Draw 别名、分组、依赖关系、Mask 和各组物理参数。
6. 点击“应用配置”。
7. 回到游戏按 `F10`，在相关角色、场景和视角中测试。

适配后的项目会记录在首页。应用不会把普通 Mod 或管理器容器全部当作项目，只会保留实际打开并适配过的历史记录，并在当前 ZZMI 中检索带有 JiggleForge 标记的 Mod。

### 生成的文件

适配后的 Mod 通常包含：

```text
JiggleForge.txt
_JiggleForgeRuntime
JiggleForge.original.zip
```

- `JiggleForge.txt`：项目配置源。
- `_JiggleForgeRuntime`：根据配置生成的私有运行资源。
- `JiggleForge.original.zip`：首次适配前的原始文件备份，游戏运行时不依赖它。

### Draw、分组和原版部件

一个 Draw 表示原 Mod 中的一条实际绘制路径，不一定正好对应肉眼看到的完整身体部位。

- 同一组内的 Draw 共用变形状态和物理参数。
- 连续模型的多个 Draw 可以放入同一组。
- 左腿和右腿若需要独立响应，应放入不同组。
- 关闭 Draw 的变形后，它仍可被检测器识别，但不会产生或接收变形。
- “原版部件”表示当前 Mod 没有替换、仍由游戏原始路径绘制的几何体，其开关独立于普通 Draw，并且新项目中默认关闭。

Draw 检测器只应用于配置和诊断。完成部件识别后，正常游玩时建议关闭。

### 依赖关系

有向边：

```text
A -> B
```

表示拖动 A 组时，B 组也会受到影响。依赖可以传递，例如：

```text
Body -> Underwear
Underwear -> Coat
```

此时从 `Body` 开始的拖动也会影响 `Coat`。这可以减少身体和衣物之间的穿模。

依赖关系也可以用来制作掀开或拉动衣服的效果。将身体和衣服放入不同组，只建立需要的单向依赖，即可让衣服在被拖动时独立移动，而不会自动拉动身体；同时仍可让选定的外层衣物跟随内层部件。

### 纹理 Mask

Mask 使用 DDS 纹理红色通道作为变形权重：

- 黑色：`0.0`，不变形。
- 白色：`1.0`，完全变形。
- 灰色：部分变形。
- 文件缺失或无效：回退到 `1.0`。

多个 Draw 可以共用同一个 Mask。Mask 必须匹配模型使用的 UV；它只改变顶点变形权重，不改变鼠标能够拾取哪些三角形。

### 物理参数

设置中的全局默认参数用于原版角色和新项目，每个组都可以使用独立值覆盖默认参数。主要参数包括：

- 影响半径与衰减指数。
- 强度、拖动比例和最大位移。
- 抓取/松手频率与阻尼比。
- 释放/拍打冲量。
- 目标跟随时间。
- 滚轮深度步长和最小/最大深度。
- 体积响应。

修改后需要保存或应用配置，再回到游戏按 `F10`。

## 游戏内控制与性能

- 拖动键：默认为鼠标左键，可在设置中配置多个按键。
- 滚轮：按住拖动键时控制朝向或远离摄像机的深度，需要 WheelBridge。
- 快速点击：沿所选三角形法线产生拍打和回弹。
- `F10`：重新加载 XXMI/ZZMI 配置。
- `F7`：关闭或开启 JiggleForge，按键可配置。

关闭 JiggleForge 后，不只是禁止鼠标输入，还会跳过主要拾取、物理模拟、自动校准、分组注册和适配 Draw 的 GPU 工作。适配 Mod 仍通过兼容路径正常显示。

## 备份与恢复

首次适配前，JiggleForge 会在 Mod 根目录创建 `JiggleForge.original.zip`。它只保存应用将要修改的源文件和适配前已存在的相关运行文件，不复制整个 Mod。

恢复方法：

1. 在 JiggleForge 中打开该项目。
2. 进入“概览”，确认备份有效。
3. 点击“恢复原始 Mod”并确认。

应用会恢复原始 INI 字节、删除项目配置和生成的私有运行资源，并保留备份压缩包以便再次恢复。旧版本适配且没有有效备份的 Mod 无法保证逐字节恢复，请先自行复制。

## 更新与完整性校验

应用启动时会检查最新稳定版。发现更新后可以立即安装或暂时跳过；跳过后，应用名称旁会保留提示。

一键更新会下载发布 ZIP 和 SHA-256 文件，校验成功后才替换程序。可以在“设置”的“应用更新与校验”中手动检查更新、重新安装最新版或验证当前文件。

## 卸载

### 只卸载全局运行时

在“设置”中停止 WheelBridge，然后点击“卸载运行环境”。这会移除全局变形运行时并恢复安装时备份的 ShaderFix 文件。

已经适配的 Mod 可能仍需要 JiggleForge 运行时或兼容层才能正常显示，不要直接随机删除其中生成的 INI 区块。

### 完整移除应用

点击“准备卸载”，然后选择：

- **保留兼容层**：移除变形计算，但保留项目配置和备份，让已适配 Mod 在不能变形的情况下继续正常显示。
- **恢复 Mod**：验证备份并恢复所有已记录或发现的适配 Mod，然后移除全局运行时。

处理完成后应用会打开自身目录并退出，请手动删除整个应用文件夹。JiggleForge 不使用提权的自删除程序，以降低杀毒软件启发式误报风险。

## 常见问题

### 游戏中没有效果

- 更新到最新官方 XXMI/ZZMI。
- 确认选择的是实际启动游戏的 ZZMI 根目录。
- 重新安装 JiggleForge 全局运行时。
- 确认 `F7` 没有关闭运行时。
- 回到游戏按 `F10`，必要时彻底重启游戏。

### 左上角显示 Draw，但模型不变形

说明检测器或部分资源已经加载，但变形状态、Shader pass、Draw 绑定或全局运行时不匹配。更新 XXMI/ZZMI 和 JiggleForge，重新安装运行时，再对该 Mod 点击一次“应用配置”。

### 只有部分区域可以拖动

可能原因包括未适配 Mod 的近似拾取、未登记的 Shader pass、半透明/轮廓/阴影/近摄像机路径、不可见网格抢先被拾取、Draw 被关闭、黑色 Mask 或过期配置。

### 模型破碎、变白或闪烁

先确认原 Mod 在不使用 JiggleForge 时正常。不要跨角色、游戏版本或 LOD 复制配置；资源布局变化后应恢复原 Mod 并重新适配。

### 鼠标位置与变形位置不一致

不同场景可能使用不同投影、画布缩放或合成路径。请记录游戏分辨率、窗口模式和具体场景，并提供 `d3d11_log.txt` 与对应场景的 FrameAnalysis。

报告问题前，请先确认使用最新官方 XXMI/ZZMI。第三方重新打包、修改或过期版本无法保证兼容。

## 文档与支持

- [快速入门](docs/user-guide/quick-start.zh-CN.md)
- [配置说明](docs/user-guide/configuration.zh-CN.md)
- [备份与恢复](docs/user-guide/backup.md)
- [故障排查](docs/user-guide/troubleshooting.zh-CN.md)
- [Shader Hash 对照表](docs/development/shader-hash-matrix.zh-CN.md)
- [构建说明](docs/development/building.md)
- [Bilibili 介绍视频](https://www.bilibili.com/video/BV1Bt3d6DEYN/)
- QQ 群：`451901293`

无论有任何问题、任何建议，还是想不落下更新，或者单纯喜欢水群，欢迎加入。

## 项目结构

- `app/JiggleForge`：WinUI 3 桌面应用。
- `src/JiggleForge.Core`：扫描、配置、补丁和运行时生成。
- `src/JiggleForge.WheelBridge`：滚轮输入桥接程序。
- `src/JiggleForge.Updater`：独立更新器。
- `StandaloneShaderFixes`：全局运行时和受支持 VS 替换源文件。
- `tests`：自动化测试。
- `docs`：用户和开发文档。

## 致谢

JiggleForge 的开发离不开先行研究、开发工具和社区测试，其中包括 Rayvich / RZMenu 对《绝区零》交互式模型变形技术的早期探索与启发。

完整的项目来源与致谢记录见 [ACKNOWLEDGEMENTS.md](ACKNOWLEDGEMENTS.md)。

## 许可与声明

JiggleForge 源代码采用 [GNU GPL-3.0-only](LICENSE) 许可。项目品牌和第三方内容另见 [BRANDING.md](BRANDING.md) 与 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

本项目是独立社区工具，与 HoYoverse、游戏发行商、XXMI、ZZMI、3DMigoto 或任何 Mod 平台没有隶属关系。

请遵守游戏、原 Mod 作者和发布平台的规则。使用 JiggleForge 适配其他作者的 Mod，并不代表获得修改、重新上传或重新分发该 Mod 的许可。请为重要 Mod 保留备份。
