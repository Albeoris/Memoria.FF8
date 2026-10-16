using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging.Mim;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class MimMapLayout
{
    private const UInt16 MapTerminator = 0x7FFF;

    private MimMapLayout(MimMapTileFormat format, List<MimMapTile> tiles)
    {
        Format = format;
        Tiles = tiles;
        if (tiles.Count == 0)
            throw new InvalidDataException("A MIM/MAP workspace requires at least one MAP tile.");
        Int32 minimumX = tiles.Min(tile => (Int32)tile.X);
        Int32 minimumY = tiles.Min(tile => (Int32)tile.Y);
        Int32 maximumX = tiles.Max(tile => (Int32)tile.X);
        Int32 maximumY = tiles.Max(tile => (Int32)tile.Y);
        OffsetX = minimumX < 0 ? -minimumX : 0;
        OffsetY = minimumY < 0 ? -minimumY : 0;
        Width = checked(Math.Abs(minimumX) + Math.Abs(maximumX) + MimMapTile.PixelSize);
        Height = checked(Math.Abs(minimumY) + Math.Abs(maximumY) + MimMapTile.PixelSize);
        if (Width is < 1 or > PixelImage.MaximumDimension || Height is < 1 or > PixelImage.MaximumDimension)
            throw new InvalidDataException($"The MAP canvas size {Width}x{Height} is outside the supported range.");
        Groups = CreateGroups(tiles);
    }

    public MimMapTileFormat Format { get; }
    public List<MimMapTile> Tiles { get; }
    public List<MimMapTextureGroup> Groups { get; }
    public Int32 OffsetX { get; }
    public Int32 OffsetY { get; }
    public Int32 Width { get; }
    public Int32 Height { get; }

    public static MimMapLayout Read(MimFile mim, ReadOnlySpan<Byte> mapContent)
    {
        MimMapTileFormat format = GetFormat(mim, mapContent);
        Int32 tileSize = format == MimMapTileFormat.Logo ? MimMapTile.LogoSize : MimMapTile.Size;
        List<MimMapTile> tiles = [];
        Int32 position = 0;
        while (mapContent.Length - position >= sizeof(UInt16) && BinaryPrimitives.ReadUInt16LittleEndian(mapContent[position..]) != MapTerminator)
        {
            if (mapContent.Length - position < tileSize)
                throw new InvalidDataException($"The MAP ends with an incomplete {tileSize}-byte tile at offset {position}.");
            tiles.Add(MimMapTile.Read(mapContent.Slice(position, tileSize), tiles.Count, format));
            position += tileSize;
        }
        if (mapContent.Length - position < sizeof(UInt16) || BinaryPrimitives.ReadUInt16LittleEndian(mapContent[position..]) != MapTerminator)
            throw new InvalidDataException("The MAP has no 0x7FFF terminator.");
        return new MimMapLayout(format, tiles);
    }

    private static MimMapTileFormat GetFormat(MimFile mim, ReadOnlySpan<Byte> mapContent)
    {
        if (mim.HeaderData.Length != 0)
            return MimMapTileFormat.Standard;
        return HasAlignedTerminator(mapContent, MimMapTile.Size) ? MimMapTileFormat.Alternate : HasAlignedTerminator(mapContent, MimMapTile.LogoSize) ? MimMapTileFormat.Logo : throw new InvalidDataException("The MAP layout does not match the accompanying MIM file.");
    }

    private static Boolean HasAlignedTerminator(ReadOnlySpan<Byte> content, Int32 tileSize)
    {
        for (Int32 position = 0; content.Length - position >= sizeof(UInt16); position += tileSize)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(content[position..]) == MapTerminator)
                return true;
        }
        return false;
    }

    private static List<MimMapTextureGroup> CreateGroups(IReadOnlyList<MimMapTile> tiles)
    {
        Dictionary<(UInt32 BaseId, Int32 X, Int32 Y), Int32> occurrences = [];
        Dictionary<UInt32, List<MimMapTile>> groups = [];
        foreach (MimMapTile tile in tiles)
        {
            UInt32 baseId = CreateBasePupuId(tile);
            (UInt32 BaseId, Int32 X, Int32 Y) key = (baseId, tile.X / MimMapTile.PixelSize, tile.Y / MimMapTile.PixelSize);
            Int32 offset = occurrences.TryGetValue(key, out Int32 previousOffset) ? previousOffset + 1 : 0;
            if (offset > Byte.MaxValue)
                throw new InvalidDataException($"More than 256 MAP tiles share Pupu group 0x{baseId:X8} at ({tile.X}, {tile.Y}).");
            occurrences[key] = offset;
            if (!tile.Draw || tile.BitsPerPixel == 24)
                continue;
            UInt32 pupuId = checked(baseId + (UInt32)offset);
            if (!groups.TryGetValue(pupuId, out List<MimMapTile>? groupTiles))
                groups.Add(pupuId, groupTiles = []);
            groupTiles.Add(tile);
        }
        return groups.OrderBy(pair => pair.Key).Select(pair => new MimMapTextureGroup(pair.Key, pair.Value)).ToList();
    }

    private static UInt32 CreateBasePupuId(MimMapTile tile)
    {
        UInt32 result = (UInt32)(tile.Layer & 0x7F) << 24 | (UInt32)((Byte)tile.BlendMode & 0x07) << 20 | (UInt32)tile.AnimationId << 12 | (UInt32)tile.AnimationState << 4;
        if (tile.X % MimMapTile.PixelSize != 0)
            result |= 0x80000000;
        if (tile.Y % MimMapTile.PixelSize != 0)
            result |= 0x00800000;
        return result;
    }
}

internal sealed record MimMapTextureGroup(UInt32 PupuId, List<MimMapTile> Tiles);
