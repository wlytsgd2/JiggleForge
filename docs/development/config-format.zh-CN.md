# JiggleForge.txt 配置格式

`JiggleForge.txt` 是桌面应用保存的唯一可编辑项目配置。修改后的 INI、Mask 资源绑定和识别器数据都是由它生成的运行结果。

首次适配成功后，应用会自动切换到 Draw 配置页。所有新识别的 Draw 默认不填写 `group`，因此显示在“未分组”中；编译器会为每个未分组 Draw 建立独立的隐式运行状态。之后再次拖入或选择这个 Mod 文件夹，会直接读取本文件并显示配置工作区。点击界面中的“应用配置”后，应用会保存本文件并原地更新运行 INI；回到游戏按 F10 即可重新载入。

`[Physics]` 是当前 Mod 的默认参数，供未分组 Draw 使用，也作为旧配置和新建组的初始值。每个 `[Group:名称]` 会保存同名的 15 项物理参数和项目内稳定的 `local_state`；一个 Draw 同时读取多个依赖组时，各组仍保留自己的参数。修改后必须点击“保存参数”，再回到游戏按 F10。

## 导入状态

- 没有配置且没有 `JIGGLEFORGE_VISIBLE_RANGE` 标记：第一次导入。
- 有配置且运行标记完整：直接打开配置界面。
- 有配置但运行标记缺失或数量不符：根据配置修复运行文件。
- 没有配置但存在运行标记：从补丁标记恢复配置。
- 存在旧版 `_JiggleForge\GraphManifest.json`：迁移旧版配置。

## 示例

```ini
[Project]
schema = 4
project_id = "62bfda16-a9ae-4e9a-97de-d89f8dc00cc7"

[Physics]
radius = 0.12
strength = 0.7
falloff = 1
volume_response = 2
drag_scale = 0.75
hold_damping_ratio = 0.84
hold_frequency_hz = 10
release_damping_ratio = 0
release_frequency_hz = 5
release_impulse = 5
max_offset = 0.1
target_follow_seconds = 0.02
wheel_depth_step = 0.02
wheel_min_depth = 0
wheel_max_depth = 0.15

[Inspector]
enabled = true

[Draw:Draw0001]
alias = "Body_Nude"
deform_enabled = true
source_file = "resources\\Character.ini"
source_section = "CommandListBody"
source_line = 541
branch = "else if $swapvar == 2"
command = "drawindexed = auto"
kind = "auto"
group = "Body"
mask = "Masks\\Body.dds"

[Group:OriginalParts]
local_state = 0
draws = ["Draw0001"]
graph_x = 28
graph_y = 28

[Group:Outerwear]
local_state = 1
draws = ["Draw0002", "Draw0003"]
radius = 0.2
strength = 0.65

[Edges]
edge = ["OriginalParts", "Outerwear"]
```

`wheel_depth_step` 表示每个滚轮刻度改变多少世界深度，`wheel_min_depth` 和 `wheel_max_depth` 定义允许范围。正值沿按下拖动键时冻结的屏幕法向朝向镜头，负值进入屏幕；鼠标 XY 始终使用同一次拖动开始时冻结的屏幕右/上方向。每次新拖动从深度 `0` 开始；若配置范围不包含 `0`，则从范围内离 `0` 最近的端点开始。旧配置中的 `wheel_step` 会直接迁移；角度版配置按旧默认 `8° = 0.02` 换算滚轮灵敏度，并使用默认的有符号深度范围。

`volume_response` 控制 Kelvinlet 的方向性体积响应。`0` 关闭周围顶点的额外收缩、鼓起和剪切，效果接近所有顶点沿拖动方向平行移动；数值越大，软组织的体积联动越明显。默认值为 `2`，建议在 `0` 到 `5` 之间调整。

Schema 4 直接保存正式运行时解算器的物理量：

- `hold_frequency_hz` 和 `release_frequency_hz` 是按住与松手阶段的弹簧固有频率；数值越大，响应越快、越硬。
- `hold_damping_ratio` 和 `release_damping_ratio` 是阻尼比；`1` 接近临界阻尼，较小的值产生更多回弹。
- `target_follow_seconds` 是拖动目标的指数跟随时间常数；越小越跟手，`0` 表示不滤波。
- `release_impulse` 是松手瞬间继承目标速度的比例，同时控制短按拍打力度；`0` 会同时关闭甩动惯性和拍打冲量。

应用读取旧 Schema 配置时会先在同目录生成对应的 `JiggleForge.txt.schemaN.bak`，再迁移并写回 Schema 4。若同名备份已经存在，会使用递增后缀，绝不覆盖旧备份。Schema 1–3 的 `state_namespace`、每 Draw 的 `state_index/object_id`、`[OriginalParts] deform_enabled` 与 `adapted_draws_only` 会被验证后安全迁移或忽略。

每个 `[Draw:...]` 的 `deform_enabled` 控制该 Draw 是否绑定变形状态。设为 `false` 后仍会执行原 Mod 的 `drawindexed` 和精确范围拾取，因此 Draw 检测器仍能显示它；该结果只覆盖同一真实游戏 Draw 流程中较早的 pre-Skin 全局回退候选，不会抢占另一 Draw 在前景中的可见表面。禁用 Draw 不注册物理参数、不绑定 State 列表，也不会读取依赖组的变形。省略该字段时默认为 `true`，因此旧配置保持原有行为。

`OriginalParts` 表示全局默认通道，在 Draw 配置页显示为不可重命名、不可删除的固定组。原版角色和未适配部分始终允许成为拖动目标；它固定使用 `local_state = 0`。

普通组使用大于 0 且只需在当前项目内唯一的 `local_state`。运行时身份是完整的 `ProjectId + GroupId`，因此不同作者的 Mod 即使都使用组 1 也不会碰撞。移入 `[Group:OriginalParts]` 的适配 Draw 使用全局组 0；固定组可以作为依赖边的起点影响其他组，但不能作为依赖边的终点。

`_JiggleForgeRuntime` 是按需生成的运行中间产物。首次适配的未分组 Draw 各自需要独立的隐式运动状态，因此会生成对应 `BeginDrawNNNN`、项目身份、组参数、单一私有运动状态资源和帧末原位更新，但不会在配置中制造同名普通组。移入 `OriginalParts`、允许变形、无 Mask 且检测器关闭的 Draw 则只在原 INI 中保留定位注释，不生成可执行入口。Mask、禁用变形和检测器文件也只在对应功能实际启用时生成；所有运行功能都不再需要后，空运行目录会自动移除。不要手工编辑生成文件。

Draw 配置页以树形结构显示固定 `OriginalParts` 组、“未分组”和普通组。Draw 可以拖到组标题，也可以通过右键菜单移动；普通组可以新建、重命名或删除，删除后内部 Draw 会回到“未分组”。配置仍然只允许每个 Draw 属于一个组。

物理参数页顶部可以选择“Mod 默认参数（未分组 Draw）”或任意组。组内可使用与 `[Physics]` 完全相同的 15 个键；省略的键从 Mod 默认参数开始继承，应用保存后会展开为完整值。旧项目第一次由新版读取时，缺少组参数的组会复制旧 `[Physics]` 数值，因此升级不会改变原有效果。

`OriginalParts` 的组参数适用于移入该组的适配 Draw，以及通过 `OriginalParts → 其他组` 边使用全局状态 0 的目标 Draw。若固定组完全没有适配 Draw或出边，纯原版部件仍使用运行环境中的全局默认参数。

依赖边会自动计算传递关系。例如同时存在 `A → B` 和 `B → C` 时，拖动 A 会影响 A、B 与 C，不需要额外添加 `A → C`。环形依赖会合并所有可达状态，并通过去重避免无限递归和重复应用。

`graph_x` 和 `graph_y` 只保存桌面应用互动图里的节点位置，不参与游戏内变形计算；两项都可以省略，旧项目打开后会自动排列节点。依赖页的“边列表”和“互动图”共用 `[Edges]` 数据，在任一视图中的修改都会同步到另一视图。

## Draw 检测器

`[Inspector] enabled` 控制游戏内 Draw 检测器。首次适配原 Mod 时默认为 `true`，也可以在应用的 Draw 配置页面通过按钮直接切换。

开启后回到游戏按 F10，再拖动模型；左上角会显示实际捕获的 `DrawNNNN`、别名、来源节、三角形序号和三个 IB 顶点编号。检测器用完整项目 GUID 和组 ID 判断结果是否属于当前项目。关闭时，应用会移除检测器生成的 INI、HLSL、`drawSeen` 和 Draw 编号写入，只保留变形所需内容；这不影响变形功能。

字符串和数组采用 JSON 转义规则，因此 Windows 路径中的反斜杠写成 `\\`。应用负责读写这些转义，通常不需要用户手工处理。
