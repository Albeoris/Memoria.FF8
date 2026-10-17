# Rendering and texture resources

The application uses MonoGame WindowsDX for presentation. Field, world and battle scene geometry is currently rasterized into CPU RGBA framebuffers and uploaded to MonoGame textures. The procedural title backdrop is a separate GPU shader renderer. Changing a title shader does not change field/world/battle rasterization.

## Customization points

| Area | Main implementation | Contract |
| --- | --- | --- |
| Fields | [FieldRenderer](../../src/Memoria.FF8.Rendering/FieldRenderer.cs) | Selected field assets, actor visuals and background state produce a reusable RGBA framebuffer. |
| World | [WorldTerrainRenderer](../../src/Memoria.FF8.Rendering/WorldTerrainRenderer.cs) | World geometry, textures, camera and actor state feed the CPU renderer. |
| Battles | [BattleRenderer](../../src/Memoria.FF8.Rendering/BattleRenderer.cs) | Battle geometry and visual state produce the scene framebuffer; the host draws UI separately. |
| Title backdrop | [TitleScene project](../../src/Memoria.FF8.TitleScene/README.md) | Procedural GPU background with host-supplied time and graphics-thread ownership. |
| UI | [MonoGameUiCanvas](../../src/Memoria.FF8.App/GameUi/MonoGameUiCanvas.cs) | Connects the renderer-independent [retained UI API](../../src/Memoria.FF8.Ui/README.md) to MonoGame. |

The [Rendering project README](../../src/Memoria.FF8.Rendering/README.md) remains the detailed field-authoring reference for coordinates, colors, shadows, filtering and fidelity limits. The field's logical canvas is 320 × 240; requested output dimensions preserve its aspect with centered margins. Geometry rasterizes at the selected resolution. Model filtering defaults to bilinear, with nearest available for inspection; original background tiles retain nearest sampling. Preserve world-space camera/depth units and logical texture dimensions when changing output size.

Field backgrounds remain layered animated tiles selected by runtime state. Flattening them into one PNG loses script-controlled layers and compositing. Actor translation, facing, animation, visibility, polygon color and ground shadows are distinct inputs. Rendering consumes game state rather than advancing scripts or animation clocks.

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
  -> CPU rasterizer -> MonoGame upload/presentation
```

[TextureContracts](../../src/Memoria.FF8.Resources/TextureContracts.cs) defines `TextureKey` as owner, subresource and palette. `TextureBuildContext` carries logical dimensions, layout, source revision, an original-image callback and optional region/palette/encoded-source data. `ITextureReplacementResolver.Resolve` returns a replacement or no match; adapters must only interpret supported layouts.

`TextureValue` publishes colored RGBA pixels together with logical dimensions, provenance, optional mip levels and variants. Physical resolution may exceed the logical UV canvas. Use `ResourceTextureList.LogicalSize` or the value's logical dimensions for UVs and physical scale for sampling. Supplied mips remain available although current CPU scene renderers sample the base level.

[AssetTextureScope](../../src/Memoria.FF8.Assets/AssetTextureScope.cs) registers source images before atlas packing and releases residency with the asset scope. `TextureTransformContext` declares dependencies over resolved source textures. Format adapters return source-region replacements; the common pipeline constructs engine atlases.

[ResourceService](../../src/Memoria.FF8.Resources/ResourceService.cs) prepares resident values and dependent transforms before committing one generation. Stable `AssetHandle<TextureValue>` instances follow publication; released handles freeze their last value. Dispose subscriptions and release retained textures with their scopes. Initial lazy replacement failures retain source pixels and notify the host; failed reload preparation preserves committed values. The host publishes resources and the mod catalog before delivering callbacks.

The shared [PNG/DDS decoder](../../src/Memoria.FF8.Resources/TextureImageDecoder.cs) serves installed Remastered images and [texture packages](../modding/texture-mods.md). Recognition, search paths, hashes and filename mappings belong in adapters. The current host pumps CPU preparation on its owning thread; worker preparation would need an explicit source/residency threading contract.

## Shaders

The title's readable effect is [TitleScene.fx](../../src/Memoria.FF8.TitleScene/Shaders/TitleScene.fx). Its compiled `.mgfxo` is embedded, so ordinary builds do not invoke a shader compiler. Follow the [TitleScene README](../../src/Memoria.FF8.TitleScene/README.md#rebuild-the-shader) for the pinned compiler command, graphics state/lifetime rules, quality caps and deterministic time contract. Commit source and compiled effect together. There is no general package shader-loading API.

Use focused headless checks for the affected resource or rasterization contract; see the [documentation index](../README.md) for build/test instructions. Successful decoding alone does not verify alignment, source-region compatibility or full scene support.
