# Experimental Remastered resources

The resource provider can read a user-selected Steam Remastered installation without extracting its complete archives or modifying game files. This is initial resource support; the playable engine profile remains the English Classic release.

`GameDataSourceFactory.Open(directory)` selects storage at the composition boundary. Game assets use `ResourceKey`, `TextureKey`, common RGBA textures, and the existing shared model, skeleton and animation types. All ZZZ, localized-name, serialized guest-image and stock HD replacement rules live in `Memoria.FF8.Assets.Remastered`.

Supported resource paths include root and nested archive entries (`main/kernel.bin`, `field/mapdata/maplist`, `field/main_chr/d000.mch`), extracted effects (`magic/mag085_b.dat`), Data-relative files (`files/movies/disc00_00.cam`), named tables (`tables/field-random`, `tables/world-random`, `tables/encounter-weights`, `tables/cosine`, `tables/vibration`), and localized text (`text/draw-points`, `text/card-names`, `text/card-names-alternate`, `text/card-descriptions`, `text/enemy-scan`). Named guest tables require the authenticated English serialized image. The language argument supports English, French, German, Spanish, Italian and Japanese archive names; this does not establish localized gameplay or text-decoder support.

Fields resolve localized scripts behind the same logical key. Enhanced MCH geometry and animations decode through the shared model representation. Stock field HD images use their source page row, new-family then base-family selection, and retain logical UV dimensions independently of physical image size. Character battle models use the verified two-row image family. Side-by-side eye images expose a normal image and an `eyes-closed` variant in `TextureValue.Variants`; automatic eye-state rendering is not implemented here. PNG and DDS decoding is shared with mod packages; stock archives contain PNGs.

World and battle archives can be read through the same source, including their original embedded textures. This does not establish stock HD monster/GF/scenery projection, all UI image projections, full audio playback, full native gameplay tables or complete Remastered playability. Unsupported native engine profiles fail explicitly. Conflicting names across mounted archives are rejected because stock collision precedence has not been established. Loose Demaster extraction directories are not automatically mounted.

## Optional checks

The separate `tests/Memoria.FF8.Remastered.Tests` project is excluded from the solution, `Build.ps1 -Test`, and normal test runs. Its synthetic cases require no game data. Installed-resource checks are skipped unless `MEMORIA_FF8_REMASTERED_DIR` explicitly selects an installation:

```powershell
dotnet run --project tests/Memoria.FF8.Remastered.Tests -c Release
```

No proprietary game data or test fixtures are included.
