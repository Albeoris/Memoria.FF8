using Memoria.FF8.Resources;
using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tex;
using Memoria.FF8.Runtime;

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

    private NativeMenuArtwork(PixelImage[] characters, PixelImage[] guardianForces, PixelImage angeloPortrait, PixelImage[] iconPalettes, byte[] iconIndex)
    {
        this.characters = characters;
        this.guardianForces = guardianForces;
        AngeloPortrait = angeloPortrait;
        this.iconPalettes = iconPalettes;
        this.iconIndex = iconIndex;
        IconCount = ReadCount(iconIndex);
    }

    public int IconCount { get; }

    public bool HasControllerArtwork { get; private init; }

    public PixelImage AngeloPortrait { get; }

    /// <summary>Original controller symbols used in printed Duel combinations, independent of current key bindings.</summary>
    public PixelImage GetOriginalButtonIcon(int buttonBit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(buttonBit);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(buttonBit, 16);
        return DecodeIcon(iconPalettes, iconIndex, 128 + buttonBit, 0);
    }

    /// <summary>Reads a physical controller glyph after the caller has resolved the current binding.</summary>
    public PixelImage GetControllerButtonIcon(int nativeIconId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nativeIconId, 128);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nativeIconId, 139);
        if (!icons.TryGetValue((nativeIconId, 0), out PixelImage? image))
        {
            image = DecodeIcon(iconPalettes, iconIndex, nativeIconId, 0);
            icons.Add((nativeIconId, 0), image);
        }
        return image;
    }
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

    public static NativeMenuArtwork Load(string gameDirectory, ResourceService? resources = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        using IResourceSource provider = GameDataSourceFactory.Open(gameDirectory);
        resources ??= new(provider);
        byte[] faces = resources.Read(new ResourceKey("menu/face.sp2"));
        NativeMenuArtworkDataReader.ValidateFaceDirectory(faces);
        byte[] icons = resources.Read(new ResourceKey("menu/icon.sp1"));
        NativeMenuArtworkDataReader.ValidateIconDirectory(icons);
        PixelImage[] characterAtlases = [ReadPortraitAtlas(provider, resources, "Face_b00.TEX"), ReadPortraitAtlas(provider, resources, "Face_b01.TEX")];
        PixelImage[] guardianForceAtlases = [ReadPortraitAtlas(provider, resources, "Gf_big00.TEX"), ReadPortraitAtlas(provider, resources, "Gf_big01.TEX")];
        PixelImage[] characters = Enumerable.Range(0, CharacterCount).Select(id => DecodePortrait(characterAtlases, faces, id)).ToArray();
        PixelImage[] guardianForces = Enumerable.Range(0, GuardianForceCount).Select(id => DecodePortrait(guardianForceAtlases, faces, 32 + id)).ToArray();
        byte[] iconTexture = resources.Read(new ResourceKey("menu/icon.tex"));
        NativeMenuArtworkDataReader.ValidateTexture(new ResourceKey("menu/icon.tex"), iconTexture);
        TexFile texture = TexFile.Read(iconTexture);
        PixelImage[] originals = Enumerable.Range(0, texture.Palettes.Count).Select(texture.Decode).ToArray();
        PixelImage[] palettes = Enumerable.Range(0, texture.Palettes.Count).Select(palette => NativeArtworkTexture.Resolve(provider, new ResourceKey("menu/icon.tex"), originals[palette], palette, resources: resources)).ToArray();
        return new NativeMenuArtwork(characters, guardianForces, DecodePortrait(characterAtlases, faces, 15), palettes, icons) { HasControllerArtwork = resources.Source.Layouts.Contains("classic-ffnx", StringComparer.Ordinal) && !ReferenceEquals(originals[4], palettes[4]) };
    }

    /// <summary>Formats the original current ICON.SP1 parts for the separate native PC UI-immediate path.</summary>
    public ImmutableArray<byte> EncodeNativeIconPackets(int index, int x, int y, uint color, int paletteOffset = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, IconCount);
        if (index is >= 128 and < 140)
        {
            throw new NotSupportedException("Native4B76A0 icons128..139 require original4A2F80 current input bindings.");
        }
        return NativeMenuIconPackets.Encode4B76A0(ReadIconParts(iconIndex, index), x, y, color, paletteOffset);
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
        PixelImage atlas = atlases[x / 128];
        int scale = NativeArtworkTexture.Scale(atlas, 256, 256);
        return Crop(atlas, x % 128 * 2 * scale, y * 2 * scale, width * 2 * scale, height * 2 * scale);
    }

    private static PixelImage DecodeIcon(PixelImage[] palettes, ReadOnlySpan<byte> index, int id, int paletteOffset)
    {
        ReadOnlySpan<byte> parts = ReadIconParts(index, id);
        int count = parts.Length / 8;
        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = int.MinValue;
        int bottom = int.MinValue;
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> part = parts.Slice(i * 8, 8);
            int x = unchecked((sbyte)part[5]);
            int y = unchecked((sbyte)part[7]);
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x + part[4]);
            bottom = Math.Max(bottom, y + part[6]);
        }
        int scale = palettes.Max(atlas => NativeArtworkTexture.Scale(atlas, 256, 256));
        PixelImage result = new((right - left) * scale, (bottom - top) * scale);
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> part = parts.Slice(i * 8, 8);
            int palette = ((BinaryPrimitives.ReadUInt16LittleEndian(part[2..]) >> 6) & 15) + paletteOffset;
            if ((uint)palette >= palettes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(paletteOffset), "Menu icon CLUT offset leaves the original texture palettes.");
            }
            PixelImage atlas = palettes[palette];
            int atlasScale = NativeArtworkTexture.Scale(atlas, 256, 256);
            int x = (unchecked((sbyte)part[5]) - left) * scale;
            int y = (unchecked((sbyte)part[7]) - top) * scale;
            int width = part[4] * scale;
            int height = part[6] * scale;
            if (width == 0 || height == 0 || (part[0] + part[4]) * atlasScale > atlas.Width || (part[1] + part[6]) * atlasScale > atlas.Height)
            {
                throw new InvalidDataException("Original menu icon part leaves its atlas.");
            }
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    Rgba32 pixel = atlas[part[0] * atlasScale + column * atlasScale / scale, part[1] * atlasScale + row * atlasScale / scale];
                    if (pixel.A != 0)
                    {
                        result[x + column, y + row] = pixel;
                    }
                }
            }
        }
        return result;
    }

    private static ReadOnlySpan<byte> ReadIconParts(ReadOnlySpan<byte> index, int id)
    {
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(index[(4 + id * 4)..]);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(index[(6 + id * 4)..]);
        if (count == 0 || offset < 4 + ReadCount(index) * 4 || count > (index.Length - offset) / 8)
        {
            throw new InvalidDataException("Original menu icon lacks bounded sprite parts.");
        }
        return index.Slice(offset, count * 8);
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

    private static PixelImage ReadPortraitAtlas(IResourceSource provider, ResourceService resources, string name)
    {
        byte[] bytes = resources.Read(new ResourceKey("menu/" + name));
        NativeMenuArtworkDataReader.ValidateTexture(new ResourceKey("menu/" + name), bytes);
        TexFile texture = TexFile.Read(bytes);
        PixelImage image = new(texture.Header.Width, texture.Header.Height);
        for (int index = 0; index < image.Pixels.Length; index++)
        {
            // The original opaque menu sprite uses PSX display colors: bit15 is STP, not an ordinary alpha channel.
            image.Pixels[index] = Rgba32.FromPsxBgr555Display(BinaryPrimitives.ReadUInt16LittleEndian(texture.Pixels.AsSpan(index * 2)));
        }
        return NativeArtworkTexture.Resolve(provider, new ResourceKey("menu/" + name), image, resources: resources);
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
}
