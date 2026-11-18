# Title scene

For FFVII, construct both `TitleSceneRenderer` and `TitleArtworkRenderer` with `TitleSceneStyle.FinalFantasyVII`. The default remains the FFVIII scene described below. Both styles share the existing host-owned menus and presentation clock.

## FFVII meteor

`Shaders/MeteorScene.fx` separates the fixed object-space solid (`MeteorDistance`), its stone material, combustion, ablation sparks and Lifestream sheets. The rock is a ray-marched three-dimensional implicit model with surface normals, irregular relief, faceted stone and sparse hot fissures. Fire wraps the leading edge and streams toward the upper right. The planet is never drawn. The composition has no destination, impact, scene transition, camera travel or finite animation clip: the fire and lower streams continuously advect through envelopes anchored to the base composition. Nothing advances gameplay or changes the save format.

The fire moves faster than the low foreground streams. All animation uses host-supplied presentation time; repeating a time reproduces the background. Work is bounded by the existing HDR surface cap and fixed ray/field sample counts. A reduced composition scale keeps the solid inside narrow viewports. The foreground uses triangulated font outlines for `FINAL FANTASY VII`; it does not load the original sword artwork.

[Artwork/Meteor.obj](Artwork/Meteor.obj) is the reusable closed object-space mesh: 4,514 vertices, 9,024 triangles, outward normals, long axis X, incoming face -X, camera on -Z. It contains geometry and normals; the procedural material and fire remain separate in the shader. Rebuild it with the configured Python command from the product root:

```powershell
python tools/Export-MeteorModel.py
```

The standard-library exporter samples the same solid equations and validates closed topology, winding, nondegenerate triangles and surface error. It uses double precision; GPU floating-point noise can vary slightly by backend. Regenerate the OBJ after changing the solid equations. Optional `--output`, `--longitudes` and `--latitudes` select an output and mesh density.

The resource-free preview accepts `--ff7` in addition to its existing capture/sequence/benchmark options:

```powershell
dotnet run --project tools/Memoria.FF8.TitleScene.Preview -c Release -f net10.0-windows -- --ff7 --resolution 1920x1080 --time 4 --output meteor-title.png
```

The meteor has a stable upper-left key, a teal tail-side rim and a final fill at six percent of key power. Artist source offsets `(-3,+3,+2)`, `(+3,+1,-2)` and `(0,-1,+4)` become object-space toward-light vectors `(-1.797,-3.843,-2)`, `(3.161,+0.083,+2)` and `(-0.341,+0.94,-4)`. Each key/rim visibility query takes at most 24 solid samples with a moderate penumbra, corresponding to a source radius of roughly half the rock radius at the key distance. Microfacet slopes use the geometric tangent frame, with the mass normal controlling the light horizon. There is no ambient term, additive stone edge light or bright overlay; only the golden cracks emit. Rim and flame share `1 + 0.055 sin(3.2t) + 0.025 sin(7.1t)`, bounded to +/-8 percent. The existing fixed exposure shoulder remains `1 - exp(-1.5 color)`; there is no bloom pass.

For lighting authoring, add `--meteor-lighting full|key|rim|fill|key-rim` with `--ff7`. These views select the key, rim, fill or key plus rim; `full` is the ordinary default. Isolated views gate flame, glow, sparks and crack emission so each source can be assessed separately. Use `--background-only` to omit the foreground wordmark. Preview reports name the selected pass. The rock still moves in isolated passes, while the key and fill sources remain fixed. This is an authoring control, with no player configuration option. Library callers can select the same view through `TitleSceneRenderer.MeteorLighting`; FFVIII ignores it.

The meteor uses object coordinates with X along the upper-right tail, Y perpendicular toward the lower-right and Z away from the camera. Screen-space lighting descriptions instead use X right, Y up and Z toward the camera; convert directions between these conventions before using them in the solid's lighting calculations.

`MeteorMotion` samples an inertial pose directly from presentation seconds. The stone spins steadily around its own flight axis at 0.8 degrees/second. Sparse event-seeded impulses arrive 2.2..4.8 seconds apart, tilt it by 0.15..0.3 degrees and settle completely within 0.4..0.8 seconds. Their angular response is the critically damped velocity-impulse solution, with a smooth final cutoff: one deflection and return, without pendulum oscillations. Event timing, direction and strength vary deterministically; there is no continuously oscillating positional noise or frame-by-frame random displacement.

Each impulse also produces a smooth 1.6..2.0-second catch-up excursion along the visible flight axis: 0.002..0.003R forward, then a larger 0.004..0.005R lag, returning exactly to the base position. Transverse movement stays within 0.001R. The excursion finishes before the next event. There is no depth translation, zoom, camera shake or accumulated drift. Camera rays and the fixed lighting rig are transformed into the rotating solid's object space, so surface details and shadows remain attached correctly. `DescribeImpulse` exposes immutable event parameters for authoring and validation; the 4096-slot schedule wraps through an idle interval and has continuous negative-time history.

The flame shell follows the current head, with its radius variation reduced tenfold. Nine analytic position samples cover the preceding 0.25 seconds; delayed motion and small transverse filament flutter grow with squared downstream distance. The far plume reduces its response to transverse head movement, retaining a stable mean axis. Flame detail and sparks advect quickly from head to tail; the plume is never rigidly rotated with the stone. All samples derive from the supplied time, so seeking, pausing and repeating a time need no mutable history buffer or save fields. The static OBJ and implicit solid equations remain unchanged. Preview reports record the requested head offset and maximum tail lag.

Rebuild all three compiled shader profiles and the manifest with `tools/Update-Shaders.ps1` after editing HLSL. Ordinary builds use the embedded effects and need no compiler, model exporter, OBJ loader or game artwork.

## FFVIII fabric

`TitleSceneRenderer` is a shared MonoGame library for the isolated DX11 and Vulkan hosts' animated title backdrop: turbulent crimson fabric and filaments, shaded storm clouds, a pearl upper light and a sunset reflected in water on the right. It uses procedural GPU fields and no raster artwork, content pipeline, game assets, random simulation, or wall clock. The host owns the foreground crest, wordmark and menu. The graphics device must use `GraphicsProfile.HiDef`; the constructor checks before allocating resources.

```csharp
using var title = new TitleSceneRenderer(GraphicsDevice);

// In Draw, before beginning the host's SpriteBatch:
title.Draw(new Rectangle(0, 0, width, height), elapsedSeconds);
spriteBatch.Begin();
// Draw title art, menu and overlays.
spriteBatch.End();
```

Construct, draw and dispose on the graphics thread. Bounds use the current viewport's local coordinates. Call before drawing other content into the target: this renderer briefly switches targets, so a caller target using `DiscardContents` must not contain artwork that needs preserving. Render targets, viewport, scissor, blend, depth, rasterizer, sampler 0 and texture 0 are restored. Begin the host's own batch afterward; the private batch changes shader and vertex bindings. Empty bounds are ignored; nonfinite time throws. Dispose and recreate when replacing the graphics device.

## GPU passes and quality

`Shaders/TitleScene.fx` holds the current implementation. Its compiled WindowsDX effect is embedded in the assembly, so ordinary builds and deployment need no shader compiler or external content files.

1. **Bounded HDR surface field.** A `HalfVector4` target preserves bright filament and cloud lighting until the final highlight shoulder. Its size follows the destination aspect without stretching, capped at **1440 × 1080**, and never exceeds native resolution. A 3840 × 2160 destination uses **1440 × 810**. This target holds the large fabric shapes, torn material detail, warm horizon and water. Angular flow coordinates, low-frequency domain warping and several anisotropic noise scales form the fabric; independent breakup fields keep its highlights from becoming continuous contour lines. Clouds integrate fourteen fixed depth samples through a five-octave 3D density field, including neighboring light-density samples and finer surface lighting. Work below 61% of frame height exits immediately. The fixed target cap and fixed sample count bound this expensive pass.
2. **Native-resolution composite and detail.** The final pass samples that field, then evaluates new directional fabric veins and hairline glints at the destination resolution. These follow the same coarse flow, break into finite irregular lengths, use pixel derivatives for antialiasing, and are masked by the field's red coverage to keep clouds and dark gaps clean. The sun's core, halo, rays, rings and the title underline/center glint are also analytic at native resolution. Tone mapping retains brighter detail instead of clipping it into solid red.

The large cloud and material fields are intentionally resolution capped; they are interpolated above that cap. Native filaments and light details remain newly evaluated at higher display resolutions. This split does not claim that every cloud contour is generated at native 4K resolution.

Positions use screen height as their unit, so curved flow and circular light remain aspect correct. Wider views reveal more of the sides. All motion derives from the supplied seconds; repeating a time reproduces the same background, and pausing the host clock pauses it. CPU work updates uniforms and submits two quads. No ribbon geometry, noise image, or texture data is rebuilt or uploaded per frame. The HDR target is reallocated only when its required dimensions change.

The scene fades to black between 49% and 59% of viewport height. A thin red underline sits at 57.1%, leaving the lower menu area black. The scene contains no castle, text or crest. Those belong to the host's separate foreground renderer.

The native composite still scales with destination pixel count. Measure performance on the target hardware; the surface cap bounds the volume/material work rather than establishing a universal frame budget.

## Light and continuous inward motion

`TitleSceneAnimation` supplies one shared 8-second light pulse (intensity 0.78–1.22). The halo/core size and brightness, cloud illumination, fabric glints, water highlights and foreground silver reflections follow that pulse. Movement uses a separate `MotionSpeed = 0.5` clock: clouds, ripples, rays and the crest's narrow reflection run at half their original speed. The reflection now takes 32 seconds. Foreground paths and buffers remain static.

The fabric is sampled in cylindrical material coordinates around the existing distant point. The material angle advances at 0.0125 radians/second and logarithmic radius at 0.01125/second. Visible features therefore turn slowly and contract toward the point, with fresh procedural material arriving from outside. There is no zoom reset, crossfade between repeated clips, or finite texture to exhaust. Surface and native-resolution detail use the same coordinates.

The cylindrical noise lattice and integer angular harmonics close around a full turn. The shader also unwraps angular derivatives before antialiasing fine veins, avoiding a branch-cut line when the angle wraps. Only the rotation phase is reduced modulo a turn; radial travel remains continuous.

## Rebuild the shader

On Windows, from the product root:

```powershell
pwsh ./tools/Update-Shaders.ps1
pwsh ./tools/Update-Shaders.ps1 -Check
```

The tool manifest pins `dotnet-mgfxc` to **3.8.5.1**, matching the centrally versioned MonoGame packages. Commit the readable `.fx`, all three rebuilt `.mgfxo` profiles and the shader manifest together. See [artifact validation](../../docs/developers/shaders.md). The runtime only needs this assembly and its selected MonoGame dependency.
