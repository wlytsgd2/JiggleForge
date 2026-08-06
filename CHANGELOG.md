# Changelog

## 0.1.8-beta.3 — test build

- Replaced fixed scene cursor offsets with automatic previous-frame calibration
  from both role-texture composition and whole-scene composition passes.
- Added wallpaper-scene calibration through the shared 857 composition shader,
  filtered against the role texture produced earlier in the frame.
- Added combat-scene calibration through the unique full-screen
  `788ff53c1e1d1227` composition pass.
- Removed the post-Skin fallback pick so restored and otherwise unadapted Mods
  cannot lose the valid pre-Skin selection to a later incompatible buffer.
- Restored the public `activePickProfile` compatibility symbol for Mods adapted
  by earlier releases, while newly generated patches use `activePickPipeline`.
- Added runtime ABI contract tests so public variables, resources, and command
  lists used by already-adapted Mods cannot be removed accidentally.

## 0.1.8 — 2026-08-04

- Updated the recommended global physics defaults for a smaller deformation radius, quicker target following, freer release motion, and stronger tap response.
- Applied the new defaults once on first launch of v0.1.8, while preserving every adapted Mod's independently saved group parameters.
- Added a Settings button for restoring the current recommended defaults at any time.
- Clarified the update dialog with an explicit release-notes section; future updates do not reset user defaults unless a dedicated migration is intentionally added.

## 0.1.7 — 2026-08-03

- Added the QQ community group to Settings with one-click group-number copying.
- Added the community invitation to the final onboarding step and new-version dialog.
- Added and refined standalone ShaderFixes diagnostics for troubleshooting runtime loading.

## 0.1.6 — 2026-08-03

- Persisted the selected ZZMI root between application launches.
- Reused the saved root for runtime installation, updates, WheelBridge, and default-physics writes.
- Kept an invalid saved path visible so users can repair it instead of silently reverting to the default.

## 0.1.5 — 2026-08-03

- Bundled the Windows App SDK 1.8 runtime in the self-contained Windows release.
- Removed the requirement for users to install Windows App Runtime separately.
- Added release-package validation for the required app-local Windows App SDK files.

## 0.1.4 — 2026-08-03

- Added GitHub repository and Bilibili introduction links to the Settings page.
- Opened both links with the system default browser from the new project links card.

## 0.1.3 — 2026-08-01

- Added short-click tap deformation along the picked triangle normal.
- Added tap detection based on hold time and cursor travel, while preserving normal drag and release behavior.
- Reused the existing release impulse, spring, damping, strength, and maximum-offset parameters for configurable tap response.
- Extended captured-pick diagnostics and CPU/GPU parity coverage for the new interaction path.

## 0.1.2 — 2026-07-31

- Added startup checks for the latest stable GitHub Release.
- Added an optional one-click update flow with a persistent title-bar reminder when an update is postponed.
- Added release-package SHA-256 verification, per-file installation verification, and a separate rollback-capable updater.
- Added application update and integrity controls to the Settings page.

## 0.1.1 — 2026-07-31

- Added per-Mod `JiggleForge.original.zip` backups before first adaptation.
- Added Overview-page restore to return a Mod to its pre-adaptation files.
- Added backup validation, checksums, rollback handling, and Core tests.
- Added the interactive first-run guide and made the Chinese README the public entry page.
- Clarified English/Chinese user documentation, installation, compatibility, and backup behavior.

## Unreleased

- Prepared a public-facing repository layout.
- Added English and Simplified Chinese user guides.
- Documented build, troubleshooting, configuration, runtime architecture, and shader hash maintenance.
- Kept generated Mod resources and local game captures out of the source tree.
- Selected GPL-3.0-only for original source code and documented separate branding and third-party material boundaries.

## 2026-07-31 — Independent runtime baseline

- Formalized the independent runtime structure under `StandaloneShaderFixes/JiggleForge/runtime`.
- Consolidated global shader includes under `StandaloneShaderFixes/ShaderFixes/JiggleForgeRuntime`.
- Renamed transitional reset-frame and test artifacts to their formal runtime names.
- Verified the solution with the .NET test suite and GPU runtime parity checks.
