# Rendering and texture resources

The application uses MonoGame WindowsDX for presentation. Field, world and battle scenes submit geometry through the shared triangle renderer to the GPU; software framebuffers remain available for headless rendering. The world terrain uses retained vertex buffers: camera transformation, native horizon curvature, projection and clipping run on the GPU. The procedural title backdrop uses a separate shader.

## Customization points

| Area | Main implementation | Contract |
| --- | --- | --- |
| Fields | [FieldRenderer](../../src/Memoria.FF8.Rendering/FieldRenderer.cs) | Selected field assets, actor visuals and background state produce a reusable RGBA framebuffer. |
| World | [WorldTerrainRenderer](../../src/Memoria.FF8.Rendering/WorldTerrainRenderer.cs) | Retained terrain geometry, textures, camera and actor state feed the selected backend. |
| Battles | [BattleRenderer](../../src/Memoria.FF8.Rendering/BattleRenderer.cs) | Battle geometry and visual state produce the scene framebuffer; the host draws UI separately. |
| Title backdrop | [TitleScene project](../../src/Memoria.FF8.TitleScene/README.md) | Procedural GPU background with host-supplied time and graphics-thread ownership. |
| UI | [MonoGameUiCanvas](../../src/Memoria.FF8.App/GameUi/MonoGameUiCanvas.cs) | Connects the renderer-independent [retained UI API](../../src/Memoria.FF8.Ui/README.md) to MonoGame. |

The [Rendering project README](../../src/Memoria.FF8.Rendering/README.md) remains the detailed field-authoring reference for coordinates, colors, shadows, filtering and fidelity limits. The field's logical canvas is 320 × 240; requested output dimensions preserve its aspect with centered margins. Geometry rasterizes at the selected resolution. Model filtering defaults to bilinear, with nearest available for inspection; original background tiles retain nearest sampling. Preserve world-space camera/depth units and logical texture dimensions when changing output size.

Field backgrounds remain layered animated tiles selected by runtime state. Flattening them into one PNG loses script-controlled layers and compositing. Actor translation, facing, animation, visibility, polygon color and ground shadows are distinct inputs. Rendering consumes game state rather than advancing scripts or animation clocks.

Field FPS selects 30 or 60 presentations per second independently of the fixed 30 Hz field callbacks. At 60, [FieldPresentation](../../src/Memoria.FF8.App/FieldPresentation.cs) retains the last two completed callbacks and samples their fractional presentation phase. Actor positions, facing, control and skin offsets, camera scrolling and parallax interpolate together; model poses sample native fractional animation phases with local quaternion blending. This preserves animation duration, event completion, RNG and saved state, including accelerated gameplay. At 30 the renderer uses the current native pose and camera directly.

Presentation history is transient. Scene replacement, restore, camera changes and explicit instant placement reset the relevant history; an unrelated actor's placement does not discard other actors' movement. Native background wrap endpoints are validated before fractional placement, and markers and shadows follow the same interpolated actor sample. Original tile/texture animation, visibility, shake and other discrete native commands retain their source cadence. Movies retain their detected media frame rate.

The display host yields to drawing after a bounded batch of simulation updates in every scene. Missed display deadlines do not create presentation catch-up work; outstanding simulation time remains in its existing bounded accumulator. Ordinary world callbacks retain the dormant field VM instead of serializing it repeatedly. `GameSession.Save` prepares a current snapshot through the active allocation; callers exporting a `GameState` directly must use `PrepareSnapshot()` so shared process owners match the dormant VM checkpoint.

World presentation expands horizontally to fill wide windows while retaining its original vertical camera scale and centered projection. Portrait windows retain a fitted native 320 × 224 view. The render resolution limit scales the expanded target uniformly; it does not narrow the visible camera region. Movies retain their independent aspect policy.

Battle presentation fills the current window, expanding horizontally in wide windows and vertically in portrait windows. The centered native 4:3 canvas fixes the camera scale, primitive coordinates, damage text and framebuffer capture region; the surrounding target reveals additional scenery without stretching the scene or HUD. Untextured flat screen rectangles covering the native canvas without projected vertex depths extend across the full target, so screen fades also cover the expanded scenery. Partial rectangles, textured captures and projected geometry retain their original coordinate mapping. Standard stage and actor submission retains geometry outside the original screen bounds. Full projected coordinates travel with their logical packet through ordering and snapshot restoration, so distant vertices cannot wrap through signed WORD coordinates. Expanded scene winding uses full coordinates; physical native packet words, scratch, texture coordinates and depth payloads retain their native representation. Native effect packet contracts remain unchanged. Raw native raster diagnostics can continue to render directly into their supplied coordinate canvas.

## Resource providers

[ResourceContracts](../../src/Memoria.FF8.Resources/ResourceContracts.cs) separates logical identities from storage:

- `ResourceKey` is a lowercased, slash-normalized relative path. Rooted paths, empty components, `.` and `..` are rejected.
- `IResourceSource` exposes its descriptor and `Find(ResourceKey)`. `ResourceContent` carries format, revision, provenance and a stream factory. `ResourceService.Get` fails when required content is absent.
- `ResourceSourceDescriptor` declares edition and layouts. Representation compatibility must not be inferred from a package name or physical path.
- `IResourcePrefixSource` optionally supplies bounded reads. `InMemoryResourceSource` supports storage-independent tests.

[GameDataSourceFactory.Open](../../src/Memoria.FF8.Assets/GameDataSourceFactory.cs) selects Remastered when `main.zzz` exists, otherwise Classic. Storage lives in [ClassicGameDataSource](../../src/Memoria.FF8.Assets/ClassicGameDataSource.cs) and the [Remastered provider](remastered-resources.md). Storage selection is separate from playable engine-profile support.

The asset layer also exposes `IGameArchiveSource`, `IGameFileSource`, `IFieldModelSource` and `IBattleModelSource` for consumers needing those capabilities. Resolve those contracts at the composition boundary instead of adding edition checks to renderers.

## Texture lifecycle

```text
logical resource -> source bytes -> format decoder
  -> source texture -> optional replacement resolver
  -> dependent atlas/transformation -> TextureValue/AssetHandle
  -> GPU texture residency and geometry submission, or software framebuffer
```

[TextureContracts](../../src/Memoria.FF8.Resources/TextureContracts.cs) defines `TextureKey` as owner, subresource and palette. `TextureBuildContext` carries logical dimensions, layout, source revision, an original-image callback and optional region/palette/encoded-source data. `ITextureReplacementResolver.Resolve` returns a replacement or no match; adapters must only interpret supported layouts.

`TextureValue` publishes colored RGBA pixels together with logical dimensions, provenance, optional mip levels and variants. Physical resolution may exceed the logical UV canvas. Use `ResourceTextureList.LogicalSize` or the value's logical dimensions for UVs and physical scale for sampling. Supplied mips remain available although current CPU scene renderers sample the base level.

[AssetTextureScope](../../src/Memoria.FF8.Assets/AssetTextureScope.cs) registers source images before atlas packing and releases residency with the asset scope. `TextureTransformContext` declares dependencies over resolved source textures. Its revision changes refresh the builder and dependent graph atomically while retaining handle identity; a failed refresh preserves the previous graph. Format adapters return source-region replacements; the common pipeline constructs engine atlases.

[ResourceService](../../src/Memoria.FF8.Resources/ResourceService.cs) prepares resident values and dependent transforms before committing one generation. Stable `AssetHandle<TextureValue>` instances follow publication; released handles freeze their last value. Dispose subscriptions and release retained textures with their scopes. Initial lazy replacement failures retain source pixels and notify the host; failed reload preparation preserves committed values. The host publishes resources and the mod catalog before delivering callbacks.

The shared [PNG/DDS decoder](../../src/Memoria.FF8.Resources/TextureImageDecoder.cs) serves installed Remastered images and [texture packages](../modding/texture-mods.md). Recognition, search paths, hashes and filename mappings belong in adapters. The current host pumps CPU preparation on its owning thread; worker preparation would need an explicit source/residency threading contract.

## Shaders

The scene effect is [Triangles.fx](../../src/Memoria.FF8.App/Shaders/Triangles.fx). Its world technique transforms retained terrain vertices and reproduces the native integer horizon calculation. Conservative segment bounds exclude invisible terrain before submission; contiguous texture runs preserve opaque draw order, while transparent terrain retains its sorted primitive stream. Mesh identity follows the resolved segment, including terrain replacements and periodic map offsets; texture handles are resolved independently each frame so mod reloads do not require rebuilding geometry. GPU buffers have bounded residency and are released with the renderer. `DrawnTriangles` counts submitted geometry, including triangles subsequently clipped by hardware.

To regenerate the scene shader, run `dotnet tool restore` from the product root, followed by `dotnet tool run mgfxc src/Memoria.FF8.App/Shaders/Triangles.fx src/Memoria.FF8.App/Shaders/Triangles.mgfxo /Profile:DirectX_11`. Commit source and compiled effect together. Ordinary builds embed the compiled effect without invoking a shader compiler.

The optional FPS overlay reports completed presentations as `FPS` and RGB changes in the complete composed output before drawing the overlay as `Changes/s`. Presentation cadence is recorded immediately after presentation completes, independently of pending GPU comparison queries. Identical presentations, including repeated decoded movie frames and still menus, contribute presentations but no visual changes. Visible UI animation counts alongside scene animation. While enabled, two surfaces retain consecutive frames at the output size and multisampling setting. [FrameDifference.fx](../../src/Memoria.FF8.App/Shaders/FrameDifference.fx) discards unchanged pixels; asynchronous occlusion queries report whether any visible RGB byte changed without reading the framebuffer back to the CPU. Samples use completed presentation timestamps, so rendering, pacing and V-Sync waits remain part of the elapsed time. Resize, device reset and unavailable query results discard the measurement baseline. Disabling the overlay releases its GPU resources. Rebuild this effect with `dotnet tool run mgfxc src/Memoria.FF8.App/Shaders/FrameDifference.fx src/Memoria.FF8.App/Shaders/FrameDifference.mgfxo /Profile:DirectX_11` and commit both files together.

The title's readable effect is [TitleScene.fx](../../src/Memoria.FF8.TitleScene/Shaders/TitleScene.fx). Follow the [TitleScene README](../../src/Memoria.FF8.TitleScene/README.md#rebuild-the-shader) for its pinned compiler command, graphics state/lifetime rules, quality caps and deterministic time contract. There is no general package shader-loading API.

Use focused headless checks for the affected resource or rasterization contract; see the [documentation index](../README.md) for build/test instructions. Successful decoding alone does not verify alignment, source-region compatibility or full scene support.
