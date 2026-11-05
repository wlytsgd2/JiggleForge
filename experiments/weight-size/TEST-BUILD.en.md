# JiggleForge v0.1.26-beta.3 · Weight-region size test

This is a GitHub pre-release, not a stable update. Stable auto-update users will not receive it. The app packages the verified WeightSize r1 runtime without changing its deformation algorithms.

## Installation

1. Extract the full app ZIP into a new directory and start the root `JiggleForge.exe`. Keep the `App` and `Runtime` directories together. .NET and Windows App Runtime are bundled.
2. **Exit the game completely** and back up the previous runtime outside ZZMI. Do not load two JiggleForge runtimes simultaneously.
3. In Settings, confirm the ZZMI directory actually used by XXMI, install/update the global runtime, then restart the game. F10 alone is insufficient.
4. Enable **Use bone weights to limit deformation**. With the cursor visible, hold X or LMB to drag; F7 toggles the runtime. Previously saved key bindings are retained.

Alternatively download `JiggleForge-manual-v0.1.26-beta.3.zip` and follow its instructions. Both downloads contain the same 51 runtime source files. The app also includes the wheel bridge; the manual package contains no executable. Choose either installation method.

## Test changes

- On capture, select the bone with the strongest merged weight at the picked vertex and estimate its weighted vertex-region size. Bones are not rotated.
- Effective size is capped at **0.04**, with a shared **0.03** reference. Influence radius and motion displacement scale together.
- The base maximum offset is limited to **0.05**, then scaled. With the default reference its largest state offset is about **0.0667**, not an absolute 0.05 limit.
- Smaller existing limits are preserved. Larger values are capped only during runtime decoding; existing Mod parameter files are not rewritten.
- Measure once when a new capture binding is resolved, then reuse during dragging and rebound. No extra GPU resource buffer or Dispatch is added, but measurement adds capture-time work.
- Turning bone weighting off also disables size estimation, returning to the original distance field while retaining the test's base offset limit.

Existing parameters are not reset. No Mod re-adaptation or custom XXMI build is required. The app's existing parameter lab remains a baseline v26 preview: **it does not model this size scaling or the 0.05 cap**, and must not be treated as an exact preview of this test.

## Testing and rollback

Compare different character sizes, central/edge grab locations, scenes, original/adapted Mods, and capture-time stutter. Selecting another dominant bone may still change the estimate. This is not anatomical recognition or a non-intersection guarantee.

To roll back, exit the game and install the stable runtime using the stable app, or restore backups following the manual instructions. Do not leave adapted Mods referencing a deleted runtime. Test and stable apps share user settings/history; settings you explicitly save in the test app also affect later stable use.

Shader compilation, GPU replays and app installation flow are validated; in-game results on every character/device are not guaranteed. The general README still applies, except where this test document states a difference.
