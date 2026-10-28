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
| Demaster battle models | `native-battle-model-sheet`: original character, weapon and monster DAT owners with source-published TIM row identities | Body and monster rows use 128 × 128 canvases; weapon rows use 128 × 32. Only original TIM layouts that publish this contract are eligible. |
| Demaster field models | `native-field-model-sheet`: original ONE/MCH owners with source-published material ordinals | 128 × 128 planes, including horizontal closed-eye planes and source-selected alternate materials. |
| Demaster content textures | `demaster-content-upload`: exact source-published native upload bytes | `hashOutput/<16 hexadecimal digits>_HD.dds` or `.png`; complete primary 4/8-bit indexed battle windows, including retained pixels and the live palette. Other upload families require their own proved source contract. |
| Demaster provider extensions | Explicit `demaster-world-wmset`, `demaster-world-wmset0`, `demaster-direct` | Available address contracts; these names do not promise that the selected provider publishes them. |
| Junction Field backgrounds | `classic-field-tile` with a native MAP-record atlas address | Whole `field/mapdata/<field>/<field>.dds` or `.png` atlas; uniform positive integer scale over 16px native slots, with first native source identity/palette ownership. |
| Junction menu artwork | Portrait atlases under `data/eng/menu/hires/` and split `iconfl00` through `iconfl03` pages | Portraits preserve the 256 × 256 canvas; icon pages preserve 128 × 128 and select the exact native palette. A complete `menu/icon` atlas uses 256 × 256 when no split pages match. |
| Junction FFNx | Source-published `classic-ffnx` identities under selected `mods/textures/` resources | Card/menu atlases, native field/world/battle model textures, native effect uploads and composite pages, terrain TEXL pages, roads, static world objects and implemented native world section consumers. |

Unknown targets produce diagnostics or retain original pixels. World overlays use native consumers: map/globe geometry and markers, clouds, object shadows and supported particle producers retain their original coordinates, palette selection and blend modes. Texture files alone do not establish support for an unimplemented producer. Full UI native-page reconstruction, general palette-animation revisions and general Tonberry resize preferences remain outside the implemented contract.

FFNx lookup preserves native model/texture indexes and world section/member identities. DDS and PNG replacements must preserve the native canvas aspect ratio and map crop boundaries to whole pixels. Crops used by terrain redirects and native atlas copies apply to the enlarged replacement image. When an exact palette suffix is absent, lookup uses the lowest available palette, following the external texture convention. The localized `eng/` root precedes the common root for English resources.

World foot model resource selection maps native entity types 0, 3, 4, 5, 7, 8 and 9 to shape 0; types 1, 2 and 6 use shapes 4, 5 and 6. Each shape retains its separate original texture members and polygon texture selectors. This resource and rendering support does not expand the world-entry story, control or contact lifecycles supported by gameplay.

Battle textures use `mods/textures/battle/<original filename>-<TIM ordinal>_<palette>`, with a two-digit palette suffix and DDS before PNG. Original stage, character, weapon and enemy resources publish their own TIM identities; decoded palette rows do not become new TIM ordinals. Stage palettes therefore retain the stage's single original TIM member. Native VRAM pixels and CLUT ownership determine which replacement a polygon can sample. Inspection distinguishes unsupported addresses from files whose original owner or TIM member is absent; absent targets remain unused.

Native effect textures publish `magic/` or `battle/` identities from the original file-open and upload events. A TIM ordinal counts actual TIM upload callbacks since that file was opened; it is independent of an embedded TIM's byte offset. Raw MAG members retain their original selector/member identity and zero-based native effect index, independently of the one-based engine catalog identifier. Shared native texture pages retain their virtual resource identities. Unidentified uploads keep original pixels rather than guessing an identity from a mod's filenames.

Effect page replacements use the same 256 × 256 integer-scale page contract, including concatenated registered owners and optional `_fN` copy-frame suffixes. Registration ownership and first-seen copy rectangles survive snapshots independently of live pixel provenance. A cached native texture window retains its sampled pixels and their source metadata while palette sampling uses current VRAM. Copies preserve the high-resolution source replacement even when the destination has no separate mod image; this extends FFNx, whose mod-pool copy path requires replacements at both ends. Palette suffixes follow the selected physical CLUT relative to the registered image palette anchor. Replacements preserve the native effect renderer's coverage and blending encoding; imported image data cannot change effect logic. Texture resolution goes through the common resource service, including package conflicts, diagnostics, generation changes and bounded resident handles.

Battle material images can use any positive resolution, with independent horizontal and vertical pixel densities over the original logical texture rectangle. Geometry, UV coordinates, texture pages and CLUT selection stay native. As a Memoria extension beyond strict FFNx image scaling, this also supports replacement images with different aspect ratios. When an image exactly describes the original TIM canvas completed to its native VRAM allocation (64-word horizontal pages, with 256-row stage pages or 128-row actor slots), the adapter crops that padding at whole-pixel boundaries first. Artwork and world image aspect rules remain unchanged.

### Junction Field background atlases

Native loose pages can also use `field/mapdata/<prefix>/<field>/<field>_<page>.png`, optionally under `direct/`. The prefix must match the field name. These are original 256 × 256 VRAM pages sampled with the tile's source coordinates, independently of the packed MAP-record atlas below. Native pages 0–12 and legacy page names 13–25 retain their distinct numbering; the legacy name takes precedence when both describe the same page. Unproved multi-suffix variants are rejected individually with owning diagnostics.

The palette-free path is `mods/textures/field/mapdata/<field>/<field>.dds` or `.png`, with the existing optional English root. Original MAP record count determines atlas width: `ceil(count / 16) * 16`, with height 256. Each record occupies a 16 × 16 slot. Native source identity includes texture ID, quantized source coordinates, depth and indexed palette; duplicate identities use the earliest matching slot. Destination placement, animation and blending remain controlled by native MAP data. These addresses are separate from MIM-page aliases used by other adapters.

External atlas alpha is coverage: values below 127 are transparent; values from 127 upward retain RGB and select the native primitive's blend behavior. The adapter converts this to the engine's canonical semitransparency marker. Authored opacity is not passed directly to the renderer.

The current direct tile consumer requires source UV coordinates aligned to 16 pixels, supported depth and in-page source bounds. Unsupported source layouts are diagnosed during inspection and reject the selected replacement when acquired; original tiles remain active. It does not guess residual UV reconstruction or change the original MAP decoder. Incompatible dimensions reject that resource without retrying PNG or a lower package. Valid resources and sibling mod components remain available.

### Junction native artwork

Native `menu/magNN.tex.png` and `menu/hires/iconflNN_PP.png` paths can address their corresponding artwork directly. Their source canvas and available palettes remain authoritative; extra palette filenames do not expand the original palette set.

Inspection checks palette availability against the selected source. Extra palette filenames cannot create new original palette consumers. Existing native font/title atlases without a current renderer consumer remain unused with an owning diagnostic; no title or glyph rendering is implied by accepting those resources.

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

### Demaster battle model sheets

Model images use `textures/battle.fs/hd_new/<original DAT basename>_<row>.dds` or `.png`, with DDS preferred. Character bodies use `dHcNNN`, weapons use `dHwNNN`, and monsters use `c0mNNN`; `H` is the original hexadecimal character identity and `NNN` is the original three-digit model identity. The source retains each actual DAT owner independently, including models whose original images happen to match. The row suffix identifies a native TIM slot, not an animation frame or decoded palette number.

Compatible sources publish `native-battle-model-sheet` through `NativeSheet` addresses on eligible `classic-battle` texture contexts. Body rows use a 128 × 128 canvas and native vertical slots 0 or 1; a weapon's single row uses a 128 × 32 canvas. Monster rows use 128 × 128 canvases and six native slots: X positions 640, 704 and 768, each with Y positions 0 and 128, ordered by X then Y. Original TIM dimensions and placement determine eligibility and the crop; unsupported model layouts retain their original textures.

Body sheets additionally require `native-battle-body-sheet`, published by the corresponding Remastered source profile. Original Classic bodies do not advertise it. Weapon and monster sheet contracts remain independent; compatibility cannot be inferred from matching embedded TIMs or from a model filename alone.

Model compatibility is a resource contract. Explicit package requirements or a verified adapter layout can identify the required model profile; arbitrary replacement pixels cannot reliably identify their intended geometry and UVs. Matching filenames, image dimensions or original TIM pixels are insufficient. Different model geometry alone also does not prove that a replacement is incompatible. Unverified combinations require validation rather than an inferred edition assignment.

For body sheets, a complete square image in the same owner's row-zero/row-one pair establishes the positive integer scale over a 128 × 128 plane. Either row may contain one plane or two horizontal planes at that scale; the second plane supplies the `eyes-closed` variant. A shorter upload image is completed with transparent pixels before the adapter crops the actual original TIM rectangle. The crop retains enlarged pixels and compatible DDS mip crops. Monster and weapon sheets keep equal positive integer scale on both axes over their published row canvas. A missing replacement retains original pixels; corrupt winning content, missing body scale anchors or incompatible dimensions report the owning package and file. This contract does not imply support for every DAT texture arrangement.

Replacements preserve original polygon UVs, texture pages, CLUT ownership, blend modes and native texture animations. Original open/closed copy plans establish distinct expected TIM words; matching complete pixel words and CLUT select the whole corresponding HD plane on the owning material. Ambiguous source allocations, indistinguishable native frames and unrelated pixel or palette writes retain native sampling. This bounded ownership check does not decode arbitrary renderer state. Native commands and the eye-status producer remain unchanged; the adapter does not invent a blink clock or infer timing from image differences. Native TIM identity remains separate from its decoded palette and from the renderer's atlas packing. Package enablement and refresh update resident texture handles through the normal resource lifecycle.

### Demaster field model sheets

Compatible sources publish `native-field-model-sheet` addresses on eligible `classic-field-model` textures. Filenames are `<native name>_<material ordinal>[_alpha].dds` or `.png` beneath `textures/field.fs/field_hd_new/`, then `field_hd/`. DDS precedes PNG within each directory. The source's `cNNN` names map to external `nNNN` names; other published names retain their identity. The suffix is the material ordinal, independent of the model's index in its ONE directory and its original VRAM position. Alternate materials are selected by source metadata.

This layout requires compatible Remastered model geometry and UVs. Equal TIM dimensions do not establish compatibility: original Classic character models use different atlas arrangements and do not advertise this layout. A texture package alone does not replace model geometry. A Classic playthrough can select corresponding Remastered **Model resources** in the [launcher](../players/getting-started.md#model-resources-from-another-installation). Layout capabilities then belong to each selected model; unrelated resource families and the primary runtime edition remain unchanged.

Source material access supports both actual ONE placements and original MCH library tables. Library materials retain their own encoded resource identity and can be loaded without constructing a field placement, geometry or animation. Availability in the library does not imply that a particular field uses that model.

A complete square contains one 128 × 128 plane; an image twice as wide as it is tall contains two. The second plane supplies the `eyes-closed` variant. Short uploads require an unambiguous complete material from the same source owner to establish plane width, and receive transparent completion before cropping the original TIM rectangle. Complete materials may use different integer scales. Package refresh updates stable resource handles.

Registered Remastered materials select the whole plane from retained native page state. Original explicit commands and automatic copy callbacks queue open/closed commands; the actual material bind consumes them. Source replacement clears current state while preserving explicitly retained pending commands. Refreshing a mod preserves this native state. Physical page identity and packed original UVs select the applicable half; a material spanning both halves currently fails explicitly. Draw submissions retain their selected resource values so later bindings cannot change an earlier actor's image. Older registered snapshots without the required native state need an explicit migration before use.

### Demaster content uploads

Content lookup uses the high 64 bits of seeded XXH128 over the exact native upload bytes. Channel order, palette expansion and the complete upload window belong to the source adapter; rendered RGBA images are not interchangeable with this input. The resolver retains the other 64 bits to reject conflicting uploads with the same filename identity.

Lookup tries the canonical `_HD.dds` path before `_HD.png`. Named alternatives and bare fingerprints remain inactive and produce diagnostics. Replacements require uniform positive integral scaling of the source upload canvas; exact crop boundaries are retained at each usable mip level. A rejected resource keeps its original pixels and reports a per-mod diagnostic.

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
