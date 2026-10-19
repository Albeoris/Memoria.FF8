# Texture mods

Memoria reads unpacked Tonberry and Demaster texture packages without loading either original mod loader. Support is limited to source layouts listed below; recognizing a package does not establish that every texture in it can be displayed. The playable profile remains English Steam 2013 Classic. [Remastered resource access](../developers/remastered-resources.md) is experimental.

## Install and configure

Place each package in an immediate child directory of `Mods/Tonberry` or `Mods/Demaster`, either under the selected game directory or beside `Memoria.Launcher.exe`. The launcher passes its adjacent `Mods` directory through `--mods-dir <directory>`. Direct starts from a package's `Data/Memoria.FF8.exe` use the same root when the adjacent launcher and package `Mods` directory are present. Other direct starts use `Mods` beside the executable. `--mods-dir <directory>` selects an explicit mod root. The selected game directory's `Mods` remains active in every case. Preserve its internal table and texture paths. Both roots participate; the working directory is irrelevant. Texture packages must be unpacked. No XML or JSON texture-package manifest is required. Junction archives use a separate [file replacement format](file-replacements.md); native scene configuration uses [Memoria field mods](field-mods.md).

The Mods menu exposes enablement, automatic enablement of new packages, diagnostics and Refresh. Global policy lives in `mods.ini` under the selected mod root (the launcher-adjacent `Mods` directory for launcher starts):

```ini
auto_enable_new_mods = 1
```

Each texture package may contain `mod.ini`:

```ini
enabled = 1
```

Only `0` and `1` are valid boolean values. Missing global policy means automatic enablement is on. Missing package enablement inherits that policy; explicit `0` or `1` overrides it. Discovery does not create missing configuration files. Menu changes write to the selected package's own directory. Turning automatic enablement off first preserves the current state of discovered inherited packages. Explicit disables survive turning it on again. Writes preserve unrelated keys/comments and use temporary-file replacement.

Package IDs are `Format/PackageName`, compared case-insensitively. For duplicate IDs, the game-directory copy owns the entire package. Distinct packages and files use ordinal case-insensitive alphabetical order, with ordinal spelling as tie-breaker. The first contribution to a virtual filename wins. Removing or disabling it exposes the next contribution. Across texture formats, resolution tries Demaster, Tonberry, then the source's own replacement/original content.

## Supported layouts

| Format | Selection contract | Limits |
| --- | --- | --- |
| Tonberry | Classic `classic-world-texl`, `classic-world-road-region`, and explicitly supplied `tonberry-classic-page` | Requires original color/page semantics. Generic field tiles, model atlases and battle images are not automatically equivalent. Remastered is incompatible. |
| Demaster | Classic world TEXL pages and original road regions represented by world page 29 | Compatibility of a package does not make every target compatible. Other families require a source-published compatible layout. |
| Demaster provider extensions | Explicit `demaster-world-wmset`, `demaster-world-wmset0`, `demaster-direct` | Available address contracts; these names do not promise that the selected provider publishes them. |

Unknown targets produce diagnostics or retain original pixels. Installing a cloud image does not add cloud geometry to the Classic renderer. Water mapping, full field/UI/model native-page reconstruction, dynamic palette-animation revisions and general Tonberry resize preferences remain outside the implemented contract.

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
