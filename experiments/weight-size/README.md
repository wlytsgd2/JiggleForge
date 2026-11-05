# WeightSize r1 · 骨骼权重尺寸测试

这是基于 v26 / binding-weight r11 的独立完整手动运行时，不是正式更新。
不采用之前 Tactile / SizeAdaptive / SizeRegion 的其他变形或运动公式。
不修改 XXMI、角色 Mod、应用保存的配置，也不会自动安装。

v0.1.26-beta.3 同时提供完整应用和手动运行时预发布包；发布说明见
`TEST-BUILD.zh-CN.md` / `TEST-BUILD.en.md`。`payload/` 保留已验证的运行时源文件
原始字节，`runtime-manifest.json` 记录指纹，避免打包时更改算法或行尾。
从仓库构建完整应用：运行 `BuildTestRelease.ps1`。开发算法时可用
`node experiments/weight-size/build.cjs` 重新生成；行尾可能随检出设置不同，
重新生成不替代发布所用的冻结 payload。

## 本版数值

- 全局基础最大位移：`0.05`。适配组原先更小的上限保留；更大的值在本测试运行时
  解码时限制到 `0.05`，不改写 Mod 参数文件。
- 参考尺寸：`0.03`，所有角色共用。
- 估计尺寸上界：`0.04`。这是对有效估计值的截顶，不是超过后拒绝变形。
- 缩放：`s = clamp(min(L, 0.04) / 0.03, 0.25, 4)`。
- 半径和实际运动位移分别乘 `s`；上限同样随尺寸缩放。默认参考尺寸下最大运动
  幅度约为 `0.05 × 0.04 / 0.03 = 0.0667`，不是绝对限幅到 `0.05`。

运动状态上限不等于每个顶点最终合计位移的严格上限：原空间场方向项、多个状态
叠加、mask 和权重仍参与运算。本版也不提供碰撞或不穿透保证。

## 一次抓取如何估计

1. 保留可见像素拾取、最近蒙皮顶点解析和原绑定身份保护。
2. 合并抓点同编号骨骼的权重，选择权重最大的一个；并列取较小编号。
3. 在对应的蒙皮位置/权重视图内，按此骨骼的原始权重 `w` 计算中心和均方根半径：
   `center = sum(w*x)/sum(w)`，`L² = sum(w*|x-center|²)/sum(w)`。
   不归一化每个顶点的权重、不用 w²、不设区域权重阈值、不用骨架父子信息。
4. 只在新的抓取绑定确认时做一次统计。上界 262144 个输入顶点；超出、无有效
   尺寸或无效布局时用倍率 1，仍保留原骨骼过滤。成功后倍率随抓取代际冻结，
   持续拖动和松手回弹不重新扫描。

复用现有确认绑定的一个 Dispatch 和位置/权重读取视图，不新增 GPU 资源缓冲或
全模型副本。这个 Dispatch 改为 128 线程单组，使用约 4 KiB 统计共享暂存。
活动 VS 增加冻结倍率缓存和缩放；隐藏鼠标、F7、z112 的旁路规则沿用 r11。
这不是“完全无额外开销”：新抓取统计可能产生短暂开销，游戏内性能尚待测试。

## 与离线实验室的边界

本版对应实验室的默认“最大抓点权重骨骼 / 阈值 0 / 指数 1”测量和 v26 系数。
每次新抓取自动选择骨骼，不加入实验室的手动骨骼选择、跨抓取锁定或参考记录。
抓点换到另一根主骨骼时，估计对象会变化；0.04 上界不解决骨骼区域语义差异。

运行时统计整个可读取的 Position 视图，不知道 IB 实际绘制子集、隐藏替换部件、
mask、组依赖或真正的拓扑。实验室的导入子集与此可能不同。因此这依然是权重
区域尺度，不是精确胸部大小、组织厚度或解剖识别。正常支持的角色和 Mod 不需
按角色预先录入骨架，也无需修改 Mod 文件；不能宣称所有自定义蒙皮路径都支持。

## 开关

主 INI 的 `JIGGLEFORGE_WEIGHT_SIZE_BEGIN/END` 段：

```ini
global $sizeAdaptationEnabled = 1
global $sizeReferenceLength = 0.03
```

将第一项改为 0 并完全重启游戏，可对比同样基础位移限制下的 v26 固定尺寸。
骨骼权重关闭时，尺寸估计也关闭，恢复原距离场；本测试的基础位移限制仍在。
不要用旧应用“更新运行时”，它会覆盖这个测试版本。

## 建议游戏验证

1. 同一个部位中央/边缘、不同角色相近部位：是否正常抓取、是否仍有明显尺寸跳变。
2. 大小不同 Mod 保持同一套参数：相对形变量是否比固定半径更接近。
3. 隐藏鼠标连续点击战斗、F7 关闭：不应唤醒新的统计或产生变形。
4. 原版、未适配 Mod、已适配组/mask/依赖；选人、菜单、街区、近镜头。
5. 第一次抓取的停顿，以及抓住不松手时是否稳定、有无闪烁。

本地编译和独立 GPU 回放不等于以上项目已经在游戏中通过。

## English

Complete manual TEST runtime based on v26 / binding-weight r11, not Tactile.
No game installation, character edits or custom XXMI is performed by the build.
v0.1.26-beta.3 provides both app and manual pre-release packages. Build the app
with `BuildTestRelease.ps1`; `payload/` and `runtime-manifest.json` freeze the
tested runtime source bytes. See TEST-BUILD.en.md for app-specific differences.
The shared reference is 0.03; measured weighted RMS L is capped at 0.04 before
calculating `s=clamp(L/0.03,0.25,4)`. The base motion cap is 0.05 (smaller existing
group caps remain smaller). Radius and motion including the cap scale together:
the default largest state offset is about 0.0667, not an absolute 0.05.

At binding confirmation, choose the strongest merged captured bone and measure
its linear raw-weight RMS over the available skinned position view. No per-vertex
weight normalization, region threshold, hierarchy, anatomy lookup or connectivity.
The complete view is scanned once, up to 262144 vertices; unsupported/invalid size
uses factor 1. Freeze the factor for this capture and its rebound. No new dispatch,
GPU resource buffer or full-mesh copy; the existing resolve uses 128 threads and
roughly 4 KiB reduction scratch. Some capture-time and active-VS work is added.

The game view may include hidden/unused vertices absent from the lab subset.
The lab's manual bone lock is not a runtime feature: a new grab may select a new
dominant bone. Size is a binding-region proxy, not anatomical size. Existing Mod
ABI and cursor/F7/z112 protections remain. Engine layout limits still apply.
Set `$sizeAdaptationEnabled = 0` and restart for a fixed-size comparison. Turning
bone weighting off also turns sizing off, but retains the test's base offset cap.
Do not update the runtime from an older app while testing. Game feel and FPS must
still be checked; see the included validation report for actual offline coverage.
