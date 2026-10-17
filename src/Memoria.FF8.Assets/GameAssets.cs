using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Mim;
using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Reads selected original assets without modifying or extracting the installed game.</summary>
public sealed partial class GameAssets : IDisposable
{
    private static readonly ConcurrentDictionary<(string Path, long Length, long ModifiedTicks), string> NativeExecutableHashes = new();
    private readonly IFlArchive outer;
    private readonly IGameArchiveSource archiveSource;
    private readonly IResourceSource? ownedSource;
    private readonly string[] fieldNames;
    private readonly Dictionary<int, FieldAssets> fields = [];
    private readonly Dictionary<int, FieldSoundEffectTable> fieldSoundEffects = [];
    private readonly IAssetOverrideSource? overrides;
    private readonly ResourceService? resources;
    private readonly IMusicCueResolver musicCueResolver;
    private readonly AssetTextureScope textureScope;
    private readonly Dictionary<IFlArchive, string> archiveIdentities = [];
    private IFlArchive? mainCharacters;
    private NativeVibrationBank? nativeVibrationBank;
    private NativeShopDefinitions? nativeShopDefinitions;
    private NativeWeaponShopDefinitions? nativeWeaponShopDefinitions;
    private NativeDrawPointCatalog? nativeDrawPointCatalog;
    private NativeEncounterTables? nativeEncounterTables;

    public GameAssets(string gameDirectory, IAssetOverrideSource? overrides = null, ResourceService? resources = null, IMusicCueResolver? musicCueResolver = null)
    {
        this.musicCueResolver = musicCueResolver ?? new ClassicMusicCueResolver();
        this.resources = resources;
        textureScope = new AssetTextureScope(resources);
        this.overrides = overrides;
        GameDirectory = Path.GetFullPath(gameDirectory);
        if (resources is null)
        {
            ClassicGameDataSource source = new(GameDirectory);
            ownedSource = source;
            archiveSource = source;
        }
        else
        {
            archiveSource = resources.GetSource<IGameArchiveSource>() ?? throw new NotSupportedException("The game resource source does not support archive enumeration.");
        }
        IFlArchive? openedArchive = null;
        try
        {
            string executable = Path.Combine(GameDirectory, "FF8_EN.exe");
            if (File.Exists(executable))
            {
                FileInfo info = new(executable);
                NativeExecutableHash = NativeExecutableHashes.GetOrAdd((info.FullName, info.Length, info.LastWriteTimeUtc.Ticks), identity =>
                {
                    using FileStream stream = File.OpenRead(identity.Path);
                    return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
                });
            }
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
    public string? NativeExecutableHash { get; }
    public NativeEncounterTables ReadNativeEncounterTables() => nativeEncounterTables ??= NativeEncounterTables.ReadExecutable(ReadNativeExecutable());
    public NativeDrawPointCatalog ReadNativeDrawPointCatalog()
    {
        if (nativeDrawPointCatalog is not null)
        {
            return nativeDrawPointCatalog;
        }
        using IFlArchive main = OpenArchive("main");
        nativeDrawPointCatalog = NativeDrawPointCatalogReader.Read(ReadNativeExecutable(), Read(main, "kernel.bin"));
        return nativeDrawPointCatalog;
    }
    public NativeVibrationBank ReadNativeVibrationBank() => nativeVibrationBank ??= NativeVibrationBankReader.ReadExecutable(ReadNativeExecutable());
    public NativeShakeTables ReadNativeShakeTables()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native shake tables require the preserved English executable profile.");
        }
        using FileStream stream = File.OpenRead(Path.Combine(GameDirectory, "FF8_EN.exe"));
        byte[] random = new byte[256];
        byte[] cosine = new byte[512];
        stream.Position = 0x780A18;
        stream.ReadExactly(random);
        stream.Position = 0x780D9C;
        stream.ReadExactly(cosine);
        return new NativeShakeTables(random, cosine);
    }
    public NativeCharacterVitals ReadNewGameCharacterVitals()
    {
        if (NativeRuntimeProfile is null)
        {
            throw new NotSupportedException("Native character initialization requires the proved installed executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeCharacterInitialization.Read(Read(main, "init.out"), Read(main, "kernel.bin"));
    }

    public NativeBattleExperienceDefinition ReadNativeBattleExperienceDefinition()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Battle experience requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeBattleExperienceDefinitionReader.Read(Read(main, "kernel.bin"));
    }
    public NativeEconomy ReadNewGameEconomy()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native economy initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeEconomyInitialization.Read(Read(main, "init.out"));
    }

    public NativeShopHistory ReadNewGameShopHistory()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native shop initialization requires the authenticated original executable.");
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
        return nativeShopDefinitions = NativeShopDefinitionReader.Read(ReadNativeExecutable(), Read(menu, "shop.bin"), Read(menu, "price.bin"), Read(main, "kernel.bin"));
    }

    public NativeWeaponRecipes ReadNewGameWeaponRecipes()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native known-weapon initialization requires the authenticated original executable.");
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
        return nativeWeaponShopDefinitions = NativeWeaponShopDefinitionReader.Read(ReadNativeExecutable(), Read(main, "kernel.bin"), Read(menu, "mwepon.bin"), Read(menu, "mwepon.msg"));
    }

    public NativeShotAvailabilityState ReadNewGameShotAvailability()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native shot availability initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeShotAvailabilityInitialization.Read(Read(main, "init.out"), Read(main, "kernel.bin"));
    }

    public NativeLimitProgress ReadNewGameLimitProgress()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native Limit initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeLimitProgressInitialization.Read(Read(main, "init.out"));
    }

    public NativePocketStationState ReadNewGamePocketStation()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("PocketStation initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativePocketStationInitialization.Read(Read(main, "init.out"));
    }

    public NativeSavedBattleFlags ReadNewGameSavedBattleFlags()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Saved battle flag initialization requires the authenticated original executable.");
        }
        //56D9F0/47E410 clear1CFDC58..1CFEFF8.56DA10's init.out payload ends1CFE7A4, before BYTE1CFE97A.
        return new NativeSavedBattleFlags(NativeInputConfiguration.OriginalInitHash, 0);
    }
    public NativeInputConfiguration ReadNewGameInputConfiguration()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native input initialization requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeInputInitialization.Read(Read(main, "init.out"));
    }
    public NativeSalaryTable ReadNativeSalaryTable()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Salary lookup requires the authenticated original executable.");
        }
        using FileStream stream = File.OpenRead(Path.Combine(GameDirectory, "FF8_EN.exe"));
        byte[] raw = new byte[64];
        stream.Position = 0x78E474;
        stream.ReadExactly(raw);
        ushort[] entries = new ushort[32];
        for (int index = 0; index < entries.Length; index++)
        {
            entries[index] = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(index * 2));
        }
        NativeSalaryTable table = new(NativeExecutableHash!, System.Collections.Immutable.ImmutableArray.CreateRange(entries));
        table.Validate();
        return table;
    }
    public NativeFieldRuntimeProfile? NativeRuntimeProfile => NativeExecutableHash == NativeFieldRuntimeProfile.FF8EnExecutableHash ? NativeFieldRuntimeProfile.FF8En : null;

    public NativeCharacterAppearance ReadNewGameCharacterAppearance()
    {
        if (NativeRuntimeProfile is null)
        {
            throw new NotSupportedException("Native appearance initialization requires the proved installed executable.");
        }
        using IFlArchive main = OpenArchive("main");
        return NativeAppearanceInitialization.Read(Read(main, "init.out"));
    }

    public FieldAssets LoadField(int fieldId)
    {
        if (fields.TryGetValue(fieldId, out FieldAssets? cached))
        {
            return cached;
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
        BackgroundTile[] tiles = map.Tiles.Select(tile => new BackgroundTile(tile.X, tile.Y, unchecked((ushort)tile.Z), tile.Parameter, tile.State, tile.PackedLayer >> 1, tile.BlendMode, ((tile.PackedTexture >> 5) & 3) != 0, (tile.PackedTexture & 0x10) != 0, FieldBackgroundTileDecoder.Decode(mim, tile)) { PackedPalette = tile.PackedPalette }).ToArray();
        for (int index = 0; index < tiles.Length; index++)
        {
            BackgroundTile tile = tiles[index];
            tiles[index] = tile with { Texture = textureScope.Register(new TextureKey(new ResourceKey($"field/{name}/{name}.mim"), $"tile/{index}", map.Tiles[index].PaletteId), tile.Image, "classic-field-tile") };
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
        IReadOnlyList<FieldModel> models = (resources?.GetSource<IFieldModelSource>()?.ReadModels(new ResourceKey($"field/{name}/chara.one"), one, ReadRegistrationMain, nativeDirectory.Add) ?? FieldModelReader.Read(one, ReadRegistrationMain, nativeDirectory.Add)).Select((model, index) => model with { Textures = textureScope.RegisterList(new ResourceKey($"field/{name}/chara.one"), model.Textures, "classic-model", $"model/{index}/texture") }).ToArray();
        IReadOnlyList<FieldCamera> cameras = FieldCamera.ReadAll(Read(nested, name + ".ca"));
        byte[] information = Read(nested, name + ".inf");
        byte[] walkmesh = Read(nested, name + ".id");
        FieldAssets result = new(fieldId, name, Read(nested, name + ".jsm"), MsdFile.Read(Read(nested, name + ".msd"), FF8Encoding.CreateEuropean()).Texts.ToArray(), tiles, cameras[0], FieldWalkmesh.Read(walkmesh), models) { Cameras = cameras, Information = information.Length == 676 ? FieldInformation.Read(information) : null, WalkmeshHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(walkmesh)), NativeExecutableHash = NativeExecutableHash };
        if (NativeRuntimeProfile is { IsVerified: true } registrationProfile && nativeDirectory.Count == models.Count)
        {
            result = result with { NativeRegistration = new(fieldId, name, registrationProfile.ExecutableHash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(result.Jsm)), new($"field/{name}/chara.one", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(one)), one.ToImmutableArray()), nativeDirectory.ToImmutableArray(), nativeMain.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToImmutableArray()) };
        }
        result = result with { RandomEncounters = NativeEncounterTables.ReadField(Read(nested, name + ".rat"), Read(nested, name + ".mrt")) };
        if (result.Information?.NativeMovieMaskFlag14 == 1)
        {
            //471010 admits MSK only through the original INF byte14, independently of the selected CA camera.
            result = result with { MovieMask = FieldMovieMask.Read(Read(nested, name + ".msk")) };
        }
        fields.Add(fieldId, result);
        return result;
    }

    public string GetMoviePath(int index, int disk = 1)
    {
        string stem = $"disc{disk - 1:00}_{index:00}";
        string directory = Path.Combine(GameDirectory, "Data", "movies");
        foreach (string suffix in new[] { "h.avi", ".bik", ".avi", "h.bik" })
        {
            if (archiveSource is IGameFileSource files)
            {
                try
                {
                    string resolved = files.ResolveFile("movies/" + stem + suffix);
                    if (File.Exists(resolved))
                    {
                        return resolved;
                    }
                    continue;
                }
                catch (FileNotFoundException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }
            }
            string path = Path.Combine(directory, stem + suffix);
            if (File.Exists(path))
            {
                return path;
            }
        }
        throw new FileNotFoundException($"Movie {stem} is absent from {directory}.");
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

    private byte[] ReadNativeExecutable()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native executable tables require the authenticated original executable profile.");
        }
        return File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe"));
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
        if (resources is not null && archiveIdentities.TryGetValue(archive, out string? identity))
        {
            return resources.Read(new ResourceKey(identity + "/" + basename));
        }
        if (entry is null)
        {
            throw new FileNotFoundException($"Resource '{basename}' is unavailable from its archive.");
        }
        using Stream source = entry.OpenForRead();
        using MemoryStream result = new(checked((int)entry.Size));
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
