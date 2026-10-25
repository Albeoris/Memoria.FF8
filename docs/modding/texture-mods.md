# Texture mods

Memoria reads unpacked Tonberry and Demaster texture packages and textures selected by Junction manifests without loading the original mod loaders. Support is limited to source layouts listed below; recognizing a package does not establish that every texture in it can be displayed. The playable profile remains English Steam 2013 Classic. [Remastered resource access](../developers/remastered-resources.md) is experimental.

## Install and configure

Place each package in an immediate child directory of `Mods/Tonberry` or `Mods/Demaster`, either under the selected game directory or beside `Memoria.Launcher.exe`. The launcher passes its adjacent `Mods` directory through `--mods-dir <directory>`. Direct starts from a package's `Data/Memoria.FF8.exe` use the same root when the adjacent launcher and package `Mods` directory are present. Other direct starts use `Mods` beside the executable. `--mods-dir <directory>` selects an explicit mod root. The selected game directory's `Mods` remains active in every case. Preserve its internal table and texture paths. Both roots participate; the working directory is irrelevant. Texture packages must be unpacked. No XML or JSON texture-package manifest is required. Junction archives use a separate [file replacement format](file-replacements.md); native scene configuration uses [Memoria field mods](field-mods.md).

The Mods menu exposes enablement, automatic enablement of new packages, diagnostics and Refresh. Global policy lives in `mods.ini` under the selected mod root (the launcher-adjacent `Mods` directory for launcher starts):

```ini
auto_enable_new_mods = 1
```

Each texture package may contain `mod.ini`:

```ini
enabled = 1
priority = 0
```

Only `0` and `1` are valid boolean values. Missing global policy means automatic enablement is on. Missing package enablement inherits that policy; explicit `0` or `1` overrides it. Discovery does not create missing configuration files. Menu changes write to the selected package's own directory. Turning automatic enablement off first preserves the current state of discovered inherited packages. Explicit disables survive turning it on again. Writes preserve unrelated keys/comments and use temporary-file replacement.

Tonberry and Demaster packages may set `priority` to a signed 32-bit integer (`-2147483648` through `2147483647`); the default is `0`. Higher values win before lower values. An invalid priority rejects the reload and reports its package and `mod.ini`; the previous committed generation remains active.

Package IDs are `Format/PackageName`, compared case-insensitively. For duplicate IDs, the game-directory copy owns the entire package. Within Tonberry or Demaster, descending priority precedes ordinal case-insensitive alphabetical package order, with ordinal spelling as tie-breaker. Files retain their alphabetical ordering. The first contribution to a virtual filename wins. Removing or disabling it exposes the next contribution. Priority applies to contributions to the same virtual filename; the adapter's candidate-path preference is evaluated first. For example, an exact-palette DDS in a lower-priority package precedes a plain-page PNG in a higher-priority package. Junction retains its manifest dependency/order relationships and does not use this texture-package priority. Across texture formats, resolution tries Demaster, Junction FFNx textures, Tonberry, then the source's own replacement/original content.

## Supported layouts

| Format | Selection contract | Limits |
| --- | --- | --- |
| Tonberry | Classic `classic-world-texl`, `classic-world-road-region`, and explicitly supplied `tonberry-classic-page` | Requires original color/page semantics. Generic field tiles, model atlases and battle images are not automatically equivalent. Remastered is incompatible. |
| Demaster | Classic world TEXL pages and original road regions represented by world page 29 | Compatibility of a package does not make every target compatible. Other families require a source-published compatible layout. |
| Demaster card artwork | `demaster-card-artwork`: menu card atlases, Triple Triad fronts, back, elements and split backgrounds | Square 256 × 256 source sheets at an integer scale; localized alternatives follow the source language. |
| Demaster field backgrounds | `native-field-background-page`: original MAP tiles addressed within their MIM pages | Square 256 × 256 source pages at a positive integer scale; native page, palette and pixel UV metadata select each tile. |
| Demaster battle stages | `native-battle-stage-page`: original stage pages 16–21 under `textures/battle.fs/hd_new/` | Independent 256 × 256 page canvases; static and numbered animation frames. Model and effect layouts are separate contracts. |
| Demaster provider extensions | Explicit `demaster-world-wmset`, `demaster-world-wmset0`, `demaster-direct` | Available address contracts; these names do not promise that the selected provider publishes them. |
| Junction menu artwork | Portrait atlases under `data/eng/menu/hires/` and split `iconfl00` through `iconfl03` pages | Portraits preserve the 256 × 256 canvas; icon pages preserve 128 × 128 and select the exact native palette. |
| Junction FFNx | Source-published `classic-ffnx` identities under selected `mods/textures/` resources | Card/menu atlases, native field/world model textures, terrain TEXL pages, roads, static world objects and implemented native world section consumers. |

Unknown targets produce diagnostics or retain original pixels. World overlays use native consumers: map/globe geometry and markers, clouds, object shadows and supported particle producers retain their original coordinates, palette selection and blend modes. Texture files alone do not establish support for an unimplemented producer. Full UI native-page reconstruction, general palette-animation revisions and general Tonberry resize preferences remain outside the implemented contract.

FFNx lookup preserves native model/texture indexes and world section/member identities. DDS and PNG replacements must preserve the native canvas aspect ratio and map crop boundaries to whole pixels. Crops used by terrain redirects and native atlas copies apply to the enlarged replacement image. When an exact palette suffix is absent, lookup uses the lowest available palette, following the external texture convention. The localized `eng/` root precedes the common root for English resources.

World foot model resource selection maps native entity types 0, 3, 4, 5, 7, 8 and 9 to shape 0; types 1, 2 and 6 use shapes 4, 5 and 6. Each shape retains its separate original texture members and polygon texture selectors. This resource and rendering support does not expand the world-entry story, control or contact lifecycles supported by gameplay.

Battle textures use `mods/textures/battle/<original filename>-<TIM ordinal>_<palette>`, with a two-digit palette suffix and DDS before PNG. Original stage, character, weapon and enemy resources publish their own TIM identities; decoded palette rows do not become new TIM ordinals. Stage palettes therefore retain the stage's single original TIM member. Native VRAM pixels and CLUT ownership determine which replacement a polygon can sample. Inspection distinguishes unsupported addresses from files whose original owner or TIM member is absent; absent targets remain unused.

Battle material images can use any positive resolution, with independent horizontal and vertical pixel densities over the original logical texture rectangle. Geometry, UV coordinates, texture pages and CLUT selection stay native. As a Memoria extension beyond strict FFNx image scaling, this also supports replacement images with different aspect ratios. When an image exactly describes the original TIM canvas completed to its native VRAM allocation (64-word horizontal pages, with 256-row stage pages or 128-row actor slots), the adapter crops that padding at whole-pixel boundaries first. Artwork and world image aspect rules remain unchanged.

### Junction native artwork

Portraits use `mods/textures/data/eng/menu/hires/face_b00_00` and `face_b01_00`; guardian portraits use `gf_big00_00` and `gf_big01_00`. DDS and PNG files preserve the native canvas at an integer scale. Sprite cropping preserves that enlarged resolution.

Menu icons use `mods/textures/data/eng/menu/hires/iconfl<page>_<palette>`, with two-digit page and palette numbers. Pages 00–03 are row-major quadrants of the original 256 × 256 atlas. Sparse replacements retain original pixels in missing quadrants; a missing palette retains that original palette. Controller prompts select the glyph for the currently resolved physical binding. Printed button combinations use their original physical glyph. Resource generation changes refresh cached menu portraits, icons and controller glyphs.

Single-source indexed world section textures may use FFNx VRAM page names `<native-name>_<page>_<palette-x>_<palette-y>`. These addresses use original VRAM word coordinates and CLUT coordinates, independently of the engine atlas layout. Each replacement page is a square 256 × 256 logical canvas at an integer scale. The resolver crops and composes the native texture rectangle, tries the exact palette coordinates before the zero-coordinate fallback, and retains logical UV dimensions. The same page-address contract applies to native `cardgame/icons` regions, including elemental icons and win/loss/draw banners. A matching VRAM page takes precedence over the whole-image `_00` replacement. World and card page identities remain single-source.

Registered field model pages also support concatenated source identities: `field/model/<kind>_chr/<name>-<member>_[f<frame>_]...<page>_<palette-x>_<palette-y>` under `mods/textures/`. Source membership and order come from the actual registered VRAM rectangles. The adapter selects the exact native page and CLUT, crops the square 256 × 256 page at an integer scale and retains the original source UV dimensions. An animated `_fN` suffix records the first-seen order of actual texture-copy rectangles; this history survives snapshots and battle capture. A restored active registration lacking that history rejects matching page replacements until the original field resources are actually reloaded. Frames are never inferred from an eye-open or eye-closed flag.

### Demaster field background pages

Field backgrounds use `textures/field_bg/<first two map characters>/<map>/<map>_<page>[_<palette>].dds` or `.png`. The directory prefix and both map names must agree. Page numbers are the original MAP `TextureId`, 0 through 15, corresponding to native pages 16 through 31. The optional palette suffix is the original MAP palette, 0 through 15. Write page and palette numbers without leading zeroes.

The Classic source publishes `native-field-background-page` metadata for each `classic-field-tile`: the original MIM owner, native page and palette, and the MAP tile's `SourceX`/`SourceY` pixel coordinates in a 256 × 256 canvas. These coordinates retain their pixel meaning at either indexed bit depth. The adapter crops the original 16 × 16 logical tile from the replacement page, preserving its enlarged pixels. Pages must be square images at a positive integral scale of 256 × 256; a 1024 × 1024 page produces 64 × 64 tile images. DDS mip crops are retained while every crop boundary falls on a whole pixel.

Lookup tries the exact palette's DDS, exact palette's PNG, plain-page DDS, then plain-page PNG. There is no fallback to palette zero for another palette. A missing target retains the original tile. Corrupt winning content or incompatible image dimensions fail preparation with package/file diagnostics.

The suffix selects a palette; it does not create animation frames. Original MAP `Parameter`/`State` selection still chooses animated tiles and their independent source UVs. Destination coordinates, layers, draw order, visibility and native blend modes remain unchanged, including transparent and semitransparent texels. Enabling, disabling or refreshing a package republishes resident tile handles and refreshes cached CPU rendering and GPU atlases, including replacements whose dimensions stay the same.

### Demaster battle stage pages

Stage images use `textures/battle.fs/hd_new/a0stgNNN_PAGE.dds` or `.png`, where `NNN` is the original stage resource number and `PAGE` is its native page number, 16 through 21. These pages overlap in the original VRAM atlas at eight-bit depth; each replacement retains its own 256 × 256 logical canvas. Replacement images may use any positive physical dimensions, with independent horizontal and vertical densities over that logical page. Original polygon UVs, geometry, blend modes and colors remain unchanged. DDS takes precedence over PNG.

Numbered images append `_0`, `_1`, and so on. Frame zero takes precedence over the static image. The sequence ends at the first missing number and loops to zero; later numbers are unreachable. Without frame zero, the static image is used. Every reachable frame must have the same dimensions. Animation uses the default 100 ms period on the engine's deterministic battle presentation clock, rather than the external loader's wall clock and upload visits. Pausing holds the frame; restoring a snapshot preserves its phase. Loading a different stage resource resets the animation clock.

Only polygons explicitly submitted from the corresponding original stage resource receive these page replacements. The current resource identity follows stage reloads. An older snapshot without the current resource identity or required animation epoch must be explicitly migrated before these replacements can render; the engine reports the missing contract instead of guessing from the encounter's initial stage. Existing Junction atlas replacements retain their original VRAM pixel and CLUT guards. Disabling or reloading a package refreshes the same resident page handles.

### Tonberry

Tables are UTF-8 CSV with `name,unsigned-number` rows. Blank lines and lines beginning with `*` are ignored. A leading `!` on the name requests decoded-image persistence for the active resolver generation. Tables beneath a `disabled` directory are ignored.

| Table | Recognized filenames or directory |
| --- | --- |
| Primary | `hash1map.csv`, `*_hm.csv`, CSVs directly under `hashmap/` |
| Collision gate | `collisions.csv` |
| Secondary | `hash2map.csv` |
| Object | `objmap.csv`, CSVs directly under `objmap/` |

First keys win independently in each table. Primary lookup precedes collision-gated secondary lookup, then object lookup. A collision gate without a secondary match does not fall through to object lookup. A winning rule with a missing image does not select a later duplicate key.

For a rule such as `sample_page_1`, the preferred PNG address is `textures/sa/sample_page/sample_page_1.png`. Exact basename lookup elsewhere in active packages is a fallback and reports ambiguity. Recognition uses the original source representation, never replacement pixels. Tonberry replacements are PNGs.

### Demaster

Classic TEXL page `n` resolves `textures/world/dat/texl/texl_<n+1, three digits>_<palette>`. Page zero, palette zero therefore selects `textures/world/dat/texl/texl_001_0.dds`, then `.png`. A nonzero palette falls back to palette zero, again DDS before PNG. Candidate filename priority precedes package ownership. Corrupt winning content fails preparation rather than exposing a lower-priority package.

Classic road regions resolve `textures/world/dat/wmset/wmset_029_0.dds` or `.png`. This image represents the complete 256 × 256 source page. The adapter crops each original road region before the engine builds its road atlas; do not supply the optimized engine atlas under this name.

Logical UV dimensions remain separate from physical resolution. TEXL and road page replacements must have equal positive integral scale on both axes. Incompatible dimensions fail preparation. Filename helpers for other families do not establish UV compatibility.

Card artwork uses `textures/mc00.tex` through `mc09.tex` for the inventory and field card markers, and `textures/cards/cards_0` through `cards_13` for the mini-game. Names take `.dds` or `.png`, with DDS preferred. Each menu sheet contains eleven fronts in a four-column grid. Each mini-game sheet contains eight fronts in two columns; `cards_13` also supplies the hidden-card back at (0, 192). Element icons come from `textures/cards/text_0`. Japanese resources select `mc05.tex` and `cards_7`; other source languages select `mc05.tex_en` and `cards_7_en`. These are alternatives, so a single language does not consume both versions.

The rules background combines `textures/cards/back_0` and `back_1`; the playing board combines `board_0` and `board_1`. The first sheet supplies the left 256 × 224 region and the second its leftmost 128 × 224 region, producing a 384 × 224 canvas. Both replacement halves must have the same scale. A missing half retains original pixels. Card interfaces and field markers refresh when the active resource generation changes; composed fronts preserve replacement resolution above the default 256 × 256 output.

The shared decoder accepts PNG and two-dimensional DDS: 32-bit RGBA/BGRA, BC1, BC2, BC3 and BC7, including supported DX10 headers. Arrays, cube maps, volumes, unsupported masks/formats, invalid dimensions/mip counts and truncated data fail. DDS mip levels are retained; current CPU renderers sample the base level.

## Reload lifecycle

Watchers enqueue changes; they do not decode images or touch rendering state. Reconciliation waits for 250 ms of inactivity, with a two-second maximum under continuous changes. The application currently pumps discovery and CPU preparation on its owning thread; this is not background loading.

Resident replacements and dependent atlas transforms are prepared before publication. A failed reload retains the previous committed generation and reports package/file ownership. A first-use lazy replacement failure retains original pixels. Retries occur on later pump calls after 100, 250, 500 and 1,000 ms; exhaustion waits for a new event or Refresh. Explicit Refresh revalidates content even when timestamps and sizes are unchanged.

Paths are relative and confined to the package. Escaping links, linked package roots and cycles are rejected. Content is version-checked when read, so edits cannot silently replace bytes of an indexed revision. Resources and the mod catalog become visible before callbacks run. Dispose subscriptions when consumers leave their scope.

## APIs and checks

`IModService` exposes catalogs, policy, enablement, diagnostics, Refresh and subscriptions. `ModRuntime` owns discovery/publication; adapters own recognition and filename mapping. `GameResourceHost` composes them with `ResourceService`. Consumers use [resource/texture contracts](../developers/rendering.md) instead of loader names.

From the product directory:

```powershell
dotnet run --project tests/Memoria.FF8.Mods.Tests
```

Installed-package cases require explicitly configured optional inputs and skip when absent. See [ModRuntime](../../src/Memoria.FF8.Mods/ModRuntime.cs), [Tonberry tables](../../src/Memoria.FF8.Mods/TonberryTables.cs), [Demaster resolver](../../src/Memoria.FF8.Mods/DemasterTextureResolver.cs), [shared decoder](../../src/Memoria.FF8.Resources/TextureImageDecoder.cs) and [composition root](../../src/Memoria.FF8.App/GameResourceHost.cs).
