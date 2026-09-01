# JiggleForge 运行时架构

## 自动坐标校准

运行时不再把场景偏移作为持久配置。它在最终画面合成阶段捕获一张规范化的“屏幕坐标到源纹理 UV”映射，并在下一帧的三角形拾取中读取：

- 独立角色纹理路径：先记录 `6443f79de027d780` 输出的资源；`edf794e6c2599288` 是唯一合成 Draw，可直接捕获；壁纸场景共用的 `857cd16250c6142e` 会在 GPU 上把当前输入与已记录资源的尺寸和多点纹理指纹比较，只有真正消费角色纹理的 Draw 才能写入校准图；
- 完整场景路径：从 `1da80a4543267137` 的全屏场景合成 Draw 捕获映射；
- 两条合成路径分别复用原生 VS 的位置与 UV 计算，把映射栅格化到 `320×180` 浮点纹理；结果保留到下一帧供拾取器使用；
- 每帧只接受上一帧产生的有效路径。场景切换后第一帧允许旧映射失效，随后自动采用新场景映射，不再依赖场景 profile。

这种设计让窗口分辨率、内部渲染比例和画面中的角色纹理位置都由游戏实际合成结果决定，而不是依赖手写比例与偏移。

本文描述当前正式运行时。它不是实验链路，也不存在旧版与新版并行解算。

## 目录

- `StandaloneShaderFixes/JiggleForge.ini`
  - 3DMigoto 入口、资源、按键、场景 VS 和逐帧调度。
- `StandaloneShaderFixes/JiggleForge/runtime`
  - 输入、拾取、运动、参数登记、诊断和形变场的独立 HLSL 实现。
- `StandaloneShaderFixes/ShaderFixes/JiggleForgeRuntime`
  - 供全局 VS replacement 引用的只读形变消费代码。
- `StandaloneShaderFixes/ShaderFixes/*-vs_replace.txt`
  - 按游戏 VS hash 区分的场景接收器。
- `_JiggleForgeRuntime`
  - 应用写入每个已适配 Mod 的生成结果，不属于全局运行时源码。

## 每帧流程

1. 主体 VS 根据当前场景执行世界坐标拾取。
2. 所有候选结果写入本帧拾取缓冲，并按可见性、深度、优先级和流水号选出一个目标。
3. `update_input_cs.hlsl` 处理拖动开始、保持和释放边沿，并冻结抓取时的世界坐标、屏幕右方向和屏幕上方向。
4. `update_motion_cs.hlsl` 更新全局原版组 0 和旧版适配 Mod 的兼容状态。
5. 每个新版适配项目在自己的帧末 `Present` 中绑定项目参数、私有运动状态与完整 GUID，然后调用共享的 `update_project_motion_cs.hlsl` 原位更新各组状态。
6. 场景 VS 通过 `draw_state_consumer.hlsl` 读取当前 Draw 的影响组列表；组 0 读取全局缓冲，正数组读取当前项目的私有缓冲。
7. `deformation_field.hlsl` 在世界空间计算平滑形变，并用同一形变的 Jacobian 变换原始法线、切线和副切线。
8. 半透明、轮廓、近摄像机和附加材质 pass 只消费已有状态，不参与鼠标拾取。
9. 帧末清理瞬时拾取记录；运动状态持续到休眠或被下一次抓取更新。

拾取与绘制允许跨一帧衔接。冻结的抓取记录使用 generation 标识，避免同一按键保持期间被后续 pass 覆盖。

## 核心资源

| 资源 | 记录数 | 用途 |
| --- | ---: | --- |
| `ResourceInputController` | 2 | 当前拖动输入和控制器边沿 |
| `ResourceFramePick` | 10 | 本帧候选，最后两条以 8 个 16 位有限数值无损保存项目 GUID |
| `ResourceCapturedPick` | 9 | 冻结的抓取目标、三角形法线、按住时间和完整项目 GUID |
| `ResourceGroupParameters` | 65536 × 5 | 每个状态的物理参数 |
| `ResourceMotionStates` | 65536 × 7 | 每个状态的运动状态 |
| `ResourceDefaultParameters` | 5 | 未适配原版部件的默认参数 |
| `ResourceRuntimeDiagnosticText` | 256 | 可选诊断文字 |

状态与参数都采用 `float4` 记录，以兼容 3DMigoto 的结构化缓冲绑定。

## Draw 与依赖

应用按实际启用的功能生成 `_JiggleForgeRuntime/Project.generated.ini`。首次适配的 Draw 显示在“未分组”，但每个 Draw 都拥有独立的隐式私有状态。若后来将所有 Draw 移入 `OriginalParts`，并保持允许变形、没有 Mask 且检测器关闭，则该文件和整个 `_JiggleForgeRuntime` 都不存在。需要时，其中包括：

- 完整 128 位 `ProjectId`；
- 每个普通组稳定的项目内 `GroupId`（`OriginalParts` 固定为全局组 0）；
- 每组五条参数记录；
- 每项目一份私有运动状态缓冲；各组线程先把自己的七条记录完整读取到局部变量，完成计算后再原位写回；
- 每个 Draw 的 `PickGroupId` 和编译后的 `InfluenceGroups`；
- 项目级 `BeginPrivateDraw` 公共入口，统一设置可见标记、调用全局 `BeginAdaptedDraw`，并绑定私有运动状态、组参数和项目身份；
- 每个 Draw 的 `BeginDrawNNNN` 轻量入口，只设置 Draw 编号、`z26 = PickGroupId`、影响组和可选 Mask，再调用 `BeginPrivateDraw`；
- 可选的 UV Mask。

原 Mod INI 不再保存上述运行细节。每个 Draw 始终保留两条无执行成本的定位注释；只有实际需要运行功能时，注释内部才形成 `BeginDrawNNNN` → 原 `drawindexed` → 全局 `EndAdaptedDraw` 边界。普通组共用 `ResourceProjectIdentity`，`GroupId` 由 `z26` 传给拾取流程，因此不需要每 Draw Context。位于 `OriginalParts` 的 Draw 不生成入口；只有添加 Mask、关闭变形或开启检测器时才生成最小入口。

依赖图在应用侧计算传递闭包并去重。运行时只遍历编译后的组 ID 列表，不解析组名和图结构。每个 `ResourceDrawInfluencesNNN` 的 `data` 只保存实际组 ID，不含格式标记或数量字段；`array` 就是数量。列表中的 `0` 表示还要读取原版全局状态，正数表示当前项目的私有组；不写 `0` 就不受原版状态影响。关闭变形的 Draw 不生成空列表资源，而是直接把 `vs-t72` 绑定为 `null`。

运行时身份是 `ProjectId + GroupId`，而不是发布者手工分配的全局编号。不同 Mod 可以安全地重复使用组 1。原版和未适配部件使用全零 ProjectId 与全局组 0，并始终允许全局默认候选成为新的抓取目标。全局求解器会在边界把新组 0 映射到旧 ABI 的内部 ObjectID 1；Schema 1–3 的旧适配 Mod 因此仍可通过原 `t75/t76` 全局状态 ABI 运行。

项目状态采用单缓冲是安全的，因为项目更新只在帧末执行，每个 Compute 线程只读写一个组独占的七条记录，当前求解器不会读取其他组的运动状态。这样无需在分散于不同 INI 文件的 `Present` 之间协调0/1槽位切换。全局 `$projectStateReadSlot` 仅作为旧生成文件的固定兼容占位保留，新项目不读取它。

## 物理模型

运动状态显式保存世界位移和速度。按住与松手阶段分别使用固有频率和阻尼比，由稳定的隐式弹簧积分器更新：

- 鼠标 XY 沿抓取开始时冻结的屏幕右/上世界方向；
- 滚轮沿冻结屏幕平面的法向量修改深度；
- `target_follow_seconds` 控制目标低通跟随；
- `release_impulse` 控制松手时继承目标速度的比例，同时作为短按拍打的冲量系数；
- 短按不超过 `0.20` 秒且移动不超过 `10` 像素时，沿捕获三角形法线反方向施加一次冲量；
- `max_offset` 限制世界位移；
- 静止若干帧后状态休眠并归零。

形变场使用有限半径、边界连续归零的正则化体积响应。法线与切线不重新猜测，而是由形变 Jacobian 变换原始输入，因此零位移时保持原 Mod 光照。

## 场景接收器

当前 hash 表见 [shader-hash-matrix.zh-CN.md](shader-hash-matrix.zh-CN.md)。

接收器分两类：

- 拾取并消费：不透明主体 pass，负责产生鼠标候选并读取形变。
- 只消费：半透明、轮廓、深度或材质重绘 pass，只复用同一状态。

这种分离避免半透明排序与不透明深度结果互相争抢鼠标目标，同时保持所有可见 pass 的位置、历史位置和光照一致。

## 诊断

运行时诊断默认关闭。开启后，`build_diagnostic_text_cs.hlsl` 显示控制器、抓取、状态和参数摘要。Draw 检测器属于各 Mod 的临时测试资源，由应用单独启用或关闭。关闭后不会保留检测器 INI、HLSL、`drawSeen` 或有意义的 Draw 编号；正式变形流程的 `SourceDraw` 固定为 0。

## 验证

```powershell
dotnet test JiggleForge.slnx --configuration Release
powershell -ExecutionPolicy Bypass -File tests\shaders\ValidateGpuRuntime.ps1
```

第二条命令会：

1. 用 FXC 编译正式运行时入口和全部受支持 VS；
2. 将 C# 参考模型与 D3D11 Compute Shader 的读回结果逐分量比较；
3. 检查 NaN、无穷值、缓冲越界和资源布局偏差。
