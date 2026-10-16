# Title scene

`TitleSceneRenderer` is an isolated WindowsDX/MonoGame library for the animated title backdrop: turbulent crimson fabric and filaments, shaded storm clouds, a pearl upper light and a sunset reflected in water on the right. It uses procedural GPU fields and no raster artwork, content pipeline, game assets, random simulation, or wall clock. The host owns the foreground crest, wordmark and menu. The graphics device must use `GraphicsProfile.HiDef`; the constructor checks before allocating resources.

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

`TitleSceneAnimation` supplies one shared 8-second light pulse (intensity 0.78–1.22). The halo/core size and brightness, cloud illumination, fabric glints, water highlights and foreground silver reflections follow that pulse. The crest also has a narrow 16-second travelling reflection. Foreground paths and buffers remain static.

The fabric is sampled in cylindrical material coordinates around the existing distant point. The material angle advances at 0.025 radians/second and logarithmic radius at 0.0225/second. Visible features therefore turn slowly and contract toward the point, with fresh procedural material arriving from outside. There is no zoom reset, crossfade between repeated clips, or finite texture to exhaust. Surface and native-resolution detail use the same coordinates.



## Rebuild the shader

On Windows, from this directory:

```powershell
dotnet tool restore
dotnet tool run mgfxc Shaders/TitleScene.fx Shaders/TitleScene.mgfxo /Profile:DirectX_11
dotnet build
```

The local tool manifest pins `dotnet-mgfxc` to **3.8.5.1**, matching the centrally versioned MonoGame package. Commit the readable `.fx` and rebuilt `.mgfxo` together. The runtime only needs this assembly and its normal MonoGame dependency.
