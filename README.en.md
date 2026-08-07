# JiggleForge

[简体中文](README.md)

JiggleForge is an open-source Windows desktop application for *Zenless Zone Zero* running through XXMI/ZZMI. It adds real-time vertex deformation to supported character rendering paths, allowing players to drag characters with the mouse, control depth with the wheel, and create a slap-like impulse with a quick click.

The global runtime can process supported original character geometry directly. For third-party replacement Mods, JiggleForge can also analyze actual Draws, generate a dedicated configuration, and provide groups, dependencies, masks, and per-group physics.

> JiggleForge is under active development. Game updates, scenes, LODs, transparent materials, special shaders, and third-party Mods may affect compatibility.

## Features

- Install, update, verify, or remove the global JiggleForge runtime.
- Drag models with the mouse, control depth with the wheel, and create tap impulses with a quick click.
- Disable deformation and its main GPU work with a configurable in-game master switch (`F7` by default).
- Adapt an existing replacement Mod in place without its Blender project or a new model export.
- Give each Draw a stable identity, editable alias, and independent deformation switch.
- Group multiple Draws and assign independent physics to every group.
- Connect groups through transitive directed dependencies.
- Control deformation weights inside a Draw with DDS texture masks.
- Identify small or overlapping parts with the optional in-game Draw Inspector.
- Create an original backup automatically before first adaptation and restore the Mod from the app.
- Remember adapted projects and rediscover Mods carrying JiggleForge markers in the current ZZMI.
- Provide Simplified Chinese and English interfaces, onboarding, application updates, and integrity verification.

## Requirements

- 64-bit Windows 10 version 19041 or newer, or Windows 11.
- The Windows version of *Zenless Zone Zero*.
- The latest official XXMI/ZZMI release.
- A fully extracted official self-contained JiggleForge package.

The official package includes the required .NET and Windows App SDK runtime components. A separate installation is normally unnecessary. Do not launch the application from inside its ZIP archive.

## Quick Start for Players

1. Download the latest `win-x64` archive from [GitHub Releases](https://github.com/wlytsgd2/JiggleForge/releases) and extract it completely.
2. Run `JiggleForge.exe`, choose a language, and follow the first-run guide.
3. In **Settings**, select the ZZMI root actually used to launch the game. The correct folder normally contains both `Mods` and `ShaderFixes`.
4. Close the game if possible, then select **Install or update runtime**.
5. Start the game, or return to it and press `F10` to reload configuration.
6. Place the cursor over a supported character surface, hold a configured drag key (left mouse button by default), and move the mouse.
7. For third-axis control, start WheelBridge from **Settings** and scroll while the drag key is held.

Characters can now be dragged, but replacement Mods may not respond correctly without adaptation. An unadapted Mod can only use approximate original-geometry picking, which may miss parts, select the wrong location, or respond only in some areas. If you want accurate replacement-Mod support or separate parameters for each Mod, continue with the Mod-creator section below.

## For Mod Creators

This section is intended for authors who want to publish JiggleForge-adapted Mods, and for advanced users who want to configure a specific replacement Mod precisely. JiggleForge does not require the original Blender project or a new model export. An existing replacement Mod that already works through XXMI/ZZMI can be adapted directly.

### Adapting a Replacement Mod

1. Drop one specific Mod folder onto the JiggleForge home page, or select that folder manually.
2. Do not select the complete `Mods` directory, the ZZMI root, or a Mod-manager collection folder.
3. JiggleForge inspects INI files, Draw commands, and resource references, and creates a backup before the first change.
4. Review the detected Draws. Use the Draw Inspector in game when a part needs to be identified.
5. Configure Draw aliases, groups, dependencies, masks, and per-group physics.
6. Select **Apply configuration**.
7. Return to the game, press `F10`, and test the relevant characters, scenes, and camera views.

Adapted projects are remembered on the home page. JiggleForge does not treat every ordinary Mod or manager container as a project: it keeps projects that were actually opened and adapted, and scans the current ZZMI only for explicit JiggleForge markers.

### Generated Files

An adapted Mod normally contains:

```text
JiggleForge.txt
_JiggleForgeRuntime
JiggleForge.original.zip
```

- `JiggleForge.txt`: the project configuration source.
- `_JiggleForgeRuntime`: private runtime resources generated from that configuration.
- `JiggleForge.original.zip`: the pre-adaptation backup; it is not required during gameplay.

### Draws, Groups, and Original Parts

A Draw represents one actual rendering path found in the source Mod. It does not necessarily correspond to one complete visible body part.

- Draws in one group share deformation state and physics parameters.
- Multiple Draws forming one continuous component can share a group.
- Left and right legs should use separate groups when they need independent responses.
- A Draw with deformation disabled can still be shown by the inspector, but it does not generate or receive deformation.
- **Original Parts** represents geometry not replaced by the current Mod and still rendered through the game's original path. Its switch is separate from normal replacement Draws and is disabled by default for new projects.

The Draw Inspector is intended for configuration and diagnostics. Disable it for regular gameplay after the parts have been identified.

### Dependencies

A directed edge:

```text
A -> B
```

means that dragging group A also affects group B. Dependencies are transitive. For example:

```text
Body -> Underwear
Underwear -> Coat
```

allows a drag originating from `Body` to reach `Coat`, reducing clipping between the body and clothing.

Dependencies can also create effects such as lifting or pulling clothing. Put the body and clothing into separate groups and add only the required dependency direction. Clothing can then move independently when dragged without automatically pulling the body, while selected outer layers can still follow inner components when needed.

### Texture Masks

JiggleForge reads the red channel of a DDS texture as deformation weight:

- Black: `0.0`, no deformation.
- White: `1.0`, full deformation.
- Gray: partial deformation.
- Missing or invalid file: fallback to `1.0`.

Multiple Draws may share one mask. The mask must match the model's UV layout. It changes vertex deformation weight, not which triangles the cursor can pick.

### Physics

Global defaults in **Settings** apply to original characters and new projects. Every group can override them independently. Major parameters include:

- Influence radius and falloff exponent.
- Strength, drag scale, and maximum offset.
- Hold/release frequency and damping ratio.
- Release/tap impulse.
- Target follow time.
- Wheel depth step and minimum/maximum depth.
- Volume response.

Save or apply changes before returning to the game and pressing `F10`.

## In-Game Controls and Performance

- Drag key: left mouse button by default; multiple keys can be configured.
- Mouse wheel: controls depth toward or away from the camera while a drag key is held; requires WheelBridge.
- Quick click: creates a tap and rebound along the picked triangle normal.
- `F10`: reloads XXMI/ZZMI configuration.
- `F7`: disables or enables JiggleForge; the key is configurable.

Disabling JiggleForge does more than block mouse input. It skips the main picking, physics, automatic calibration, group-registration, and adapted-Draw GPU work. Adapted Mods continue to render through the compatibility path.

## Backup and Restore

Before first adaptation, JiggleForge creates `JiggleForge.original.zip` in the Mod root. It stores only the source files the application is about to modify and relevant runtime files that already existed. It does not copy the complete Mod.

To restore:

1. Open the project in JiggleForge.
2. Go to **Overview** and confirm that the backup is valid.
3. Select **Restore original Mod** and confirm.

The application restores the original INI bytes, removes the project configuration and generated private runtime, and keeps the backup archive for another restore later. A Mod adapted by an old version without a valid backup cannot be guaranteed a byte-for-byte restore; make an external copy first.

## Updates and Integrity Verification

The application checks the latest stable release at startup. An update can be installed immediately or postponed; postponing keeps a reminder beside the application name.

One-click update downloads both the release ZIP and its SHA-256 file and replaces files only after verification succeeds. **Settings → Application update and integrity** can check again, reinstall the latest release, or verify the current installation.

## Uninstallation

### Remove Only the Global Runtime

Stop WheelBridge in **Settings**, then select **Uninstall runtime**. This removes the global deformation runtime and restores ShaderFix files backed up during installation.

Adapted Mods may still need either the JiggleForge runtime or its compatibility layer to render correctly. Do not delete random generated INI blocks from adapted Mods.

### Remove the Complete Application

Select **Prepare removal**, then choose:

- **Keep compatibility layer**: remove deformation work while retaining project configuration and backups, so adapted Mods continue to render without deformation.
- **Restore Mods**: validate backups, restore every recorded or discovered adapted Mod, and remove the global runtime.

After either operation, JiggleForge opens its application folder and exits. Delete the application folder manually. JiggleForge intentionally avoids an elevated self-deleting uninstaller to reduce antivirus heuristic false positives.

## Troubleshooting

### Nothing Happens in Game

- Update to the latest official XXMI/ZZMI.
- Confirm that JiggleForge points to the ZZMI root actually launching the game.
- Reinstall the JiggleForge global runtime.
- Confirm that `F7` has not disabled the runtime.
- Return to the game and press `F10`; restart the game completely when necessary.

### A Draw Name Appears, but the Model Does Not Deform

The inspector or part of the runtime is loaded, but the deformation state, shader pass, Draw binding, or global runtime does not match. Update XXMI/ZZMI and JiggleForge, reinstall the runtime, and apply the Mod configuration again.

### Only Some Areas Respond

Possible causes include approximate picking on an unadapted Mod, an unregistered shader pass, transparent/outline/shadow/close-camera paths, an invisible mesh winning the pick, a disabled Draw, a black mask, or an outdated configuration.

### The Model Is Broken, White, or Flickering

First verify that the original Mod works without JiggleForge. Do not copy configurations between characters, game versions, or LODs. Restore and adapt again when resource layouts change.

### Cursor and Deformation Positions Do Not Match

Scenes may use different projection, canvas-scaling, or composition paths. Record the resolution, window mode, and exact scene, and provide `d3d11_log.txt` plus a FrameAnalysis capture of the affected scene.

Before reporting any issue, verify that the latest official XXMI/ZZMI is installed. Modified, repackaged, or outdated distributions are not guaranteed to work.

## Documentation and Support

- [Quick start](docs/user-guide/quick-start.md)
- [Configuration](docs/user-guide/configuration.md)
- [Backup and restore](docs/user-guide/backup.md)
- [Troubleshooting](docs/user-guide/troubleshooting.md)
- [Shader hash matrix](docs/development/shader-hash-matrix.zh-CN.md)
- [Build instructions](docs/development/building.md)
- [Bilibili introduction video](https://www.bilibili.com/video/BV1Bt3d6DEYN/)
- QQ group: `451901293`

Whether you have a problem, a suggestion, want to keep up with updates, or simply want to chat, you are welcome to join.

## Repository Layout

- `app/JiggleForge`: WinUI 3 desktop application.
- `src/JiggleForge.Core`: scanning, configuration, patching, and runtime generation.
- `src/JiggleForge.WheelBridge`: wheel-input bridge.
- `src/JiggleForge.Updater`: standalone updater.
- `StandaloneShaderFixes`: global runtime and supported VS replacement sources.
- `tests`: automated tests.
- `docs`: user and developer documentation.

## License and Disclaimer

JiggleForge source code is licensed under [GNU GPL-3.0-only](LICENSE). See [BRANDING.md](BRANDING.md) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for project branding and third-party material.

This is an independent community project and is not affiliated with HoYoverse, the game publisher, XXMI, ZZMI, 3DMigoto, or any Mod platform.

Follow the rules of the game, original Mod creators, and hosting platforms. Adapting another creator's Mod with JiggleForge does not grant permission to modify, reupload, or redistribute that Mod. Keep backups of important Mods.
