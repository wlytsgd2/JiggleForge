# v0.1.26-beta.2 — 骨骼权重 / Bone weights

这是预发布测试版，不作为正式版自动更新；正式用户无需安装。
This is an opt-in prerelease, not a stable automatic update.

- 设置新增骨骼权重开关：开启使用共同骨骼的当前顶点权重，关闭回到原距离场。
- 两种模式均使用“最大位移”参数，没有额外 0.03 上限。
- 隐藏鼠标时暂停交互和新增的大型骨骼搜索／缓冲准备。
- 修复部分绘制路径的对象常量缓冲子区域读取。
- 离线三维实验室包含 15 项物理参数及拖动、回弹、拍打、滚轮深度交互。

Close the game completely, install/update the global runtime using this app,
then restart the game. Switching the bone option also requires a full restart.
Existing adapted Mods do not need re-adaptation just for this option.
The offline lab previews stationary fixtures; it does not reproduce all game
animation, picking latency, masks, groups, or dependencies.

完全退出游戏后，用此应用安装／更新全局运行时，再启动游戏。切换骨骼开关后
也需完整重启。既有适配 Mod 不必仅为这个开关重新适配。
这次上传保留已测试的 beta.2 压缩包，不包含后续尺寸估计实验。

Package SHA-256:
`d2c81ea1b8c37c0d76ec2abf5f7968da2f96c8fe620c64bc6059b9cbf0f0d889`
