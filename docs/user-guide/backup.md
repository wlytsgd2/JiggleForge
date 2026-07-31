# Mod backup and restore

JiggleForge creates `JiggleForge.original.zip` in a Mod's root immediately before the first adaptation. The archive contains the source INI files referenced by the discovered Draws and any JiggleForge files that already existed at that moment. It does not copy the whole Mod.

The archive is kept after a restore. This makes it safe to test a configuration, restore the original Mod, and adapt it again later. A Mod created by an older build without this archive cannot be restored byte-for-byte by the current application; make an external copy before applying a major update to such a project.

## 恢复原始 Mod

1. 在应用中打开已适配的 Mod。
2. 进入“概览”，确认备份状态显示为有效。
3. 点击“恢复原始 Mod”并确认。
4. 应用会还原源 INI 的原始字节，删除 `JiggleForge.txt`、`_JiggleForgeRuntime` 和配置迁移备份，然后重新显示为可首次适配状态。

备份压缩包会保留在 Mod 文件夹中，不会被 3DMigoto/ZZMI 当作配置加载。
