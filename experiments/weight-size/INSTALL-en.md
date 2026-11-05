# WeightSize r1 · Install and revert

This is a complete manual TEST runtime. No previous JiggleForge app or test is
required. Use an up-to-date, working XXMI / ZZMI installation. No EXE, DLL or
.NET installation is included or required.

1. Fully exit the game; F10 is not sufficient. Identify the ZZMI root actually
   selected in XXMI, containing d3d11.dll, d3dx.ini, Mods and ShaderFixes.
2. Back up Mods/JiggleForgeShaderFix, ShaderFixes/JiggleForgeRuntime and the
   replacement files listed in SHADER-HASHES.txt OUTSIDE ZZMI. If the old runtime
   lives elsewhere, find its main INI with `namespace = jiggle_forge` and move
   that runtime aside. Disable other bone-control experiments. Never load two
   runtimes or leave INI backups inside Mods. Do not move or edit character Mods.
3. Merge this package's Mods and ShaderFixes into the ZZMI root, replacing matching
   files. The INI must be ZZMI/Mods/JiggleForgeShaderFix/JiggleForge.ini, not inside
   an extra package-level folder.
4. Move matching HASH-vs_replace.bin / HASH-vs.bin caches for the listed hashes
   to the external backup, if present. Preserve the new .txt sources. Do not
   clear all ShaderFixes or remove unrelated Mod shaders.
5. Restart. First compilation may pause briefly. Show the cursor, hold X or the
   left mouse button to drag, click to tap, and use F7 to toggle. Wheel depth
   requires the existing WheelBridge; no bridge executable is bundled here.

The package uses its own global defaults: base maximum offset 0.05, measured-size
ceiling 0.04 and shared reference 0.03. Existing Mod parameter files are untouched;
larger base group caps are limited to 0.05 in this test, smaller caps are preserved.
Other per-group physics values are not reset. The cap scales with the size factor.

Do not update the runtime from an older app while testing. For a fixed-size
comparison set `global $sizeAdaptationEnabled = 0` in the main INI and restart.

## Revert

Exit, restore the backed-up files listed in manifest.json and remove only test
files that did not exist before installation. If the old runtime was elsewhere,
remove the newly installed runtime directory before returning it to its previous
location. Avoid duplicate INIs. Move matching generated .bin caches aside again,
preserve restored .txt sources and restart.

Do not delete all runtime files while adapted Mods still reference them. For full
removal, use the app's compatibility-layer uninstall or restore adapted Mods first.
The accompanying SHA-256 file checks ZIP integrity. This v0.1.26-beta.3 pre-release
does not enter other users' stable update channel.
