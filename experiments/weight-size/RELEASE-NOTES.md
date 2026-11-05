# v0.1.26-beta.3 — 权重区域尺寸估计 / Weight-region sizing

**测试版 / Pre-release。不替换 v0.1.25 正式版，不通过正式版自动更新推送。**

基于 v26 的骨骼权重版本，在新抓取时估计权重区域尺寸，并冻结到本次拖动和回弹结束。参考尺寸 0.03，尺寸上界 **0.04**；基础最大位移 **0.05**，仍随尺寸缩放，最高约 **0.0667**。原先更小的上限保留。无需重新适配 Mod，也不修改角色 Mod 文件。

## 下载哪个？

- **完整应用**：`JiggleForge-win-x64-v0.1.26-beta.3.zip`。自包含，解压后运行根目录 `JiggleForge.exe`，用本版应用安装／更新全局运行时。
- **纯手动运行时**：`JiggleForge-manual-v0.1.26-beta.3.zip`。不含 EXE/DLL，按包内说明放入 ZZMI。与应用内置运行时源文件一致，不含滚轮桥接程序。
- `.sha256` 是对应压缩包的校验文件，不是安装包。

安装前完全退出游戏并备份原运行时；**不能只按 F10**。两种方式任选其一，不要同时加载两份运行时。已有参数、分组和历史记录不会主动重置；测试应用与正式应用共用用户设置。不要用旧应用覆盖本版运行时。

开启骨骼权重才使用尺寸估计；关闭则回到原距离场，但仍保留本测试的基础位移限制。应用旧实验室不模拟新尺寸缩放，详情见 `TEST-BUILD.zh-CN.md`。反馈请注明角色/Mod、场景、抓取位置，重点关注大小角色的相对效果、不同抓点的稳定性和抓取瞬间停顿。

已完成编译、独立 GPU 回放、包一致性与临时目录安装验证；未承诺所有游戏环境均正常。回退请退出游戏后用正式应用重新安装全局运行时，不要直接删除运行时后留下适配 Mod。

## English

This is an opt-in **pre-release**, not a stable update. Stable v0.1.25 and its automatic-update channel remain unchanged.

Based on the v26 bone-weight runtime. At each new capture, estimate the selected bone's weighted vertex-region size and freeze it for dragging/rebound. Reference size **0.03**, size cap **0.04**, base offset cap **0.05** followed by size scaling (up to about **0.0667** at the default reference). Smaller existing limits remain smaller. No character Mod edits or re-adaptation required.

Download the **win-x64 ZIP** for the self-contained app, or the **manual ZIP** for executable-free manual runtime installation. Both contain identical runtime sources; only the app includes the wheel bridge. `.sha256` files verify downloads.

Exit the game completely, back up the previous runtime, then install/update using this app or follow the manual instructions. F10 is not sufficient; do not load two runtimes. Existing settings are preserved and shared with stable. Bone weighting must be enabled for sizing; disabling it retains the test's base offset cap. The existing app lab is a baseline preview, not a preview of new sizing.

Compilation, independent GPU replay, package consistency and temporary-directory installation are checked, not all in-game environments. Report character/Mod, scene, grab point, size consistency and capture-time stutter. To roll back, exit the game and reinstall the stable runtime rather than deleting it while adapted Mods still reference it.
