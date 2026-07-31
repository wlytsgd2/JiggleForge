# Changelog

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
