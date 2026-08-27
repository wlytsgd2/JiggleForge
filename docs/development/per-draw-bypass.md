# Per-Draw Full Bypass Interface

JiggleForge exposes a shared IniParams flag that another Mod can use to bypass deformation for a specific Draw while preventing the cursor from selecting geometry behind that Draw:

```ini
z112 = 1
drawindexed = 12345, 0, 0
z112 = 0
```

`z112` is the INI shorthand for `IniParams[112].z`. It addresses the z component of entry 112 in the shared `t120` parameter texture; it is not a VS or PS resource-slot number.

- `0`: run JiggleForge normally; this is the default.
- `1`: the target Draw neither receives deformation nor becomes the picking result.

The caller must set `z112 = 1` immediately before the target Draw and restore `z112 = 0` immediately afterward. JiggleForge deliberately does not reset the flag automatically because an outer game ShaderOverride can execute before the replacement Mod issues its inner Draw; resetting in the outer scope would use the wrong boundary.

## Runtime behavior

The replacement vertex shader checks `z112` as early as practical. When it is `1`, JiggleForge skips screen-basis construction, mask sampling, state evaluation, displacement, and normal reconstruction. Only the original game VS work and the minimal flag/default-output overhead remain.

The injected pixel-shader block does not merely ignore the Draw. The surface still participates in the game's normal rasterization and depth test:

- If the surface does not cover the cursor pixel, it writes nothing.
- If it covers the cursor pixel and passes the depth test, it clears JiggleForge's pick packet to zero.
- A front bypassed surface therefore blocks picking geometry behind it without becoming selectable itself.
- Draws using unusual depth behavior—such as disabled depth writes, depth-always, another depth buffer, or a later depth clear—may behave differently.

## Call boundary

If a command list contains multiple Draws, wrap each target Draw separately. Keep the reset on every executed path:

```ini
if $draw_component
    z112 = 1
    drawindexed = 12345, 0, 0
    z112 = 0
endif
```

This interface is independent of the application's existing Disable Deformation switch. That switch keeps the Draw detectable while preventing it from producing or receiving deformation. `z112` fully bypasses JiggleForge and makes the surface occlude picking of geometry behind it.
