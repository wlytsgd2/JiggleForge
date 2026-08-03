# ShaderFixes 加载诊断

这些文件是临时的 HLSL 替换 Shader，不会由 JiggleForge 自动安装，也不能长期留在游戏目录中。

`ShaderFixes` 中的 `*-vs_replace.txt` 是 HLSL 源码，不是 INI 配置。诊断文件必须使用与目标 Shader 完全相同的文件名，临时覆盖正常运行时文件。

## 测试角色菜单

1. 完全退出游戏。
2. 备份实际 ZZMI 中的 `ShaderFixes/c280f6945b23a42a-vs_replace.txt`。
3. 将 `ShaderFixes/Menu/c280f6945b23a42a-vs_replace.txt` 复制到实际 ZZMI 的 `ShaderFixes`，覆盖同名文件。
4. 启动游戏并进入角色菜单。
5. 如果匹配的角色主体向屏幕右侧明显偏移，说明这个 ZZMI 的 ShaderFixes 路径和 c280 VS 替换都在实际运行。
6. 退出游戏，恢复备份；也可以在 JiggleForge 中重新安装运行时。

## 测试街区

使用 `ShaderFixes/Street/26214fb5eedfcbdd-vs_replace.txt` 覆盖实际 ZZMI 中的同名文件，然后进入街区场景。主体明显向右偏移表示 2621 VS 替换生效。测试后恢复正常运行时文件。

## 结果解释

- 主体明显向右偏移：ShaderFixes 确实被加载，且该场景的 Hash 匹配；如果 JiggleForge 仍不变形，应继续检查运动状态、Draw 开关、Mask 和资源绑定。
- 主体没有变化：可能选择了错误的 ZZMI 根目录、游戏使用了不同的 VS Hash、替换 Shader 编译失败，或该场景没有经过这个 pass。
- 只在一个场景偏移：ShaderFixes 路径正常，但另一个场景的 Hash 或 Shader 编译存在问题。

诊断文件依赖正常安装的 `ShaderFixes/JiggleForgeRuntime` include 文件夹。复制后如果 `d3d11_log.txt` 报编译错误，应先重新安装 JiggleForge 全局运行时。
