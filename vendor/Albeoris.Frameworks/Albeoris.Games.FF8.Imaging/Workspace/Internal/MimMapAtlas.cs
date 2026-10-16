using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging.Mim;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class MimMapAtlas
{
    private const Int32 TexturePageByteWidth = 128;
    private readonly MimFile _mim;
    private readonly WorkspaceImportSpec? _importSpec;
    private readonly Dictionary<Int32, PaletteColorMapper> _paletteMappers = [];

    public MimMapAtlas(MimFile mim, WorkspaceImportSpec? importSpec = null)
    {
        ArgumentNullException.ThrowIfNull(mim);
        _mim = mim;
        _importSpec = importSpec;
    }

    public Rgba32 ReadPixel(MimMapTile tile, Int32 x, Int32 y)
    {
        ValidateLocalCoordinates(x, y);
        return tile.BitsPerPixel switch
        {
            4 => _mim.GetDisplayColor(tile.PaletteId, Read4BitIndex(tile, x, y)),
            8 => _mim.GetDisplayColor(tile.PaletteId, _mim.TextureData[GetByteOffset(tile, x, y)]),
            16 => Rgba32.FromPsxBgr555Display(BinaryPrimitives.ReadUInt16LittleEndian(_mim.TextureData.AsSpan(GetByteOffset(tile, x, y)))),
            _ => default,
        };
    }

    public Byte ReadPaletteIndex(MimMapTile tile, Int32 x, Int32 y)
    {
        ValidateLocalCoordinates(x, y);
        return tile.BitsPerPixel switch
        {
            4 => Read4BitIndex(tile, x, y),
            8 => _mim.TextureData[GetByteOffset(tile, x, y)],
            _ => throw new InvalidDataException($"MAP tile {tile.Index} does not use an indexed palette."),
        };
    }

    public void WritePixel(MimMapTile tile, Int32 x, Int32 y, Rgba32 color)
    {
        ValidateLocalCoordinates(x, y);
        switch (tile.BitsPerPixel)
        {
            case 4:
                Write4BitIndex(tile, x, y, MapColor(tile, color, x, y));
                break;
            case 8:
                _mim.TextureData[GetByteOffset(tile, x, y)] = MapColor(tile, color, x, y);
                break;
            case 16:
                BinaryPrimitives.WriteUInt16LittleEndian(_mim.TextureData.AsSpan(GetByteOffset(tile, x, y)), color.ToPsxBgr555());
                break;
            default:
                throw new InvalidDataException($"MAP tile {tile.Index} uses unsupported {tile.BitsPerPixel}-bpp pixels.");
        }
    }

    private Byte MapColor(MimMapTile tile, Rgba32 color, Int32 x, Int32 y)
    {
        WorkspaceImportSpec spec = _importSpec ?? throw new InvalidOperationException("The atlas is read-only.");
        if (!_paletteMappers.TryGetValue(tile.PaletteId, out PaletteColorMapper? mapper))
        {
            Rgba32[] palette = new Rgba32[MimFile.ColorsPerPalette];
            for (Int32 colorIndex = 0; colorIndex < palette.Length; colorIndex++)
                palette[colorIndex] = _mim.GetDisplayColor(tile.PaletteId, colorIndex);
            _paletteMappers.Add(tile.PaletteId, mapper = new PaletteColorMapper(palette, spec));
        }
        Rgba32 normalized = Rgba32.FromPsxBgr555Display(color.ToPsxBgr555());
        Byte paletteIndex = mapper.Map(normalized, $"MIM/MAP texture pixel ({tile.X + x}, {tile.Y + y}) in palette {tile.PaletteId}");
        if (tile.BitsPerPixel == 4 && paletteIndex > 0x0F)
            throw new InvalidDataException($"MIM/MAP 4-bpp tile {tile.Index} maps a pixel to palette index {paletteIndex}; only indices 0 through 15 are representable.");
        return paletteIndex;
    }

    private Byte Read4BitIndex(MimMapTile tile, Int32 x, Int32 y)
    {
        Int32 sourceX = checked(tile.SourceX + x);
        Byte value = _mim.TextureData[GetByteOffset(tile, x, y)];
        return (Byte)((sourceX & 1) == 0 ? value & 0x0F : value >> 4);
    }

    private void Write4BitIndex(MimMapTile tile, Int32 x, Int32 y, Byte index)
    {
        Int32 sourceX = checked(tile.SourceX + x);
        Int32 offset = GetByteOffset(tile, x, y);
        Byte original = _mim.TextureData[offset];
        _mim.TextureData[offset] = (sourceX & 1) == 0 ? (Byte)((original & 0xF0) | index) : (Byte)((original & 0x0F) | index << 4);
    }

    private Int32 GetByteOffset(MimMapTile tile, Int32 x, Int32 y)
    {
        Int32 sourceX = checked(tile.SourceX + x);
        Int32 sourceY = checked(tile.SourceY + y);
        Int32 pixelOffset = tile.BitsPerPixel switch
        {
            4 => sourceX / 2,
            8 => sourceX,
            16 => checked(sourceX * sizeof(UInt16)),
            _ => throw new InvalidDataException($"MAP tile {tile.Index} uses unsupported {tile.BitsPerPixel}-bpp pixels."),
        };
        Int32 offset = checked(sourceY * _mim.TextureStride + tile.TextureId * TexturePageByteWidth + pixelOffset);
        Int32 byteCount = tile.BitsPerPixel == 16 ? sizeof(UInt16) : sizeof(Byte);
        if (offset < 0 || byteCount > _mim.TextureData.Length - offset)
            throw new InvalidDataException($"MAP tile {tile.Index} addresses bytes outside the MIM texture atlas.");
        return offset;
    }

    private static void ValidateLocalCoordinates(Int32 x, Int32 y)
    {
        if ((UInt32)x >= MimMapTile.PixelSize || (UInt32)y >= MimMapTile.PixelSize)
            throw new ArgumentOutOfRangeException($"Tile coordinates ({x}, {y}) are outside a {MimMapTile.PixelSize}x{MimMapTile.PixelSize} tile.");
    }
}
