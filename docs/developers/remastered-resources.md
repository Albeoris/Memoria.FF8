# Experimental Remastered resources

The provider can read a user-selected Steam Remastered installation without extracting complete archives or modifying installed files. This is resource support, not Remastered gameplay support: the playable engine profile remains English Steam 2013 Classic. Other engine profiles fail explicitly.

## Storage and APIs

[GameDataSourceFactory.Open(directory)](../../src/Memoria.FF8.Assets/GameDataSourceFactory.cs) selects Remastered when the selected directory contains `main.zzz`. [RemasteredGameDataSource](../../src/Memoria.FF8.Assets/Remastered/RemasteredGameDataSource.cs) requires both `main.zzz` and `other.zzz`. It implements `IResourceSource`, `IGameArchiveSource`, `IGameFileSource`, `IFieldModelSource`, `IBattleModelSource` and `ITextureReplacementResolver`.

Consumers use [logical resource/texture contracts](rendering.md), RGBA images and shared model, skeleton and animation types. ZZZ mounting, localized names, serialized guest-image tables and stock HD selection stay inside `Memoria.FF8.Assets.Remastered`. Mounted name collisions fail because precedence is not established. Loose Demaster extraction directories are not automatically mounted.

| Resource | Supported address examples |
| --- | --- |
| Root and nested archive entries | `main/kernel.bin`, `field/mapdata/maplist`, `field/main_chr/d000.mch` |
| Extracted effects | `magic/mag085_b.dat` |
| Data-relative files | `files/movies/disc00_00.cam` |
| Named tables | `tables/field-random`, `tables/world-random`, `tables/encounter-weights`, `tables/cosine`, `tables/vibration` |
| Localized text | `text/draw-points`, `text/card-names`, `text/card-names-alternate`, `text/card-descriptions`, `text/enemy-scan` |

Named guest tables require the authenticated English serialized image. The constructor accepts `en`, `fr`, `de`, `es`, `it` and `ja` resource languages, defaulting to `en`; the factory uses that default. Archive-language selection does not establish localized gameplay or text-decoder support. Localized field scripts resolve behind the same logical keys.

## Models and images

Enhanced MCH geometry and animations decode into the shared representation. Stock field HD images use the source page row, preferring the new family before the base family. Logical UV dimensions remain independent of physical image size. Battle-character images use the two-row family. Side-by-side eye images expose the normal image and an `eyes-closed` entry in `TextureValue.Variants`; automatic eye-state rendering is not implemented.

Stock image selection belongs to the source; external adapters can precede it through the shared resolver pipeline. PNG and DDS decoding is shared with texture packages, although stock archives contain PNGs. World and battle archives, including original embedded textures, are readable through the same source.

This does not establish stock HD monster/GF/scenery projection, every UI image projection, full audio playback, all native gameplay tables or complete Remastered playability. [Demaster address helpers](../modding/texture-mods.md) do not independently establish compatible layouts for every resource family.

## Optional checks

The [Remastered test project](../../tests/Memoria.FF8.Remastered.Tests/README.md) is excluded from the solution, `Build.ps1 -Test` and normal solution runs. Synthetic archive and guest-image cases need no game data. From the product directory:

```powershell
dotnet run --project tests/Memoria.FF8.Remastered.Tests -c Release
```

Set `MEMORIA_FF8_REMASTERED_DIR` to a selected Remastered installation to enable additional read-only installed-resource checks. They skip when absent. No game process is launched, and no proprietary data or fixtures are distributed.
