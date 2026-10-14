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

    public NativeWorldForestTables(string gameDirectory)
    {
        byte[] image = File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe"));
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
        for (byte type = 4; type <= 9; type++)
        {
            ReadOnlySpan<byte> record = image.AsSpan(Offset(0xC76878) + 40 * type, 40);
            ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(Offset(0xC76878) + 40 * type + offset, 2));
            if (Word(0) != 880 || Word(2) != 96 || Word(4) != 832 || record[8] != 16 || record[9] != 16 || record[10] != 1 || record[12] != 128 || record[13] != 128 || record[14] != 128 || record[20] != 0 || record[21] != 0 || record[24..].ContainsAnyExcept((byte)0))
            {
                throw new InvalidDataException("Original forest sprite record differs from its proved textured plane/zero-velocity contract.");
            }
            effects.Add(new(type, Word(16), record[18], record[15], record[19], unchecked((short)Word(22))));
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
    private TimImage? forestTim;
    public NativeWorldForestTables ForestTables => forestTables ??= new NativeWorldForestTables(gameDirectory);

    public PixelImage GetForestTexture(byte type, int frame)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(type, 4);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(type, 9);
        NativeWorldForestEffect effect = ForestTables.Effects[type - 4];
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frame, effect.Frames);
        if (forestTextures.TryGetValue((type, frame), out PixelImage? cached))
        {
            return cached;
        }
        forestTim ??= WorldAssetReader.InnerPointers(wmset, sections[37]).Select(pointer => WorldAssetReader.ReadTim(wmset, sections[37] + pointer).Image).Single(tim => tim.X == 880 && tim.Y == 96 && tim.WidthInWords == 16 && tim.Height == 16);
        if (forestTim.PixelMode != TimPixelMode.Indexed4 || forestTim.Clut is not { X: 832, Y: 226, Width: 16, Height: 6 })
        {
            throw new InvalidDataException("Native forest TIM/CLUT does not match its original VRAM rectangle.");
        }
        PixelImage atlas = forestTim.Decode(ForestTables.PaletteY[type - 4] - forestTim.Clut.Y);
        PixelImage texture = new(16, 16);
        for (int row = 0; row < 16; row++)
        {
            atlas.Pixels.AsSpan(row * atlas.Width + frame * 16, 16).CopyTo(texture.Pixels.AsSpan(row * 16, 16));
        }
        forestTextures.Add((type, frame), texture);
        return texture;
    }
}
