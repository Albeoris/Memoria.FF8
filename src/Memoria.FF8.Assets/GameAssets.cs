using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Mim;
using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;
using System.Collections.Immutable;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Reads selected original assets without modifying or extracting the installed game.</summary>
public sealed partial class GameAssets : IDisposable
{
    private readonly IFlArchive outer;
    private readonly IGameArchiveSource archiveSource;
    private readonly IResourceSource? ownedSource;
    private readonly string[] fieldNames;
    private readonly Dictionary<int, FieldAssets> fields = [];
    private readonly Dictionary<int, FieldSoundEffectTable> fieldSoundEffects = [];
    private readonly IAssetOverrideSource? overrides;
    private readonly IModelOverrideSource? modelOverrides;
    private readonly ResourceService resources;
    private readonly IMusicCueResolver musicCueResolver;
    private readonly AssetTextureScope textureScope;
    private readonly Dictionary<IFlArchive, string> archiveIdentities = [];
    private IFlArchive? mainCharacters;
    private NativeVibrationBank? nativeVibrationBank;
    private NativeShopDefinitions? nativeShopDefinitions;
    private NativeWeaponShopDefinitions? nativeWeaponShopDefinitions;
    private NativeDrawPointCatalog? nativeDrawPointCatalog;
    private long nativeDrawPointGeneration = -1;
    private NativeEncounterTables? nativeEncounterTables;

    public GameAssets(string gameDirectory, IAssetOverrideSource? overrides = null, ResourceService? resources = null, IMusicCueResolver? musicCueResolver = null, IModelOverrideSource? modelOverrides = null)
    {
        this.musicCueResolver = musicCueResolver ?? new ClassicMusicCueResolver();
        this.overrides = overrides;
        this.modelOverrides = modelOverrides;
        GameDirectory = Path.GetFullPath(gameDirectory);
        if (resources is null)
        {
            ownedSource = GameDataSourceFactory.Open(GameDirectory);
            resources = new ResourceService(ownedSource);
        }
        this.resources = resources;
        textureScope = new AssetTextureScope(resources);
        archiveSource = resources.GetSource<IGameArchiveSource>() ?? throw new NotSupportedException("The game resource source does not support archive enumeration.");
        IFlArchive? openedArchive = null;
        try
        {
            openedArchive = OpenArchive("field");
            outer = openedArchive;
            archiveIdentities[outer] = "field";
            using IFlArchive mapData = OpenNested("mapdata");
            fieldNames = System.Text.Encoding.ASCII.GetString(Read(mapData, "maplist")).Replace("\r", "").Split('\n');
        }
        catch
        {
            try
            {
                openedArchive?.Dispose();
            }
            finally
            {
                ownedSource?.Dispose();
            }
            throw;
        }
    }

    public string GameDirectory { get; }
    public ResourceService Resources => resources;
    public MusicCueResource ResolveMusicCue(MusicCue cue)
    {
        MusicCueResource resource = musicCueResolver.Resolve(cue);
        resource.Validate();
        if (resource.Cue != cue)
        {
            throw new InvalidDataException("The music resource resolver returned a different logical cue.");
        }
        return resource;
    }
    public string? NativeExecutableHash => resources.GetSource<IGameRuntimeSource>()?.NativeExecutableHash;
    public NativeEncounterTables ReadNativeEncounterTables() => nativeEncounterTables ??= NativeEncounterTables.Read(ReadNativeMemory());
    public NativeDrawPointCatalog ReadNativeDrawPointCatalog()
    {
        long generation = resources.Generation;
        if (nativeDrawPointCatalog is not null && nativeDrawPointGeneration == generation)
        {
            return nativeDrawPointCatalog;
        }
        using IFlArchive main = OpenArchive("main");
        NativeDrawPointCatalog selected = NativeDrawPointCatalogReader.Read(ReadNativeMemory(), Read(main, "kernel.bin"), ReadResource(new ResourceKey("text/draw-points")), resources.FindText(StaticTextResource.DrawPointMessages));
        nativeDrawPointCatalog = selected;
        nativeDrawPointGeneration = generation;
        return selected;
    }
    public NativeVibrationBank ReadNativeVibrationBank() => nativeVibrationBank ??= NativeVibrationBankReader.ReadTable(ReadResource(new ResourceKey("tables/vibration")), NativeExecutableHash);
    public NativeShakeTables ReadNativeShakeTables()
    {
        return new NativeShakeTables(ReadResource(new ResourceKey("tables/field-random")), ReadResource(new ResourceKey("tables/cosine")));
    }
    public NativeCharacterVitals ReadNewGameCharacterVitals()
    {
        if (NativeRuntimeProfile is null)
        {
            throw UnsupportedRuntime("Native character initialization requires the proved installed executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeCharacterInitialization.Read(Read(main, "init.out"), Read(main, "kernel.bin"));
    }

    public NativeBattleExperienceDefinition ReadNativeBattleExperienceDefinition()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("Battle experience requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeBattleExperienceDefinitionReader.Read(Read(main, "kernel.bin"));
    }
    public NativeEconomy ReadNewGameEconomy()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("Native economy initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeEconomyInitialization.Read(Read(main, "init.out"));
    }

    public NativeShopHistory ReadNewGameShopHistory()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("Native shop initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeShopDefinitionReader.ReadNewGameHistory(Read(main, "init.out"));
    }

    public NativeShopDefinitions ReadNativeShopDefinitions()
    {
        if (nativeShopDefinitions is not null)
        {
            return nativeShopDefinitions;
        }
        using IFlArchive main = OpenArchive("main");
        using IFlArchive menu = OpenArchive("menu");
        return nativeShopDefinitions = NativeShopDefinitionReader.Read(ReadNativeMemory(), Read(menu, "shop.bin"), Read(menu, "price.bin"), Read(main, "kernel.bin"));
    }

    public NativeWeaponRecipes ReadNewGameWeaponRecipes()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("Native known-weapon initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeWeaponShopDefinitionReader.ReadNewGameRecipes(Read(main, "init.out"));
    }

    public NativeWeaponShopDefinitions ReadNativeWeaponShopDefinitions()
    {
        if (nativeWeaponShopDefinitions is not null)
        {
            return nativeWeaponShopDefinitions;
        }
        using IFlArchive main = OpenArchive("main");
        using IFlArchive menu = OpenArchive("menu");
        return nativeWeaponShopDefinitions = NativeWeaponShopDefinitionReader.Read(ReadNativeMemory(), Read(main, "kernel.bin"), Read(menu, "mwepon.bin"), Read(menu, "mwepon.msg"));
    }

    public NativeShotAvailabilityState ReadNewGameShotAvailability()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("Native shot availability initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeShotAvailabilityInitialization.Read(Read(main, "init.out"), Read(main, "kernel.bin"));
    }

    public NativeLimitProgress ReadNewGameLimitProgress()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("Native Limit initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeLimitProgressInitialization.Read(Read(main, "init.out"));
    }

    public NativePocketStationState ReadNewGamePocketStation()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("PocketStation initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativePocketStationInitialization.Read(Read(main, "init.out"));
    }

    public NativeSavedBattleFlags ReadNewGameSavedBattleFlags()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("Saved battle flag initialization requires the authenticated original executable.");
        }
        //56D9F0/47E410 clear1CFDC58..1CFEFF8.56DA10's init.out payload ends1CFE7A4, before BYTE1CFE97A.
        return new NativeSavedBattleFlags(NativeInputConfiguration.OriginalInitHash, 0);
    }
    public NativeInputConfiguration ReadNewGameInputConfiguration()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("Native input initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeInputInitialization.Read(Read(main, "init.out"));
    }
    public NativeSalaryTable ReadNativeSalaryTable()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("Salary lookup requires the authenticated original executable.");
        }
        byte[] raw = ReadNativeMemory().Read(0xB8E474, 64);
        ushort[] entries = new ushort[32];
        for (int index = 0; index < entries.Length; index++)
        {
            entries[index] = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(index * 2));
        }
        NativeSalaryTable table = new(NativeExecutableHash!, System.Collections.Immutable.ImmutableArray.CreateRange(entries));
        table.Validate();
        return table;
    }
    public NativeFieldRuntimeProfile? NativeRuntimeProfile => resources.GetSource<IGameRuntimeSource>()?.NativeRuntimeProfile;

    public NativeCharacterAppearance ReadNewGameCharacterAppearance()
    {
        if (NativeRuntimeProfile is null)
        {
            throw UnsupportedRuntime("Native appearance initialization requires the proved installed executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeAppearanceInitialization.Read(Read(main, "init.out"));
    }

    public FieldAssets LoadField(int fieldId)
    {
        if (fields.TryGetValue(fieldId, out FieldAssets? cached))
        {
            return ApplyModelOverrides(cached);
        }
        if ((uint)fieldId >= fieldNames.Length || string.IsNullOrWhiteSpace(fieldNames[fieldId]))
        {
            throw new ArgumentOutOfRangeException(nameof(fieldId));
        }
        string name = fieldNames[fieldId];
        using IFlArchive nested = OpenNested(name);
        byte[] mapBytes = Read(nested, name + ".map");
        byte[] mimBytes = Read(nested, name + ".mim");
        MapFile map = MapFile.Read(mapBytes);
        MimFile mim = MimFile.Read(mimBytes);
        IReadOnlyList<TextureSourceAddress> backgroundAtlas = FieldBackgroundTileDecoder.AtlasAddresses(new ResourceKey($"field/{name}/{name}.mim"), map.Tiles);
        BackgroundTile[] tiles = map.Tiles.Select(tile => new BackgroundTile(tile.X, tile.Y, unchecked((ushort)tile.Z), tile.Parameter, tile.State, tile.PackedLayer >> 1, tile.BlendMode, ((tile.PackedTexture >> 5) & 3) != 0, (tile.PackedTexture & 0x10) != 0, FieldBackgroundTileDecoder.Decode(mim, tile)) { PackedPalette = tile.PackedPalette }).ToArray();
        for (int index = 0; index < tiles.Length; index++)
        {
            BackgroundTile tile = tiles[index];
            ResourceKey owner = new($"field/{name}/{name}.mim");
            MapTile original = map.Tiles[index];
            TextureSourceRegion region = new(original.TextureId * 64, 256, original.Uses8BitIndices ? 8 : 4, 256, 256);
            tiles[index] = tile with { Texture = textureScope.Register(new TextureKey(owner, $"tile/{index}", original.PaletteId), tile.Image, "classic-field-tile", region, nativeAlias: FieldBackgroundTileDecoder.Address(owner, original), nativeSheet: backgroundAtlas[index]) };
        }
        byte[] one = Read(nested, "chara.one");
        List<NativeFieldOneModelEntry> nativeDirectory = [];
        Dictionary<string, NativeFieldModelSource> nativeMain = [];
        byte[] ReadRegistrationMain(string model)
        {
            byte[] bytes = ReadMainCharacter(model);
            nativeMain.TryAdd(model, new($"field/main_chr/{model}.mch", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), bytes.ToImmutableArray()));
            return bytes;
        }
        IReadOnlyList<FieldModel> models = (resources.GetSource<IFieldModelSource>()?.ReadModels(new ResourceKey($"field/{name}/chara.one"), one, ReadRegistrationMain, nativeDirectory.Add) ?? FieldModelReader.Read(one, ReadRegistrationMain, nativeDirectory.Add)).Select((model, index) => RegisterFieldModelTextures(name, index, model)).ToArray();
        IReadOnlyList<FieldCamera> cameras = FieldCamera.ReadAll(Read(nested, name + ".ca"));
        byte[] information = Read(nested, name + ".inf");
        byte[] walkmesh = Read(nested, name + ".id");
        FieldAssets result = new(fieldId, name, Read(nested, name + ".jsm"), MsdFile.Read(Read(nested, name + ".msd"), FF8Encoding.CreateEuropean()).Texts.ToArray(), tiles, cameras[0], FieldWalkmesh.Read(walkmesh), models) { Cameras = cameras, Information = information.Length == 676 ? FieldInformation.Read(information) : null, WalkmeshHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(walkmesh)), NativeExecutableHash = NativeExecutableHash };
        if (NativeRuntimeProfile is { IsVerified: true } registrationProfile && nativeDirectory.Count == models.Count)
        {
            result = result with { NativeRegistration = new(fieldId, name, registrationProfile.ExecutableHash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(result.Jsm)), new($"field/{name}/chara.one", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(one)), one.ToImmutableArray()), nativeDirectory.ToImmutableArray(), nativeMain.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToImmutableArray()) { Loader = ReadNativeFieldRegistrationInputs(fieldId, information, result.Jsm, ReadOptionalFieldColors(() => Read(nested, name + ".pcb"))) } };
        }
        result = result with { RandomEncounters = NativeEncounterTables.ReadField(Read(nested, name + ".rat"), Read(nested, name + ".mrt")) };
        if (result.Information?.NativeMovieMaskFlag14 == 1)
        {
            //471010 admits MSK only through the original INF byte14, independently of the selected CA camera.
            result = result with { MovieMask = FieldMovieMask.Read(Read(nested, name + ".msk")) };
        }
        fields.Add(fieldId, result);
        return ApplyModelOverrides(result);
    }

    private FieldAssets ApplyModelOverrides(FieldAssets original)
    {
        if (modelOverrides is null)
        {
            return original;
        }
        return original with { Models = original.Models.Select((model, index) => modelOverrides.Apply($"field/{original.Name}/{index}", model)).ToArray() };
    }

    public string GetMoviePath(int index, int disk = 1)
    {
        string stem = $"disc{disk - 1:00}_{index:00}";
        IGameFileSource files = resources.GetSource<IGameFileSource>() ?? throw new NotImplementedException("The resource source has no movie file capability.");
        foreach (string suffix in new[] { "h.avi", ".bik", ".avi", "h.bik" })
        {
            try
            {
                string resolved = files.ResolveFile("movies/" + stem + suffix);
                if (File.Exists(resolved))
                {
                    return resolved;
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
        throw new FileNotFoundException($"Movie {stem} is absent from the game resource source.");
    }

    public FieldSoundEffectTable LoadFieldSoundEffects(int fieldId)
    {
        if (fieldSoundEffects.TryGetValue(fieldId, out FieldSoundEffectTable? cached))
        {
            return cached;
        }
        if ((uint)fieldId >= fieldNames.Length || string.IsNullOrWhiteSpace(fieldNames[fieldId]))
        {
            throw new ArgumentOutOfRangeException(nameof(fieldId));
        }
        string name = fieldNames[fieldId];
        using IFlArchive nested = OpenNested(name);
        FieldSoundEffectTable table = FieldSoundEffectTable.Read(name + ".sfx", Read(nested, name + ".sfx"));
        fieldSoundEffects.Add(fieldId, table);
        return table;
    }

    private byte[] ReadMainCharacter(string name)
    {
        mainCharacters ??= OpenNested("main_chr");
        return Read(mainCharacters, name + ".mch");
    }

    public byte[] ReadResource(ResourceKey key) => resources.Read(key);

    public byte[] ReadNativeExecutable() => GameDataResources.ReadNativeExecutable(resources);

    public IFlArchive OpenResourceArchive(string family) => OpenArchive(family);

    private Exception UnsupportedRuntime(string message)
    {
        _ = GameDataResources.RequireNativeRuntimeProfile(resources);
        return new NotSupportedException(message);
    }

    private IFlArchive OpenArchive(string family)
    {
        IFlArchive archive = archiveSource.OpenArchive(family);
        archiveIdentities[archive] = family;
        return archive;
    }

    private IFlArchive OpenNested(string name)
    {
        IFlArchive result = FlArchive.Open(new MemoryStream(Read(outer, name + ".fl"), false), new MemoryStream(Read(outer, name + ".fi"), false), new MemoryStream(Read(outer, name + ".fs"), false), false, FlArchiveRepresentation.Files);
        archiveIdentities[result] = "field/" + name;
        return result;
    }

    private byte[] Read(IFlArchive archive, string basename)
    {
        IFlArchiveEntry? entry = archive.Entries.SingleOrDefault(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(basename, StringComparison.OrdinalIgnoreCase));
        if (entry is not null && overrides?.TryRead(entry.RelativePath.Replace('\\', '/').TrimStart('/'), out byte[]? content) == true)
        {
            return content;
        }
        if (archiveIdentities.TryGetValue(archive, out string? identity))
        {
            return resources.Read(new ResourceKey(identity + "/" + basename));
        }
        if (entry is null)
        {
            throw new FileNotFoundException($"Resource '{basename}' is unavailable from its archive.");
        }
        using Stream source = entry.OpenForRead();
        using MemoryStream result = ResourceTrafficTelemetry.CreateStagingStream(checked((int)entry.Size));
        source.CopyTo(result);
        return result.ToArray();
    }

    public void Dispose()
    {
        textureScope.Dispose();
        mainCharacters?.Dispose();
        outer.Dispose();
        ownedSource?.Dispose();
    }
}
