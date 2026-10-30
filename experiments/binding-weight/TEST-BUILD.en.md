# JiggleForge 0.1.26-beta.2 · Local test build

Not uploaded to GitHub or offered as a stable automatic update.

Extract into a new folder and launch the root `JiggleForge.exe`. Exit the game completely, confirm the ZZMI folder in Settings, install/update the global runtime, then restart the game. F10 alone cannot reliably discard old shader/resource caches.

Settings includes **Use bone weights**, enabled by default. Apply a change and restart the game completely. ON multiplies the original spatial field by the current vertex’s shared-bone weights without an additional displacement cap. Both modes use the configured Maximum offset. OFF restores the original field and configured maximum offset, skipping bone searches and per-mesh weight-view preparation. Both modes retain hidden-cursor sleep/cancellation and the constant-buffer-region fix. OFF is not identical to uninstalling.

Global and Mod/group physics editors have **Preview in lab**, passing all 15 current editor values and the switch. Alternatively open `App/Lab/index.html`. Left-drag, click to tap, wheel while holding for depth; right-drag to orbit and wheel while idle to zoom. Parameter JSON import/export is available. Browser edits never modify app/game configuration.

The spatial field and motion formulas are ported from the runtime with a fixed 60 Hz simulation. Stationary local fixtures cannot reproduce game animation, frame timing, pick latency, calibration, masks, groups or dependencies. Bone mode uses a nearest vertex in the same fixture stream, up to four bone IDs and the 0.04 distance rejection threshold. It does not claim anatomical or physical accuracy.

Existing adapted Mod interfaces are unchanged; this option alone does not require re-adaptation. Existing user physics values are preserved. To return to stable, exit the game and update its runtime from the stable app.

Diagnostic support is retained, with the yellow HUD disabled by default. Shader compilation, signatures, independent GPU replays and lab interactions are tested; not every character/device has been verified in-game. Model/game assets belong to their respective rights holders and are included for local research tests. Three.js license: `App/Lab/vendor/THREE-LICENSE.txt`.
