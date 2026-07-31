# Configuration

`JiggleForge.txt` is the editable project source. Generated INI sections, masks, Draw inspector resources, and runtime bindings are derived from it.

## Draws and groups

- A Draw identifies one adapted execution path from the source Mod.
- A group gives one or more Draws shared physics parameters.
- `OriginalParts` represents game geometry that the selected Mod did not replace. Its toggle is separate from adapted Draws.
- A Draw with deformation disabled can still remain visible to the inspector, but it does not register a motion state or consume group dependencies.

## Dependencies

An edge `A -> B` means that a drag owned by A also affects B. The application compiles the graph into a deduplicated state list. Cycles are rejected or collapsed before generation.

Use the edge list when you need exact group names. Use the interactive graph when you need to understand the whole relationship at a glance.

## Masks

The red channel of a DDS mask is the deformation weight:

- black = `0.0`, no deformation;
- white = `1.0`, full deformation;
- gray = partial deformation;
- missing or invalid mask = `1.0` fallback.

The mask must follow the vertex/UV layout expected by the selected Draw. A texture mask does not change which triangles are picked.

## Physics

The most important parameters are:

- radius and falloff — spatial influence;
- strength and drag scale — response to mouse movement;
- max offset — world-space displacement limit;
- hold/release frequency and damping — spring feel;
- release impulse — inherited drag-release motion and short-click tap strength;
- wheel depth step and min/max depth — third-axis wheel control;
- volume response — the amount of surrounding volume preservation.

The group value overrides the Mod default for Draws in that group. Save the page before pressing `F10`.

A valid press released within `0.20` seconds and within `10` pixels of its starting point is treated as a tap. The surface is first pushed opposite the picked triangle normal, then rebounds through the selected group's release spring and damping.
