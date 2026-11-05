# WeightSize r1 · 安装与回退

这是完整手动游戏运行时，无需先装 JiggleForge 应用或旧实验包。需要已经能正常
加载 Mod 的最新版 XXMI / ZZMI。不包含 EXE、DLL，不需要安装 .NET。

1. **完全退出游戏**。不能只按 F10。确认 XXMI 实际指向的 ZZMI 根目录，
   应有 `d3d11.dll`、`d3dx.ini`、`Mods`、`ShaderFixes`。
2. 把旧 `Mods/JiggleForgeShaderFix`、`ShaderFixes/JiggleForgeRuntime` 和
   `SHADER-HASHES.txt` 所列 hash 的替换文件，完整备份到 **ZZMI 之外**。
   若旧运行时在别处，找到 `namespace = jiggle_forge` 的主 INI，移走那份运行时。
   停用其他骨骼控制实验；不要同时加载两份运行时，别把 INI 备份放在 Mods 内。
   不移动或修改角色 Mod。
3. 将本包的 `Mods` 和 `ShaderFixes` 两个文件夹合并进 ZZMI 根目录，允许覆盖。
   最终应为 `ZZMI/Mods/JiggleForgeShaderFix/JiggleForge.ini`，不要多套一层文件夹。
4. 若 ShaderFixes 内存在上述 hash 的 `HASH-vs_replace.bin` 或 `HASH-vs.bin`，
   将这些缓存移到外部备份，让加载器重新编译共享头文件。
   不清空整个 ShaderFixes，不移走新的 `.txt`，不删除其他 Mod 文件。
5. 重启游戏。首次编译可能停顿。显示鼠标后按住 **X 或左键**拖动，短按拍打，
   **F7** 总开关。滚轮深度依赖原有 WheelBridge，本包不附带桥接程序。

本包采用包内的全局参数，基础最大位移为 0.05；有效估计尺寸封顶 0.04，再按
参考尺寸 0.03 缩放。已适配 Mod 的参数文件不改；大于 0.05 的基础上限仅在
本测试内受限，更小上限保留。其他组参数不重置。

不要用旧应用更新运行时。比较尺寸开关时，修改主 INI 的
`global $sizeAdaptationEnabled = 1` 为 `0`，完全重启游戏。

## 回退

退出游戏，根据 manifest.json 恢复对应安装前备份；只有原先不存在的本包文件才
移除。旧运行时原来在别处时，先移除这次新增的主 INI 所在运行时目录，再把旧版
放回原处，避免双份加载。移走上述生成的 `.bin` 缓存，保留恢复好的 `.txt`，重启。

不要直接删除全部运行时然后留下已适配 Mod 的引用。要彻底卸载，请用应用的
兼容层卸载方式，或先恢复这些 Mod，再卸载。SHA-256 文件用于检查压缩包完整性。

本包作为 v0.1.26-beta.3 预发布版提供，不会成为其他用户的正式版自动更新。
