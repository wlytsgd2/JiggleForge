# JiggleForge

[中文版](README.md)

JiggleForge is a Windows desktop application for 《Zenless Zone Zero》 XXMI/ZZMI replacement Mods. It converts a replacement Mod into a configurable mouse-drag deformation Mod.

## Features

- Installs, updates, checks, and removes the global JiggleForge runtime.
- Does not scan ordinary Mods. Projects that are opened and adapted are remembered, and the current ZZMI is searched only for explicit JiggleForge adaptation markers so those projects can be reopened from the home page.
- Scans a Mod's DrawIndexed commands and adapts the Mod in place.
- Assigns each Draw a stable identity, with editable aliases and deformation switches.
- Groups Draws and edits directed dependencies, so related parts can deform together.
- Supports per-group physics, global defaults, multiple drag keys, short-click taps, and wheel-controlled depth.
- Supports DDS masks: white deforms fully, black does not deform, and missing masks default to weight `1.0`.
- Includes an optional in-game Draw inspector for identifying small or overlapping parts.
- Creates `JiggleForge.original.zip` before first adaptation and restores the original Mod from the Overview page.
- Checks the latest stable GitHub Release at startup, with one-click update or a persistent title-bar reminder when postponed.
- Checks for updates, reinstalls the latest version, and verifies installed files from **Settings**.
- Asks for Simplified Chinese or English on first launch, with language switching available later in **Settings**.

## Requirements

- Windows 10 version 19041 or newer, or Windows 11.
- XXMI/ZZMI and a supported game installation.
- A self-contained JiggleForge release package. No separate .NET installation is required.

Game updates can change shader hashes. If a pass is no longer recognized, update the [VS hash matrix](docs/development/shader-hash-matrix.zh-CN.md) before adapting new Mods.

## Quick start

1. Download and extract the latest self-contained package from [Releases](https://github.com/wlytsgd2/JiggleForge/releases).
2. Start `JiggleForge.exe`, choose a display language, and follow the first-run guide.
3. Open **Settings**, select the ZZMI root folder containing `Mods` and `ShaderFixes`, and install or update the runtime.
4. Drop one concrete Mod folder onto the home page, or choose the folder manually. After successful adaptation, the project is remembered on the home page for direct access later.
5. Review Draws, groups, masks, dependencies, and physics, then click **Apply configuration**.
6. Return to the game and press `F10`.
7. Hold a configured drag key to drag the model, or briefly click to tap along the picked triangle normal. Start WheelBridge from **Settings** if wheel depth is needed.

## Application updates

The app checks the latest stable GitHub Release at startup. A new version can be installed immediately or postponed; postponing leaves a highlighted reminder beside the app name.

One-click update downloads both the ZIP and its SHA-256 file. JiggleForge closes and replaces its files only after verification succeeds. The separate updater attempts to roll back if replacement fails. **Settings → Application update and integrity** can check again, reinstall the latest version, or verify the current installation at any time.

`v0.1.1` and earlier do not contain the updater. Install `v0.1.2` or later manually once; subsequent releases can use one-click update.

The app edits the selected Mod folder directly. The first adaptation creates `JiggleForge.txt`, generated runtime files, and an original backup archive in that folder.

## Backup and restore

The backup file is `JiggleForge.original.zip` in the Mod root. It contains only the source files JiggleForge changed and any relevant files that already existed. It is kept after restoration, so the Mod can be restored repeatedly.

Mods adapted by an older build without this archive cannot be restored byte-for-byte by the current app. Make an external copy before reapplying such a Mod.

## Important behavior

- A group contains Draws that share a deformation state.
- A directed edge `A -> B` means dragging group A also affects group B.
- A missing mask means the whole Draw participates.
- Runtime installation is global; each Mod still needs separate adaptation.
- A Mod must be tested after pressing `F10`, and the game should be closed while installing or updating the runtime.
- Not every character, scene, LOD, transparent pass, or third-party replacement Mod is guaranteed to use the same shader layout.

## Documentation

- [Quick start](docs/user-guide/quick-start.md)
- [Backup and restore](docs/user-guide/backup.md)
- [Configuration](docs/user-guide/configuration.md)
- [Troubleshooting](docs/user-guide/troubleshooting.md)
- [VS hash matrix](docs/development/shader-hash-matrix.zh-CN.md)
- [Build instructions](docs/development/building.md)

## Project layout

- `app/JiggleForge` — WinUI 3 desktop application.
- `src/JiggleForge.Core` — scanning, configuration, patching, and runtime generation.
- `src/JiggleForge.WheelBridge` — wheel input bridge.
- `src/JiggleForge.Updater` — separate application updater.
- `StandaloneShaderFixes` — global runtime and supported VS replacement sources.
- `tests` — automated tests.
- `docs` — user and developer documentation.

## License and disclaimer

The original source code is licensed under [GNU GPL-3.0-only](LICENSE). See [BRANDING.md](BRANDING.md) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for project branding and third-party material.

JiggleForge is an independent community tool and is not affiliated with the game publisher, XXMI, or 3DMigoto. Follow the rules of the game, Mod authors, and distribution platforms.
