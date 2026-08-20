# Changelog

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
