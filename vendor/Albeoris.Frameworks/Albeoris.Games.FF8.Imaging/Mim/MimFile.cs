using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging.Map;

namespace Albeoris.Games.FF8.Imaging.Mim;

/// <summary>An FF8 field texture atlas with 16 PSX BGR555 palettes.</summary>
public sealed class MimFile
{
    public const Int32 HeaderSize = 4 * 1024;
    public const Int32 PaletteCount = 16;
    public const Int32 ColorsPerPalette = 256;
    public const Int32 AtlasStride = 1664;
    public const Int32 AlternateAtlasStride = 1536;
    public const Int32 TileSize = 16;
    public const Int32 AtlasHeight = 256;
    private const Int32 PaletteByteSize = PaletteCount * ColorsPerPalette * sizeof(UInt16);
    private const Int32 AlternateFileSize = PaletteByteSize + AlternateAtlasStride * AtlasHeight;

    public MimFile(Byte[] headerData, UInt16[] colors, Byte[] textureData)
    {
        ArgumentNullException.ThrowIfNull(headerData);
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(textureData);
        if (headerData.Length is not (0 or HeaderSize))
            throw new ArgumentException($"The MIM header must contain either 0 or {HeaderSize} bytes.", nameof(headerData));
        if (colors.Length != PaletteCount * ColorsPerPalette)
            throw new ArgumentException($"The MIM palette area must contain {PaletteCount * ColorsPerPalette} colors.", nameof(colors));

        HeaderData = headerData;
        Colors = colors;
        TextureData = textureData;
    }

    public Byte[] HeaderData { get; }
    public UInt16[] Colors { get; }
    public Byte[] TextureData { get; }
    public Int32 TextureStride => HeaderData.Length == 0 ? AlternateAtlasStride : AtlasStride;

    public static MimFile Read(ReadOnlySpan<Byte> content)
    {
        Int32 headerSize = content.Length == AlternateFileSize ? 0 : HeaderSize;
        Int32 minimumSize = headerSize + PaletteByteSize;
        if (content.Length < minimumSize)
            throw new InvalidDataException($"The MIM file is {content.Length} bytes long; at least {minimumSize} are required.");

        Byte[] header = content[..headerSize].ToArray();
        ReadOnlySpan<Byte> paletteBytes = content.Slice(headerSize, PaletteByteSize);
        UInt16[] colors = MemoryMarshal.Cast<Byte, UInt16>(paletteBytes).ToArray();
        return new MimFile(header, colors, content[minimumSize..].ToArray());
    }

    public Byte[] Write()
    {
        Byte[] result = new Byte[checked(HeaderData.Length + PaletteByteSize + TextureData.Length)];
        HeaderData.CopyTo(result, 0);
        MemoryMarshal.AsBytes(Colors.AsSpan()).CopyTo(result.AsSpan(HeaderData.Length, PaletteByteSize));
        TextureData.CopyTo(result, HeaderData.Length + PaletteByteSize);
        return result;
    }

    /// <summary>Decodes the 16x16 atlas region referenced by a MAP tile.</summary>
    public PixelImage DecodeTile(MapTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        PixelImage image = new(TileSize, TileSize);
        Int32 pageOffset = checked(tile.TextureId * 128);
        Int32 rowOffset = checked(pageOffset + tile.SourceY * TextureStride);
        Int32 sourceX = tile.Uses8BitIndices ? tile.SourceX : tile.SourceX / 2;
        Int32 sourceBytesPerRow = tile.Uses8BitIndices ? TileSize : TileSize / 2;

        for (Int32 y = 0; y < TileSize; y++)
        {
            Int32 source = checked(rowOffset + y * TextureStride + sourceX);
            if (source < 0 || sourceBytesPerRow > TextureData.Length - source)
            {
                throw new InvalidDataException(
                    $"MAP tile ({tile.X}, {tile.Y}) addresses bytes outside the MIM texture atlas.");
            }

            if (tile.Uses8BitIndices)
            {
                for (Int32 x = 0; x < TileSize; x++)
                    image[x, y] = GetDisplayColor(tile.PaletteId, TextureData[source + x]);
            }
            else
            {
                for (Int32 x = 0; x < TileSize; x += 2)
                {
                    Byte indices = TextureData[source + x / 2];
                    image[x, y] = GetDisplayColor(tile.PaletteId, indices & 0x0F);
                    image[x + 1, y] = GetDisplayColor(tile.PaletteId, indices >> 4);
                }
            }
        }

        return image;
    }

    public Rgba32 GetColor(Int32 paletteIndex, Int32 colorIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(paletteIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(paletteIndex, PaletteCount);
        ArgumentOutOfRangeException.ThrowIfNegative(colorIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(colorIndex, ColorsPerPalette);
        return Rgba32.FromPsxBgr555(Colors[paletteIndex * ColorsPerPalette + colorIndex]);
    }

    /// <summary>Gets a palette color as it appears in a field texture. The STP bit is preserved by <see cref="GetColor"/> but does not make the displayed pixel translucent.</summary>
    public Rgba32 GetDisplayColor(Int32 paletteIndex, Int32 colorIndex)
    {
        Rgba32 color = GetColor(paletteIndex, colorIndex);
        return color.A == 0 ? default : new Rgba32(color.R, color.G, color.B);
    }
}
