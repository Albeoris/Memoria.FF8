# Experimental Remastered resources

The provider can read a user-selected Steam Remastered installation without extracting complete archives or modifying installed files. English Remastered has an authenticated runtime profile for the opening route from a cold New Game through the infirmary and corridor into the classroom. The launcher accepts complete English Remastered installations. Unimplemented runtime contracts still stop explicitly; this bounded route does not establish complete Remastered playability.

## Storage and APIs

[GameDataSourceFactory.Open(directory)](../../src/Memoria.FF8.Assets/GameDataSourceFactory.cs) selects Remastered when the selected directory contains `main.zzz`. [RemasteredGameDataSource](../../src/Memoria.FF8.Assets/Remastered/RemasteredGameDataSource.cs) requires both `main.zzz` and `other.zzz`. It implements `IGameNativeMemorySource`, `IGameRuntimeSource`, `IResourceSource`, `IGameArchiveSource`, `IGameFileSource`, `IFieldModelSource`, `IBattleModelSource` and `ITextureReplacementResolver`.

Consumers use [logical resource/texture contracts](rendering.md), RGBA images and shared model, skeleton and animation types. ZZZ mounting, localized names, serialized guest-image tables and stock HD selection stay inside `Memoria.FF8.Assets.Remastered`. Mounted name collisions fail because precedence is not established. Loose Demaster extraction directories are not automatically mounted.

| Resource | Supported address examples |
| --- | --- |
| Root and nested archive entries | `main/kernel.bin`, `field/mapdata/maplist`, `field/main_chr/d000.mch` |
| Extracted effects | `magic/mag085_b.dat` |
| Data-relative files | `files/movies/disc00_00.cam` |
| Named tables | `tables/field-random`, `tables/world-random`, `tables/encounter-weights`, `tables/cosine`, `tables/vibration` |
| Localized text | `text/draw-points`, `text/card-names`, `text/card-names-alternate`, `text/card-descriptions`, `text/enemy-scan` |

Named guest tables require the authenticated English serialized image. The constructor accepts `en`, `fr`, `de`, `es`, `it` and `ja` resource languages, defaulting to `en`; the factory uses that default. Archive-language selection does not establish localized gameplay or text-decoder support. Localized field scripts resolve behind the same logical keys.

## Runtime and consumer boundary

All asset entry points select or borrow a resource provider, including field, battle, world, menu, card, audio and movie loading. Consumers use archive families, logical keys and provider-resolved files; they do not open installation executables or assume an archive directory layout. Providers own materialized file lifetimes.

`runtime/native-executable` identifies the authenticated native image required by the existing Classic parsers. Classic supplies its original PE bytes; Remastered throws `NotImplementedException`. Serialized Remastered guest bytes are never substituted for that PE contract. Supported guest tables use named `tables/*` keys or `IGameNativeMemorySource` address reads. The semantic memory API authenticates the edition container, preserves its actual source hash and distinguishes explicit serialized data from section-backed loader zero tails. It supplies tutorial, music, field registration, salary, particle and cold-process data without constructing a PE. `runtime/disc-wrapper` is also provider-owned and currently implemented only for Classic.

`IGameRuntimeSource.NativeRuntimeProfile` is nullable for resource inspection. `RequireNativeRuntimeProfile()` is the explicit gameplay requirement. English Remastered authenticates both its serialized image and translated runtime DLL. Field allocation, movement and camera receipts retain that edition identity. Sound containers are read from mounted archives through the shared PCM/ADPCM decoder. Native segment music requests resolve to the corresponding mounted OGG sibling. Missing members of a supported archive report ordinary missing-resource errors.

Authenticated native data also supplies encounter tables, draw-point messages and definitions, shop catalogs, prices and item names, and native Triple Triad indexed artwork and both backgrounds. Scan descriptions use the mounted edition text bank, including its corrected offsets and strings. Kernel names follow each edition's section offsets. World readers cover forest effects, contact shapes, message windows and timing, scripted rail shapes, and Garden/Ragnarok camera constants. Common battle readers cover archive filenames, party model selectors, primitive dispatch tables, action-camera frequency, graphics/status resources, command sequences and entry-transition constants. These readers retain the actual edition source hash. Existing Classic byte-image entry points remain available.

Reading these resources does not establish the translated execution contracts for every associated system. The effect catalog that inspects Classic initializer instructions, further effect-specific PE readers and disc mounting remain explicit unsupported boundaries in Remastered.

## Models and images

Enhanced MCH geometry and animations decode into the shared representation. Model-local vertex units are normalized through the authenticated DLL's native model-divisor table before skeleton transforms; bone lengths and animation translations retain their original units. Stock field HD images use the source page row, preferring the new family before the base family. Logical UV dimensions remain independent of physical image size. Field images occupy fixed 384-column atlas planes at three physical pixels per logical UV unit; short images retain that scale instead of stretching. Native storage outside uploaded subimages is unspecified; managed texture backing uses transparent pixels there. Battle-character images use the two-row family. Side-by-side eye images expose the normal image and an `eyes-closed` entry in `TextureValue.Variants`; automatic eye-state rendering is not implemented.

Stock image selection belongs to the source; external adapters can precede it through the shared resolver pipeline. PNG and DDS decoding is shared with texture packages, although stock archives contain PNGs. World and battle archives, including original embedded textures, are readable through the same source.

Battle monsters select image names through the authenticated native model table, including its shared monster and GF aliases. Source TIM coordinates identify each material row. Weapons resolve their native HD strips through the same model provider. Native battle primitive rendering samples these resolved materials at the executed texture allocation and CLUT, including retained PC texture windows. Replacement pixels apply only while the original indexed pixels and palette still match; subsequent native eye or effect writes retain ownership. Native normalized UVs, texture-window selection, blend state and draw commands remain authoritative. This establishes ordinary stock model materials, not every effect upload or closed-eye phase.

Menu consumers resolve stock character/GF portraits, graphical icons and the pointer, inventory cards, Triple Triad fronts/back/element sprites, magazine illustrations and tutorial screenshots before composing their existing sprite coordinates. Each output retains its logical extent and the image's physical resolution. Native localized card variants and tutorial screenshot sheets are selected inside the source. Existing system fonts, title presentation and UI backgrounds remain in use.

For menu icons, the source assembles `textures/hires/iconfl00..03.tex/<language>/<palette>.png` into the logical `menu/icon.tex` atlas. The four pages cover its 128×128 quadrants in row-major order, retaining the selected CLUT and supplied RGBA pixels. Stock 1024×1024 pages form a 2048×2048 replacement for the native 256×256 canvas, so existing sprite UVs, multipart offsets and crops remain valid, including parts crossing page boundaries. Assembled palettes are cached for the source lifetime without retaining a second copy of the individual pages. All four selected pages must have the same integer scale; incomplete or inconsistent sets fail explicitly. When no selected split pages exist, the source uses `textures/icon.tex/<language>/<palette>.png`, including the Japanese stock layout. Additional palettes17..23 and input-specific `4_dinput`, `4_mobile` and `4_xinput` pages have no current input-selection contract and remain unselected by the sixteen-palette icon consumers.

The SPU EyesOnMe cue resolves to its mounted OGG through the file provider. Concert instrument stems still require the unsupported concert playback contract.

This does not establish every GF/effect/scenery projection, every UI image projection, full audio playback, all native gameplay tables or complete Remastered playability. [Demaster address helpers](../modding/texture-mods.md) do not independently establish compatible layouts for every resource family.

## Optional checks

The [Remastered test project](../../tests/Memoria.FF8.Remastered.Tests/README.md) is included in the solution and `Build.ps1 -Test`. Synthetic archive, guest-image and unsupported-contract cases need no game data. From the product directory:

```powershell
dotnet run --project tests/Memoria.FF8.Remastered.Tests -c Release
```

Set `MEMORIA_FF8_REMASTERED_DIR` to a selected Remastered installation to enable additional read-only installed-resource checks. They skip when absent. No game process is launched, and no proprietary data or fixtures are distributed.

The optional opening acceptance additionally requires FFmpeg tools and runs in the App test project:

```powershell
dotnet run --project tests/Memoria.FF8.App.Tests -c Release -- --filter-class '*RemasteredOpeningTests'
```

Set `MEMORIA_FF8_MEDIA_TOOLS` to the FFmpeg tools directory. `MEMORIA_FF8_REMASTERED_PROOF_DIR` optionally retains a distinct standalone classroom save, render and report in a selected output directory. The check starts through the same cold-process owner as graphical launch, applies ordinary opening inputs, and validates a save/load plus the next input and render. Movies are skipped through their normal engine callbacks.
