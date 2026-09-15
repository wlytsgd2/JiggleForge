# Changelog

## 0.1.23 — 2026-09-15

### 简体中文

- 适配本次游戏更新后的角色选择/剧情/服装界面不透明轮廓 VS：`aa59281029db3a5a` 更新为 `3dc9820c6068ed8b`。
- 适配本次游戏更新后的菜单界面不透明轮廓/附加材质 VS：`160b58ea1824c794` 更新为 `59238ab48197031d`。
- 两个新版 VS 的材质常量缓冲区由 118 个 `float4` 扩展为 126 个；变形注入已按照新的常量索引和分量读取重新移植，轮廓继续只接收主体状态而不参与鼠标拾取。
- 应用安装、更新、完整卸载、手动安装包和开发 Hash 对照表已同步更新；安装新运行时会清理旧轮廓替换文件。

### English

- Updated the opaque outline VS used by character selection, story, and outfit scenes from `aa59281029db3a5a` to `3dc9820c6068ed8b` for the current game update.
- Updated the menu opaque outline/additional-material VS from `160b58ea1824c794` to `59238ab48197031d`.
- Both new shaders expand their material constant buffer from 118 to 126 `float4` entries. The deformation injection was rebased onto the new indices and component reads; these outline passes remain receiver-only and do not participate in mouse picking.
- Application install, update, full removal, the manual package, and the developer hash matrix now use the new shaders. Installing the new runtime also removes the obsolete outline replacements.

## 0.1.22 — 2026-09-01

### 简体中文

- 重构适配 Mod 的身份和运动状态：现在使用完整 `ProjectId + GroupId` 定位项目私有状态，同一组共用一个状态，不同作者发布的 Mod 即使组编号相同也不会互相冲突。
- 配置格式升级到 schema 4，并把重复的执行代码集中到全局运行时；项目 INI 只保留资源、局部参数和必要调用，Draw 检测器关闭后不再保留其测试文件和编号代码。旧适配 Mod 更新后只需在应用中重新点击一次“应用配置”。
- 项目运动状态改为帧末单缓冲更新，保留原版全局组和旧 ABI 兼容路径；分组、传递依赖、独立物理参数和关闭 Draw 变形继续按项目私有状态运行。
- Mask 现在直接绑定 Mod 内填写的 DDS，不再复制到 `_JiggleForgeRuntime\Masks`；多个 Draw 使用同一路径时共用一个资源，并在重新应用配置时清理旧副本。正式配置关闭检测器时也不再生成重复的 `y26 = 0`。

### English

- Reworked adapted-Mod identity and motion state around the full `ProjectId + GroupId`. Draws in one group share one private state, while independently published Mods cannot collide even when they reuse the same local group number.
- Upgraded the configuration format to schema 4 and moved repeated execution logic into the global runtime. Project INIs now retain only resources, local parameters, and required calls; disabling the Draw Inspector removes its temporary files and Draw-number code. Existing adapted Mods only need one new Apply Configuration pass after updating.
- Switched project motion to a frame-end single-buffer update while retaining the global original-parts group and legacy ABI compatibility. Groups, transitive dependencies, per-group physics, and disabled Draws continue to operate on project-private state.
- Masks now bind the DDS path inside the Mod directly instead of copying files into `_JiggleForgeRuntime\Masks`. Draws using the same path share one resource, legacy copies are cleaned on the next apply, and production configurations no longer emit the redundant `y26 = 0` assignment when the inspector is off.

## 0.1.21 — 2026-08-28

### 简体中文

- 新增公开的单 Draw 完全旁路接口：在目标 Draw 前设置 `z112 = 1`，并在 Draw 后恢复 `z112 = 0`。
- 旁路时，替换 VS 会尽早跳过 JiggleForge 的 Mask、状态遍历、位移、屏幕基向量和法线重建等附加计算。
- 旁路表面仍参加游戏原有深度测试；命中鼠标像素并通过深度测试时会清空拾取包，因此它不会被选中，也不会让鼠标穿过并拾取后方普通几何。
- 保留应用现有“关闭 Draw 变形”开关的原有行为，并增加中英文开发文档与运行时契约测试。

### English

- Added a public per-Draw full-bypass interface: set `z112 = 1` immediately before the target Draw and restore `z112 = 0` immediately afterward.
- The replacement VS now bypasses JiggleForge mask sampling, state iteration, displacement, screen-basis construction, and normal reconstruction as early as practical.
- A bypassed surface still participates in the game's depth test. When it covers the cursor pixel and passes depth, it clears the pick packet, preventing both self-selection and ordinary click-through selection of geometry behind it.
- Preserved the application's existing Disable Deformation behavior and added bilingual developer documentation plus runtime contract coverage.

## 0.1.20 — 2026-08-24

### 简体中文

- 新增 NPC 身体轮廓 VS `29837c29e23201fd`，使该轮廓 pass 可以复用主体的 JiggleState 并同步变形。
- NPC 轮廓只消费已有状态，不参与鼠标拾取；位移同时应用到主裁剪位置和第二套位置输出，避免轮廓与主体错位。
- 桌面应用、完整正式包、手动安装包和手动卸载清单均已登记这一新 VS。

### English

- Added NPC body-outline VS `29837c29e23201fd`, allowing the outline pass to reuse the body JiggleState and deform in sync.
- The NPC outline only consumes existing state and never participates in picking; displacement is applied to both position paths to keep the outline aligned with the body.
- Registered the new VS in the desktop application, full release, manual package, and manual-removal list.

## 0.1.19 — 2026-08-22

### 简体中文

- 允许 JiggleForge 与 RabbitFX 在同一个材质像素着色器中共享标准 `t120` INI 参数槽位。
- 继续使用 JiggleForge 私有的 `t119` 与 `u7` 作为重复注入保护，不改变拾取、运动状态或适配 Mod 接口。
- 完整正式包与手动安装包现在包含完全相同的共享槽位运行时。

### English

- Allowed JiggleForge and RabbitFX to share the standard `t120` INI-parameter slot in the same material pixel shader.
- Kept the JiggleForge-private `t119` and `u7` resources as duplicate-injection guards without changing picking, motion state, or the adapted-Mod interface.
- The full release and manual package now contain the exact same shared-slot runtime.

## 0.1.18 — 2026-08-21

### 简体中文

- 将旧适配 Mod 使用的 `CommandListEnableAdaptedOnly` 恢复为无副作用的兼容空操作。
- 删除未被当前适配器使用、且引用不存在着色器文件的回退拾取清除路径及其状态变量。
- 保留 v0.1.17 的鼠标左键与 `X` 键默认值、手动包换行修复和全部已验证的内联拾取路径。

### English

- Restored `CommandListEnableAdaptedOnly`, used by legacy adapted Mods, as a side-effect-free compatibility no-op.
- Removed the unused fallback-pick discard path and state variables that referenced a nonexistent shader file.
- Retained the v0.1.17 left-button and `X` defaults, manual-package line-ending fix, and all verified inline-picking paths.

## 0.1.17 — 2026-08-21

### 简体中文

- 将已经过游戏验证的内联拾取运行时固定为本次候选版本，保留特殊材质像素着色器兼容路径和适配 Mod 的回退拾取隔离。
- 应用安装、内置运行时、滚轮输入桥接器和手动安装包现在统一默认支持鼠标左键与 `X` 键拖动。
- 增加默认多按键安装和运行时契约测试，防止正式打包与本地测试版本再次出现差异。

### English

- Frozen the game-verified inline-picking runtime as this release candidate, retaining the special material-pixel-shader compatibility path and adapted-Mod fallback-pick isolation.
- Application installs, the embedded runtime, WheelBridge, and the manual package now consistently enable both left-button and `X`-key dragging by default.
- Added default multi-key installation and runtime contract coverage so official packages cannot silently diverge from the locally tested candidate again.

## 0.1.16 — 2026-08-21

### 简体中文

- 将鼠标拾取内联到实际材质像素着色器，在材质裁剪之后使用可见表面的深度结果，改善未适配 Mod 中可见模型与可拖动区域不一致、鼠标穿透前方部件的问题。
- 适配 Mod 的每个 `DrawIndexed` 现在直接向运行时提供独立的 ObjectID 和 Draw 身份，继续支持独立状态、分组、依赖关系与纹理 Mask。
- 新增对已占用 `SV_POSITION.xy` 输入布局的材质 PS 的兼容，并由主体 VS 提供世界坐标、屏幕基向量和表面法线。
- 删除旧的第二次拾取与范围拾取路径，避免后一次无条件拾取覆盖有效结果；移除不再符合当前拾取模型的“关闭原版变形”开关。
- 配置 schema 升级到 3；旧配置仍可读取，旧适配 Mod 的兼容接口继续保留为空操作。
- 内联拾取使用提前深度/模板处理，因此摄像机近距离下的角色淡出可能会保持为清晰、不透明状态。

### English

- Moved cursor picking into the actual material pixel shader. Visible-surface depth is evaluated after material clipping, improving alignment between rendered geometry and draggable areas in unadapted Mods and reducing picks through foreground parts.
- Every adapted `DrawIndexed` now supplies its own ObjectID and Draw identity directly to the runtime while retaining independent state, groups, dependency edges, and texture masks.
- Added compatibility for material pixel shaders that already consume `SV_POSITION.xy`; body vertex shaders now export world position, screen basis vectors, and the surface normal.
- Removed the old second-pick and range-pick paths so a later unconditional pick cannot overwrite a valid result, and removed the “disable original deformation” option because it no longer matches the current picking model.
- Bumped the configuration schema to 3. Older configurations remain readable, and the legacy adapted-Mod compatibility interface remains available as a no-op.
- Inline picking uses early depth/stencil processing, so close-camera character fading may remain visually clear and opaque.

## 0.1.15 — 2026-08-08

### 简体中文

- 将发布包整理为根启动器、`App` 应用组件、`Runtime` 游戏运行时和 `docs` 文档目录。
- 为根启动器和 WinUI 主程序显式声明 `asInvoker`，主界面以当前用户的普通权限运行；只有确实需要提权的独立操作才请求 UAC。
- 更新应用更新、完整性校验、运行时安装和卸载路径，使旧版平铺目录能够迁移到新结构。
- 将有组织的发布目录固化到正式打包流程，后续正式版继续使用相同结构。
- 新增不含 EXE、DLL 或命令脚本的手动安装包，默认同时支持鼠标左键和 X 键拖动，并附带纯文本卸载代码。

### English

- Organized the release package into a root launcher, `App` application components, `Runtime` game runtime, and `docs` documentation directories.
- Explicitly declared `asInvoker` for both the root launcher and WinUI application so the main interface runs with the current user's standard privileges; only operations that genuinely require elevation request UAC separately.
- Updated application-update, integrity-verification, runtime-installation, and removal paths so an older flat installation can migrate to the organized layout.
- Made the organized release layout part of the official packaging flow so future stable releases keep the same structure.
- Added a manual package containing no EXE, DLL, or command script. It enables both left-button and X-key dragging by default and includes removal code as plain text.

## 0.1.14 — 2026-08-07

### 简体中文

- 移除会复制到临时目录、提权并在主程序退出后批量删除应用文件的自删除卸载链，降低 Defender 启发式误报风险。
- “准备卸载”仍可停止滚轮、卸载运行环境，并选择保留兼容层或恢复全部适配 Mod；最后打开应用目录，由用户手动删除文件夹。
- 独立更新器恢复为只负责校验和安装应用更新，不再承担应用自删除功能。

### English

- Removed the self-deleting uninstall chain that copied an executable to a temporary directory, elevated it, and deleted application files after the main process exited, reducing Defender heuristic false-positive risk.
- “Prepare removal” still stops WheelBridge, removes the runtime, and either keeps the compatibility layer or restores adapted Mods; it then opens the application directory for manual deletion.
- Restored the standalone updater to update-only responsibilities; it no longer deletes the application itself.

## 0.1.13 — 2026-08-07

### 简体中文

- 新增可配置的游戏内总开关，默认按键为 `F7`；切换时会在游戏左上角短暂显示 `JiggleForge Enabled` 或 `JiggleForge Disabled`。
- 关闭后停止变形并跳过拾取、物理模拟、自动校准、分组注册和适配 Draw 等主要 GPU 工作；再次开启时会安全清理并重建运行状态。
- 保留稳定的自动 VS 替换流程，修复早期总开关方案重复执行外层 Draw、导致替换 Mod 出现三角形材质缺口的问题。
- 在设置中加入应用卸载入口，可选择保留零计算兼容层，或校验备份并恢复全部已记录、已发现的适配 Mod 后完全卸载。
- 完整性清单现在包含发布目录中的所有应用文件，使独立卸载器能够安全删除整个 JiggleForge 应用。

### English

- Added a configurable in-game master switch, using `F7` by default, with short `JiggleForge Enabled` and `JiggleForge Disabled` messages at the top left.
- Disabling deformation now skips the main picking, physics, automatic calibration, group-registration, and adapted-draw GPU work; enabling it again safely clears and rebuilds runtime state.
- Kept the stable automatic VS replacement path and fixed the earlier master-switch implementation that replayed outer draws and caused triangular material holes on replacement Mods.
- Added application uninstall options in Settings: keep a zero-computation compatibility layer, or validate backups, restore every recorded/discovered adapted Mod, and fully uninstall.
- Included every published application file in the integrity manifest so the standalone uninstaller can safely remove the complete JiggleForge application.

## 0.1.12 — 2026-08-07

### 简体中文

- 将 Mod 列表改为“已适配历史记录 + 当前 ZZMI 中带适配标记的 Mod”，不再把管理器容器、合集目录或普通未适配 Mod 误认为项目。
- 历史记录保存在用户的本地应用数据中，应用更新和移动不会清除；丢失记录时仍可从当前 ZZMI 重新发现已适配项目。
- 改进多 INI、包装目录和错误拖入目录的识别，并验证原始备份能够完整恢复 JiggleForge 实际修改的多个 INI。
- 将项目检查、路径校验、备份、运行环境、异常和完整性校验提示统一迁移到中英文资源系统。
- 加入本地化自动测试；中文或英文资源缺失、代码引用无对应翻译时，构建测试会失败。
- 为独立更新器和滚轮输入桥接器加入中英文故障提示。

### English

- Replaced broad Mod discovery with adapted-project history plus marker-based discovery under the current ZZMI, avoiding manager containers, collections, and ordinary unadapted Mods.
- Stored project history in per-user local application data so application updates and relocation preserve it, while adapted projects can still be rediscovered from the current ZZMI.
- Improved handling of multi-INI Mods, wrapper folders, and incorrectly selected folders, with tests confirming that original backups restore every INI JiggleForge changed.
- Migrated project inspection, path validation, backup, runtime, exception, and integrity-verification messages to the unified Chinese and English resource system.
- Added localization tests that fail when either language or a referenced translation key is missing.
- Added bilingual failure messages to the standalone updater and wheel-input bridge.

## 0.1.11 — 2026-08-07

- Added complete Simplified Chinese and English application interfaces.
- Added a bilingual language choice before the first-run guide and a language
  selector in Settings.
- Replaced literal-text substitution and packaged-app language overrides with a
  unified resource-key localization service that works in the unpackaged,
  self-contained application.
- Localized static controls, dynamic status messages, dialogs, onboarding,
  templates, and tooltips through the same language resource set.
- Improved the global drag-key layout so longer English labels wrap cleanly and
  remain readable at narrower window sizes.
- Added automated checks for resource parity, missing XAML localization keys,
  and untranslated Chinese XAML literals.

## 0.1.10 — 2026-08-07

- Added an automatic Mod library that scans the selected ZZMI `Mods` directory
  and opens detected projects directly from the application.
- Distinguished real Mod roots from collection, manager, resource, and global
  runtime folders instead of treating every first-level directory as one Mod.
- Added safe handling for dragging a ZZMI root, the complete `Mods` directory,
  wrapper folders, and paths containing non-ASCII characters.
- Added ZZMI root validation and automatic correction when a user selects its
  parent directory or a child such as `Mods`.
- Refreshed library state after adaptation, restoration, repair, inspector, and
  runtime operations.
- Added automated coverage for path validation, Unicode paths, nested Mod
  discovery, wrapper correction, and invalid library selections.

## 0.1.9 — 2026-08-06

- Replaced fixed scene cursor offsets with automatic previous-frame calibration
  from both role-texture composition and whole-scene composition passes.
- Added wallpaper-scene calibration through the shared 857 composition shader,
  filtered against the role texture produced earlier in the frame.
- Added combat-scene calibration through the unique full-screen
  `788ff53c1e1d1227` composition pass.
- Removed the post-Skin fallback pick so restored and otherwise unadapted Mods
  cannot lose the valid pre-Skin selection to a later incompatible buffer.
- Restored the public `activePickProfile` compatibility symbol for Mods adapted
  by earlier releases, while newly generated patches use `activePickPipeline`.
- Added runtime ABI contract tests so public variables, resources, and command
  lists used by already-adapted Mods cannot be removed accidentally.
- Disabled the blue runtime diagnostic overlay in distributed builds while
  preserving the independently controlled per-Mod Draw inspector.

## 0.1.8 — 2026-08-04

- Updated the recommended global physics defaults for a smaller deformation radius, quicker target following, freer release motion, and stronger tap response.
- Applied the new defaults once on first launch of v0.1.8, while preserving every adapted Mod's independently saved group parameters.
- Added a Settings button for restoring the current recommended defaults at any time.
- Clarified the update dialog with an explicit release-notes section; future updates do not reset user defaults unless a dedicated migration is intentionally added.

## 0.1.7 — 2026-08-03

- Added the QQ community group to Settings with one-click group-number copying.
- Added the community invitation to the final onboarding step and new-version dialog.
- Added and refined standalone ShaderFixes diagnostics for troubleshooting runtime loading.

## 0.1.6 — 2026-08-03

- Persisted the selected ZZMI root between application launches.
- Reused the saved root for runtime installation, updates, WheelBridge, and default-physics writes.
- Kept an invalid saved path visible so users can repair it instead of silently reverting to the default.

## 0.1.5 — 2026-08-03

- Bundled the Windows App SDK 1.8 runtime in the self-contained Windows release.
- Removed the requirement for users to install Windows App Runtime separately.
- Added release-package validation for the required app-local Windows App SDK files.

## 0.1.4 — 2026-08-03

- Added GitHub repository and Bilibili introduction links to the Settings page.
- Opened both links with the system default browser from the new project links card.

## 0.1.3 — 2026-08-01

- Added short-click tap deformation along the picked triangle normal.
- Added tap detection based on hold time and cursor travel, while preserving normal drag and release behavior.
- Reused the existing release impulse, spring, damping, strength, and maximum-offset parameters for configurable tap response.
- Extended captured-pick diagnostics and CPU/GPU parity coverage for the new interaction path.

## 0.1.2 — 2026-07-31

- Added startup checks for the latest stable GitHub Release.
- Added an optional one-click update flow with a persistent title-bar reminder when an update is postponed.
- Added release-package SHA-256 verification, per-file installation verification, and a separate rollback-capable updater.
- Added application update and integrity controls to the Settings page.

## 0.1.1 — 2026-07-31

- Added per-Mod `JiggleForge.original.zip` backups before first adaptation.
- Added Overview-page restore to return a Mod to its pre-adaptation files.
- Added backup validation, checksums, rollback handling, and Core tests.
- Added the interactive first-run guide and made the Chinese README the public entry page.
- Clarified English/Chinese user documentation, installation, compatibility, and backup behavior.

## Unreleased

- Prepared a public-facing repository layout.
- Added English and Simplified Chinese user guides.
- Documented build, troubleshooting, configuration, runtime architecture, and shader hash maintenance.
- Kept generated Mod resources and local game captures out of the source tree.
- Selected GPL-3.0-only for original source code and documented separate branding and third-party material boundaries.

## 2026-07-31 — Independent runtime baseline

- Formalized the independent runtime structure under `StandaloneShaderFixes/JiggleForge/runtime`.
- Consolidated global shader includes under `StandaloneShaderFixes/ShaderFixes/JiggleForgeRuntime`.
- Renamed transitional reset-frame and test artifacts to their formal runtime names.
- Verified the solution with the .NET test suite and GPU runtime parity checks.
