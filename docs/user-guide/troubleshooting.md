# Troubleshooting

## The Mod is unchanged

- Confirm that the adapted Mod is enabled in ZZMI.
- Press `F10` after changing settings.
- Verify that the global runtime is installed in the same ZZMI root that launches the game.
- If the game was updated, refresh the VS hash matrix and regenerate the runtime resources.

## Only some areas respond

The picker can only select geometry that is rendered by an accepted main-scene pass. Check the Draw inspector and the scene allow-list. Transparent, outline, shadow, and replacement passes may need separate entries or an explicit exclusion.

If an invisible replacement mesh is still rendered, it can win the pick test. Disable that Draw, or use a mask and dependency graph that matches the visible component.

## A model is broken, white, or flickers

Disable the adapted Mod and verify the original first. Then check that each Draw keeps its original index range, vertex layout, and index buffer. Do not reuse a configuration generated for a different character, LOD, or scene. Re-run adaptation from the original Mod when those resources change.

## The drag position is offset

Check the scene/pass hash selection first. A menu pass and a street pass can use different projection or depth conventions. Re-run FrameAnalysis hunting for the affected scene and update the hash matrix before changing physics values.

## Masks appear to be ignored

The mask must be in the generated Mod directory and use the expected DDS channel/layout. Red-channel black is `0.0`; white is `1.0`; missing or invalid files intentionally fall back to `1.0`. A mask changes deformation weight, not triangle picking.

## Wheel input does not work

Start the WheelBridge from the application and grant the requested elevation if Windows asks. Confirm that the global drag key and wheel bridge are using the same runtime directory. Restart the game after installing or updating the bridge.

For diagnostic output, turn on the Draw inspector in the application. Disable it before normal play if its overlay is distracting.

