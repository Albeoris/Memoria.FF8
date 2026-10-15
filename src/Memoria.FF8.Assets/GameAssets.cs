using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Mim;
using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;
using System.Collections.Concurrent;

namespace Memoria.FF8.Assets;

/// <summary>Reads selected original assets without modifying or extracting the installed game.</summary>
public sealed class GameAssets : IDisposable
{
    private static readonly ConcurrentDictionary<(string Path, long Length, long ModifiedTicks), string> NativeExecutableHashes = new();
    private readonly IFlArchive outer;
    private readonly string[] fieldNames;
    private readonly Dictionary<int, FieldAssets> fields = [];
    private readonly Dictionary<int, FieldSoundEffectTable> fieldSoundEffects = [];
    private readonly IAssetOverrideSource? overrides;
    private IFlArchive? mainCharacters;
    private NativeVibrationBank? nativeVibrationBank;
    private NativeShopDefinitions? nativeShopDefinitions;
    private NativeWeaponShopDefinitions? nativeWeaponShopDefinitions;
    private NativeDrawPointCatalog? nativeDrawPointCatalog;

    public GameAssets(string gameDirectory, IAssetOverrideSource? overrides = null)
    {
        this.overrides = overrides;
        GameDirectory = Path.GetFullPath(gameDirectory);
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
        outer = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "field.fs"), FlArchiveRepresentation.Files);
        using IFlArchive mapData = OpenNested("mapdata");
        fieldNames = System.Text.Encoding.ASCII.GetString(Read(mapData, "maplist")).Replace("\r", "").Split('\n');
    }

    public string GameDirectory { get; }
    public string? NativeExecutableHash { get; }
    public NativeDrawPointCatalog ReadNativeDrawPointCatalog()
    {
        if (nativeDrawPointCatalog is not null)
        {
            return nativeDrawPointCatalog;
        }
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        nativeDrawPointCatalog = NativeDrawPointCatalogReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")), Read(main, "kernel.bin"));
        return nativeDrawPointCatalog;
    }
    public NativeVibrationBank ReadNativeVibrationBank() => nativeVibrationBank ??= NativeVibrationBankReader.ReadExecutable(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
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
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        return NativeCharacterInitialization.Read(Read(main, "init.out"), Read(main, "kernel.bin"));
    }
    public NativeEconomy ReadNewGameEconomy()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native economy initialization requires the authenticated original executable.");
        }
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        return NativeEconomyInitialization.Read(Read(main, "init.out"));
    }

    public NativeShopHistory ReadNewGameShopHistory()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native shop initialization requires the authenticated original executable.");
        }
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        return NativeShopDefinitionReader.ReadNewGameHistory(Read(main, "init.out"));
    }

    public NativeShopDefinitions ReadNativeShopDefinitions()
    {
        if (nativeShopDefinitions is not null)
        {
            return nativeShopDefinitions;
        }
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        using IFlArchive menu = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "menu.fs"), FlArchiveRepresentation.Files);
        return nativeShopDefinitions = NativeShopDefinitionReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")), Read(menu, "shop.bin"), Read(menu, "price.bin"), Read(main, "kernel.bin"));
    }

    public NativeWeaponRecipes ReadNewGameWeaponRecipes()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native known-weapon initialization requires the authenticated original executable.");
        }
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        return NativeWeaponShopDefinitionReader.ReadNewGameRecipes(Read(main, "init.out"));
    }

    public NativeWeaponShopDefinitions ReadNativeWeaponShopDefinitions()
    {
        if (nativeWeaponShopDefinitions is not null)
        {
            return nativeWeaponShopDefinitions;
        }
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        using IFlArchive menu = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "menu.fs"), FlArchiveRepresentation.Files);
        return nativeWeaponShopDefinitions = NativeWeaponShopDefinitionReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")), Read(main, "kernel.bin"), Read(menu, "mwepon.bin"), Read(menu, "mwepon.msg"));
    }

    public NativeShotAvailabilityState ReadNewGameShotAvailability()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native shot availability initialization requires the authenticated original executable.");
        }
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        return NativeShotAvailabilityInitialization.Read(Read(main, "init.out"), Read(main, "kernel.bin"));
    }
    public NativeInputConfiguration ReadNewGameInputConfiguration()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native input initialization requires the authenticated original executable.");
        }
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
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
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
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
        BackgroundTile[] tiles = map.Tiles.Select(tile => new BackgroundTile(tile.X, tile.Y, unchecked((ushort)tile.Z), tile.Parameter, tile.State, tile.PackedLayer >> 1, tile.BlendMode, ((tile.PackedTexture >> 5) & 3) != 0, (tile.PackedTexture & 0x10) != 0, FieldBackgroundTileDecoder.Decode(mim, tile))).ToArray();
        byte[] one = Read(nested, "chara.one");
        IReadOnlyList<FieldModel> models = FieldModelReader.Read(one, ReadMainCharacter);
        IReadOnlyList<FieldCamera> cameras = FieldCamera.ReadAll(Read(nested, name + ".ca"));
        byte[] information = Read(nested, name + ".inf");
        byte[] walkmesh = Read(nested, name + ".id");
        FieldAssets result = new(fieldId, name, Read(nested, name + ".jsm"), MsdFile.Read(Read(nested, name + ".msd"), FF8Encoding.CreateEuropean()).Texts.ToArray(), tiles, cameras[0], FieldWalkmesh.Read(walkmesh), models) { Cameras = cameras, Information = information.Length == 676 ? FieldInformation.Read(information) : null, WalkmeshHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(walkmesh)), NativeExecutableHash = NativeExecutableHash };
        fields.Add(fieldId, result);
        return result;
    }

    public string GetMoviePath(int index, int disk = 1)
    {
        string stem = $"disc{disk - 1:00}_{index:00}";
        string directory = Path.Combine(GameDirectory, "Data", "movies");
        foreach (string suffix in new[] { "h.avi", ".bik", ".avi", "h.bik" })
        {
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

    private IFlArchive OpenNested(string name)
    {
        return FlArchive.Open(new MemoryStream(Read(outer, name + ".fl"), false), new MemoryStream(Read(outer, name + ".fi"), false), new MemoryStream(Read(outer, name + ".fs"), false), false, FlArchiveRepresentation.Files);
    }

    private byte[] Read(IFlArchive archive, string basename)
    {
        IFlArchiveEntry entry = archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(basename, StringComparison.OrdinalIgnoreCase));
        string logicalPath = entry.RelativePath.Replace('\\', '/').TrimStart('/');
        if (overrides?.TryRead(logicalPath, out byte[]? content) == true)
        {
            return content;
        }
        using Stream source = entry.OpenForRead();
        using MemoryStream result = new(checked((int)entry.Size));
        source.CopyTo(result);
        return result.ToArray();
    }

    public void Dispose()
    {
        mainCharacters?.Dispose();
        outer.Dispose();
    }
}
