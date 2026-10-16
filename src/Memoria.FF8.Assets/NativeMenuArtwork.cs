using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tex;

namespace Memoria.FF8.Assets;

/// <summary>Original PC menu portraits and indexed icon sprites, read directly from menu.fs.</summary>
public sealed class NativeMenuArtwork
{
    public const int CharacterCount = 11;
    public const int GuardianForceCount = 16;
    private readonly PixelImage[] characters;
    private readonly PixelImage[] guardianForces;
    private readonly PixelImage[] iconPalettes;
    private readonly byte[] iconIndex;
    private readonly Dictionary<(int Index, int PaletteOffset), PixelImage> icons = [];

    private NativeMenuArtwork(PixelImage[] characters, PixelImage[] guardianForces, PixelImage[] iconPalettes, byte[] iconIndex)
    {
        this.characters = characters;
        this.guardianForces = guardianForces;
        this.iconPalettes = iconPalettes;
        this.iconIndex = iconIndex;
        IconCount = ReadCount(iconIndex);
    }

    public int IconCount { get; }

    /// <summary>Native character ID order: Squall, Zell, Irvine, Quistis, Rinoa, Selphie, Seifer, Edea, Laguna, Kiros, Ward.</summary>
    public PixelImage GetCharacterPortrait(int characterId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(characterId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(characterId, CharacterCount);
        return characters[characterId];
    }

    /// <summary>Native junctionable GF ID order, from Quezacotl (0) through Eden (15).</summary>
    public PixelImage GetGuardianForcePortrait(int guardianForceId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(guardianForceId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(guardianForceId, GuardianForceCount);
        return guardianForces[guardianForceId];
    }

    /// <summary>Composes an icon.sp1 sprite with its original per-part palettes and optional native CLUT offset.</summary>
    public PixelImage GetIcon(int index, int paletteOffset = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, IconCount);
        if (index is >= 128 and < 140)
        {
            throw new NotSupportedException("Original menu icons 128 through 139 require the current input binding, rather than a static texture sprite.");
        }
        if (!icons.TryGetValue((index, paletteOffset), out PixelImage? image))
        {
            image = DecodeIcon(iconPalettes, iconIndex, index, paletteOffset);
            icons.Add((index, paletteOffset), image);
        }
        return image;
    }

    public static NativeMenuArtwork Load(string gameDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        using IFlArchive menu = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "menu.fs"), FlArchiveRepresentation.Files);
        byte[] faces = ReadEntry(menu, "face.sp2");
        PixelImage[] characterAtlases = [ReadPortraitAtlas(menu, "Face_b00.TEX"), ReadPortraitAtlas(menu, "Face_b01.TEX")];
        PixelImage[] guardianForceAtlases = [ReadPortraitAtlas(menu, "Gf_big00.TEX"), ReadPortraitAtlas(menu, "Gf_big01.TEX")];
        PixelImage[] characters = Enumerable.Range(0, CharacterCount).Select(id => DecodePortrait(characterAtlases, faces, id)).ToArray();
        PixelImage[] guardianForces = Enumerable.Range(0, GuardianForceCount).Select(id => DecodePortrait(guardianForceAtlases, faces, 32 + id)).ToArray();
        TexFile texture = TexFile.Read(ReadEntry(menu, "icon.TEX"));
        if (texture.Header.Width != 256 || texture.Header.Height != 256 || texture.Palettes.Count != 16)
        {
            throw new InvalidDataException("Original menu icon texture requires a 256x256 atlas and sixteen palettes.");
        }
        PixelImage[] palettes = Enumerable.Range(0, texture.Palettes.Count).Select(texture.Decode).ToArray();
        return new NativeMenuArtwork(characters, guardianForces, palettes, ReadEntry(menu, "icon.sp1"));
    }

    private static PixelImage DecodePortrait(PixelImage[] atlases, ReadOnlySpan<byte> index, int id)
    {
        int count = ReadCount(index);
        if (id >= count)
        {
            throw new InvalidDataException("Original face.sp2 lacks the requested portrait.");
        }
        int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(index[(4 + id * 4)..]));
        if (offset < 4 + count * 4 || offset > index.Length - 16 || BinaryPrimitives.ReadUInt32LittleEndian(index[offset..]) != 1)
        {
            throw new InvalidDataException("Original face.sp2 portrait requires one bounded sprite part.");
        }
        ReadOnlySpan<byte> part = index.Slice(offset + 4, 12);
        int x = part[0];
        int y = part[1] & 127;
        int width = BinaryPrimitives.ReadUInt16LittleEndian(part[4..]);
        int height = BinaryPrimitives.ReadUInt16LittleEndian(part[6..]);
        if (width != 32 || height != 48 || x % 32 != 0 || y is not (0 or 48))
        {
            throw new InvalidDataException("Original portrait does not follow the face.sp2 32x48 cell contract.");
        }
        // Original loader 0x4B3D00 cases 9/10 splits each logical 256x128 sheet into two PC 256x256 textures, at twice the sprite resolution.
        return Crop(atlases[x / 128], x % 128 * 2, y * 2, width * 2, height * 2);
    }

    private static PixelImage DecodeIcon(PixelImage[] palettes, ReadOnlySpan<byte> index, int id, int paletteOffset)
    {
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(index[(4 + id * 4)..]);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(index[(6 + id * 4)..]);
        if (count == 0 || offset < 4 + ReadCount(index) * 4 || count > (index.Length - offset) / 8)
        {
            throw new InvalidDataException("Original menu icon lacks bounded sprite parts.");
        }
        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = int.MinValue;
        int bottom = int.MinValue;
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> part = index.Slice(offset + i * 8, 8);
            int x = unchecked((sbyte)part[5]);
            int y = unchecked((sbyte)part[7]);
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x + part[4]);
            bottom = Math.Max(bottom, y + part[6]);
        }
        PixelImage result = new(right - left, bottom - top);
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> part = index.Slice(offset + i * 8, 8);
            int palette = ((BinaryPrimitives.ReadUInt16LittleEndian(part[2..]) >> 6) & 15) + paletteOffset;
            if ((uint)palette >= palettes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(paletteOffset), "Menu icon CLUT offset leaves the original texture palettes.");
            }
            PixelImage atlas = palettes[palette];
            int x = unchecked((sbyte)part[5]) - left;
            int y = unchecked((sbyte)part[7]) - top;
            int width = part[4];
            int height = part[6];
            if (width == 0 || height == 0 || part[0] + width > atlas.Width || part[1] + height > atlas.Height)
            {
                throw new InvalidDataException("Original menu icon part leaves its atlas.");
            }
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    Rgba32 pixel = atlas[part[0] + column, part[1] + row];
                    if (pixel.A != 0)
                    {
                        result[x + column, y + row] = pixel;
                    }
                }
            }
        }
        return result;
    }

    private static int ReadCount(ReadOnlySpan<byte> index)
    {
        if (index.Length < 4)
        {
            throw new InvalidDataException("Original menu sprite index lacks its count.");
        }
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(index);
        if (count > (index.Length - 4) / 4)
        {
            throw new InvalidDataException("Original menu sprite index table leaves the file.");
        }
        return (int)count;
    }

    private static PixelImage ReadPortraitAtlas(IFlArchive menu, string name)
    {
        TexFile texture = TexFile.Read(ReadEntry(menu, name));
        if (texture.Palettes.Count != 0 || texture.Header.BytesPerPixel != 2 || texture.Header.RedMask != 0x1F || texture.Header.GreenMask != 0x3E0 || texture.Header.BlueMask != 0x7C00 || texture.Header.AlphaMask != 0x8000)
        {
            throw new InvalidDataException($"Original PC portrait atlas {name} must use the native PSX BGR555 direct-color format.");
        }
        PixelImage image = new(texture.Header.Width, texture.Header.Height);
        for (int index = 0; index < image.Pixels.Length; index++)
        {
            // The original opaque menu sprite uses PSX display colors: bit15 is STP, not an ordinary alpha channel.
            image.Pixels[index] = Rgba32.FromPsxBgr555Display(BinaryPrimitives.ReadUInt16LittleEndian(texture.Pixels.AsSpan(index * 2)));
        }
        if (image.Width != 256 || image.Height != 256)
        {
            throw new InvalidDataException($"Original PC portrait atlas {name} must be 256x256.");
        }
        return image;
    }

    private static PixelImage Crop(PixelImage source, int x, int y, int width, int height)
    {
        PixelImage result = new(width, height);
        for (int row = 0; row < height; row++)
        {
            source.Pixels.AsSpan((y + row) * source.Width + x, width).CopyTo(result.Pixels.AsSpan(row * width, width));
        }
        return result;
    }

    private static byte[] ReadEntry(IFlArchive menu, string basename)
    {
        IFlArchiveEntry entry = menu.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(basename, StringComparison.OrdinalIgnoreCase));
        using Stream source = entry.OpenForRead();
        using MemoryStream result = new(checked((int)entry.Size));
        source.CopyTo(result);
        return result.ToArray();
    }
}
