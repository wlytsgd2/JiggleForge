# Quick start

JiggleForge starts an interactive interface tour on first launch. It moves through each feature page and highlights the actual controls. After completing it, you can reopen it at any time from **Guide** at the bottom of the navigation pane. Choosing **Later** does not mark the tour as complete, so it will appear again on the next launch.

## 1. Install the global runtime

1. Extract the self-contained JiggleForge release.
2. Open **Settings**.
3. Select the ZZMI root, usually the folder that contains `Mods` and `ShaderFixes`.
4. Click **Install or update runtime**.
5. Return to the game and press `F10`.

The runtime installation is global. A Mod still needs to be adapted separately.

## 2. Adapt a Mod

1. Drag the replacement Mod folder into the application.
2. The first import scans the Mod's INI files and creates `JiggleForge.txt` and private generated resources in the Mod folder.
3. Review the Draw list. Rename aliases only when they help you identify the part; the original Draw identity remains stable.
4. Place Draws into groups. Use the dependency page to connect groups that should share deformation.
5. Assign a DDS mask when only part of a Draw should deform. Without a mask, the default weight is `1.0`.
6. Adjust the global/default or per-group physics values.
7. Click **Apply configuration**.

The original Mod draw commands remain in the source INI. JiggleForge adds the generated runtime sections and does not require the source Mod to remain in its original location after the project has been generated.

## 3. Test in the game

1. Return to the game and press `F10`.
2. Hold a configured drag key over a visible opaque body part.
3. Move the mouse to deform in the frozen screen plane.
4. Start WheelBridge from **Settings** if you want the mouse wheel to move along the frozen screen normal.
5. Enable the Draw inspector only while identifying a Draw. Disable it for normal play.

When the camera rotates during one drag, the drag basis remains frozen. Release the key and start another drag to use the new view direction.

## 4. Reopen an existing project

Opening a folder containing `JiggleForge.txt` loads the saved project. On the first adaptation, JiggleForge automatically creates `JiggleForge.original.zip` in the Mod root before changing any source INI. The archive contains only the files that JiggleForge needs to change, not the whole Mod.

When a valid archive is present, the Overview page shows **Restore original Mod**. Confirming it restores the original INI bytes and any pre-existing runtime files, removes the JiggleForge configuration/generated runtime, and keeps the archive for another restore. Mods adapted by older builds without this archive cannot be restored byte-for-byte by the new application.

After installing a new global runtime, apply the project once so its generated sections use the current runtime contract. Keep an independent copy before changing a project created by an older build.
