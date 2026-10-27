using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Authenticated C75D20 random bytes/C76878 effect records and original WMSET section37 forest sprites.</summary>
public sealed class NativeWorldForestTables
{
    private readonly byte[] random;
    public ReadOnlySpan<byte> Random => random;
    public string SourceHash { get; }
    public IReadOnlyList<NativeWorldForestEffect> Effects { get; }
    public IReadOnlyList<ushort> PaletteY { get; }
    public IReadOnlyList<NativeWorldPathPosition> RagnarokThrustOffsets { get; }
    public NativeWorldForestEffect SteamEffect { get; }
    public IReadOnlyList<NativeWorldForestEffect> AllEffects { get; }

    public NativeWorldForestEffect GetEffect(byte type) => AllEffects.Single(effect => effect.Type == type);

    public NativeWorldForestTables(string gameDirectory)
    {
        using IResourceSource source = GameDataSourceFactory.Open(gameDirectory);
        NativeWorldForestTables tables = new(source as IGameNativeMemorySource ?? throw new NotSupportedException("The resource source has no authenticated native world memory contract."));
        SourceHash = tables.SourceHash;
        random = tables.random;
        Effects = tables.Effects;
        PaletteY = tables.PaletteY;
        RagnarokThrustOffsets = tables.RagnarokThrustOffsets;
        SteamEffect = tables.SteamEffect;
        AllEffects = tables.AllEffects;
    }

    public NativeWorldForestTables(byte[] image) : this(new NativeExecutableMemory(image))
    {
    }

    public NativeWorldForestTables(IGameNativeMemorySource memory)
    {
        WorldNativeMemoryContracts.RequireSupported(memory);
        SourceHash = memory.SourceHash;
        random = memory.Read(0xC75D20, 256);
        SteamEffect = ReadEffect(0, memory.Read(0xC76878, 40), 0);
        byte[] thrust = memory.Read(0xC76790, 16);
        RagnarokThrustOffsets = Enumerable.Range(0, 2).Select(index => new NativeWorldPathPosition(BinaryPrimitives.ReadInt16LittleEndian(thrust.AsSpan(index * 8)), BinaryPrimitives.ReadInt16LittleEndian(thrust.AsSpan(index * 8 + 2)), BinaryPrimitives.ReadInt16LittleEndian(thrust.AsSpan(index * 8 + 4)))).ToArray();
        byte[] records = memory.Read(0xC76878 + 40 * 4, 40 * 18);
        List<NativeWorldForestEffect> effects = [];
        List<ushort> palettes = [];
        for (byte type = 4; type <= 21; type++)
        {
            ReadOnlySpan<byte> record = records.AsSpan(40 * (type - 4), 40);
            ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(records.AsSpan(40 * (type - 4) + offset, 2));
            if (type == 10)
            {
                if (!record.SequenceEqual(Convert.FromHexString("68038000D003E100202001008080800180101002000380020800F8FF080000000000010000000000")))
                {
                    throw new InvalidDataException("Original Garden wake record differs from its authenticated type10 velocity/acceleration contract.");
                }
            }
            else if (type == 14)
            {
                if (!record.SequenceEqual(Convert.FromHexString("58038000A003E000202001008080800100110C060003FF000000F0FF000000000000020000000000")))
                {
                    throw new InvalidDataException("Original Cactuar burst record differs from its authenticated type14 shared-pool contract.");
                }
            }
            else if (type < 10 && (Word(0) != 880 || Word(2) != 96 || Word(4) != 832 || record[8] != 16 || record[9] != 16 || record[10] != 1 || record[12] != 128 || record[13] != 128 || record[14] != 128 || record[20] != 0 || record[21] != 0 || record[24..].ContainsAnyExcept((byte)0)))
            {
                throw new InvalidDataException("Original forest sprite record differs from its proved textured plane/zero-velocity contract.");
            }
            effects.Add(ReadEffect(type, records, 40 * (type - 4)));
            palettes.Add(Word(6));
        }
        Effects = effects.AsReadOnly();
        AllEffects = Effects.Concat(new[] { SteamEffect, ReadEffect(2, memory.Read(0xC76878 + 40 * 2, 40), 0), ReadEffect(3, memory.Read(0xC76878 + 40 * 3, 40), 0) }).ToArray();
        PaletteY = palettes.AsReadOnly();
    }

    private static NativeWorldForestEffect ReadEffect(byte type, byte[] data, int offset)
    {
        ushort Word(int position) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + position, 2));
        return new(type, Word(16), data[offset + 18], data[offset + 15], data[offset + 19], unchecked((short)Word(22)))
        {
            TextureX = Word(0),
            TextureY = Word(2),
            PaletteX = Word(4),
            PaletteY = Word(6),
            Width = data[offset + 8],
            Height = data[offset + 9],
            DrawMode = data[offset + 20],
            BlendMode = data[offset + 21],
            Red = data[offset + 12],
            Green = data[offset + 13],
            Blue = data[offset + 14],
            VelocityX = unchecked((short)Word(24)),
            VelocityY = unchecked((short)Word(26)),
            VelocityZ = unchecked((short)Word(28)),
            AccelerationX = unchecked((short)Word(32)),
            AccelerationY = unchecked((short)Word(34)),
            AccelerationZ = unchecked((short)Word(36))
        };
    }
}

public sealed partial class WorldAssets
{
    private NativeWorldForestTables? forestTables;
    private readonly Dictionary<(byte Type, int Frame), PixelImage> forestTextures = [];
    private readonly Dictionary<(byte Type, int Frame), Memoria.FF8.Resources.AssetHandle<Memoria.FF8.Resources.TextureValue>> forestTextureHandles = [];
    private TimImage[]? worldParticleImages;
    public NativeWorldForestTables ForestTables => forestTables ??= new NativeWorldForestTables(NativeWorldMemory);

    public PixelImage GetForestTexture(byte type, int frame)
    {
        if (type is > 0 and < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }
        ArgumentOutOfRangeException.ThrowIfGreaterThan(type, 21);
        NativeWorldForestEffect effect = ForestTables.GetEffect(type);
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frame, effect.Frames);
        if (forestTextures.TryGetValue((type, frame), out PixelImage? cached))
        {
            return forestTextureHandles.TryGetValue((type, frame), out var current) ? current.Value.Image : cached;
        }
        if (effect.Width == 0 || effect.Height == 0 || effect.Width % 4 != 0)
        {
            throw new InvalidDataException("Native world particle does not publish a complete indexed4 sprite rectangle.");
        }
        worldParticleImages ??= WorldAssetReader.InnerPointers(wmset, sections[37]).Select(pointer => WorldAssetReader.ReadTim(wmset, sections[37] + pointer).Image).ToArray();
        int xWords = effect.TextureX + frame * effect.Width / 4;
        TimImage tim = worldParticleImages.Single(image => image.PixelMode == TimPixelMode.Indexed4 && image.X <= xWords && image.X + image.WidthInWords >= xWords + effect.Width / 4 && image.Y <= effect.TextureY && image.Y + image.Height >= effect.TextureY + effect.Height && image.Clut is { } clut && clut.X <= effect.PaletteX && clut.X + clut.Width >= effect.PaletteX + 16 && clut.Y <= effect.PaletteY && clut.Y + clut.Height > effect.PaletteY);
        int palette = (effect.PaletteY - tim.Clut!.Y) * (tim.Clut.Width / 16) + (effect.PaletteX - tim.Clut.X) / 16;
        PixelImage atlas = tim.Decode(palette);
        int x = (xWords - tim.X) * 4;
        int y = effect.TextureY - tim.Y;
        PixelImage texture = new(effect.Width, effect.Height);
        for (int row = 0; row < texture.Height; row++)
        {
            atlas.Pixels.AsSpan((y + row) * atlas.Width + x, texture.Width).CopyTo(texture.Pixels.AsSpan(row * texture.Width, texture.Width));
        }
        forestTextures.Add((type, frame), texture);
        var handle = textureScope.Register(new Memoria.FF8.Resources.TextureKey(worldSettingsKey, $"forest/{type}/{frame}"), texture, "classic-world-sprite", nativeAlias: ReadSectionTextureAddress(38, tim, palette, x, y, texture.Width, texture.Height));
        if (handle is not null)
        {
            forestTextureHandles.Add((type, frame), handle);
        }
        return handle?.Value.Image ?? texture;
    }
}
