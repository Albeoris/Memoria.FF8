using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Authenticated C75D20 random bytes/C76878 effect records and original WMSET section37 forest sprites.</summary>
public sealed class NativeWorldForestTables
{
    private readonly byte[] random;
    public ReadOnlySpan<byte> Random => random;
    public IReadOnlyList<NativeWorldForestEffect> Effects { get; }
    public IReadOnlyList<ushort> PaletteY { get; }

    public NativeWorldForestTables(string gameDirectory) : this(GameDataResources.ReadNativeExecutable(gameDirectory))
    {
    }

    public NativeWorldForestTables(byte[] image)
    {
        if (!Convert.ToHexString(SHA256.HashData(image)).Equals(NativeFieldRuntimeProfile.FF8EnExecutableHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Forest tables require the authenticated original FF8_EN executable.");
        }
        using PEReader pe = new(new MemoryStream(image));
        int Offset(uint address)
        {
            int rva = checked((int)(address - pe.PEHeaders.PEHeader!.ImageBase));
            SectionHeader section = pe.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva < section.VirtualAddress + section.SizeOfRawData);
            return rva - section.VirtualAddress + section.PointerToRawData;
        }
        random = image.AsSpan(Offset(0xC75D20), 256).ToArray();
        List<NativeWorldForestEffect> effects = [];
        List<ushort> palettes = [];
        for (byte type = 4; type <= 14; type++)
        {
            ReadOnlySpan<byte> record = image.AsSpan(Offset(0xC76878) + 40 * type, 40);
            ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(Offset(0xC76878) + 40 * type + offset, 2));
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
            effects.Add(new(type, Word(16), record[18], record[15], record[19], unchecked((short)Word(22)))
            {
                VelocityX = unchecked((short)Word(24)),
                VelocityY = unchecked((short)Word(26)),
                VelocityZ = unchecked((short)Word(28)),
                AccelerationX = unchecked((short)Word(32)),
                AccelerationY = unchecked((short)Word(34)),
                AccelerationZ = unchecked((short)Word(36))
            });
            palettes.Add(Word(6));
        }
        Effects = effects.AsReadOnly();
        PaletteY = palettes.AsReadOnly();
    }
}

public sealed partial class WorldAssets
{
    private NativeWorldForestTables? forestTables;
    private readonly Dictionary<(byte Type, int Frame), PixelImage> forestTextures = [];
    private readonly Dictionary<(byte Type, int Frame), Memoria.FF8.Resources.AssetHandle<Memoria.FF8.Resources.TextureValue>> forestTextureHandles = [];
    private TimImage? forestTim;
    private TimImage? gardenWakeTim;
    private TimImage? cactuarBurstTim;
    public NativeWorldForestTables ForestTables => forestTables ??= new NativeWorldForestTables(GameDataResources.ReadNativeExecutable(resources));

    public PixelImage GetForestTexture(byte type, int frame)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(type, 4);
        if (type is not (>= 4 and <= 10 or 14))
        {
            throw new NotSupportedException("This world particle has no represented native sprite producer.");
        }
        NativeWorldForestEffect effect = ForestTables.Effects[type - 4];
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frame, effect.Frames);
        if (forestTextures.TryGetValue((type, frame), out PixelImage? cached))
        {
            return forestTextureHandles.TryGetValue((type, frame), out var current) ? current.Value.Image : cached;
        }
        TimImage tim;
        int size;
        if (type == 14)
        {
            cactuarBurstTim ??= WorldAssetReader.InnerPointers(wmset, sections[37]).Select(pointer => WorldAssetReader.ReadTim(wmset, sections[37] + pointer).Image).Single(image => image.X == 856 && image.Y == 128 && image.WidthInWords == 8 && image.Height == 32);
            tim = cactuarBurstTim;
            size = 32;
            if (tim.PixelMode != TimPixelMode.Indexed4 || tim.Clut is not { X: 928, Y: 224, Width: 16 })
            {
                throw new InvalidDataException("Native Cactuar burst TIM/CLUT does not match its original VRAM rectangle.");
            }
        }
        else if (type == 10)
        {
            gardenWakeTim ??= WorldAssetReader.InnerPointers(wmset, sections[37]).Select(pointer => WorldAssetReader.ReadTim(wmset, sections[37] + pointer).Image).Single(image => image.X == 872 && image.Y == 128 && image.WidthInWords == 8 && image.Height == 32);
            tim = gardenWakeTim;
            size = 32;
            if (tim.PixelMode != TimPixelMode.Indexed4 || tim.Clut is not { X: 976, Y: 225, Width: 16 })
            {
                throw new InvalidDataException("Native Garden wake TIM/CLUT does not match its original VRAM rectangle.");
            }
        }
        else
        {
            forestTim ??= WorldAssetReader.InnerPointers(wmset, sections[37]).Select(pointer => WorldAssetReader.ReadTim(wmset, sections[37] + pointer).Image).Single(image => image.X == 880 && image.Y == 96 && image.WidthInWords == 16 && image.Height == 16);
            tim = forestTim;
            size = 16;
        }
        if (type is not (10 or 14) && (tim.PixelMode != TimPixelMode.Indexed4 || tim.Clut is not { X: 832, Y: 226, Width: 16, Height: 6 }))
        {
            throw new InvalidDataException("Native forest TIM/CLUT does not match its original VRAM rectangle.");
        }
        PixelImage atlas = tim.Decode(ForestTables.PaletteY[type - 4] - tim.Clut!.Y);
        PixelImage texture = new(size, size);
        for (int row = 0; row < size; row++)
        {
            atlas.Pixels.AsSpan(row * atlas.Width + frame * size, size).CopyTo(texture.Pixels.AsSpan(row * size, size));
        }
        forestTextures.Add((type, frame), texture);
        var handle = textureScope.Register(new Memoria.FF8.Resources.TextureKey(new Memoria.FF8.Resources.ResourceKey("world/wmsetus.obj"), $"forest/{type}/{frame}"), texture, "classic-world-sprite");
        if (handle is not null)
        {
            forestTextureHandles.Add((type, frame), handle);
        }
        return handle?.Value.Image ?? texture;
    }
}
