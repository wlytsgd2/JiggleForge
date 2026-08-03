# ShaderFixes 加载诊断

这些文件是临时测试文件，不会由 JiggleForge 自动安装，也不应长期留在游戏的 `ShaderFixes` 文件夹中。

## 测试角色菜单

1. 完全退出游戏。
2. 将 `ShaderFixes/JiggleForge-Diagnostic-Menu-C280.txt` 复制到实际启动游戏所使用的 ZZMI `ShaderFixes` 文件夹。
3. 启动游戏并进入角色菜单。
4. 如果角色主体模型消失，说明这个 ZZMI 的 `ShaderFixes` 路径和 VS Override 生效。
5. 退出游戏并删除该诊断文件。

## 测试街区

使用 `ShaderFixes/JiggleForge-Diagnostic-Street-2621.txt`，进入街区场景。主体消失表示街区的 `ShaderFixes` 路径和对应 VS Hash 生效。测试后删除文件。

## 结果解释

- 主体消失：ShaderFixes 确实被加载，且该场景的 Hash 匹配；如果 JiggleForge 仍不变形，应继续检查全局运行时、Draw 开关、Mask 和状态绑定。
- 主体不消失：可能选择了错误的 ZZMI 根目录、`d3dx.ini` 没有包含 `ShaderFixes`、游戏使用了不同的 VS Hash，或该场景没有经过该 pass。
- 只在一个场景消失：ShaderFixes 路径正常，但另一个场景的 Hash 不匹配。

`handling = skip` 会故意跳过一次绘制，因此诊断文件必须在测试后删除。
