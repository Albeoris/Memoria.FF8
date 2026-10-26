using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Mim;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Preserves the original CLUT STP bit for textured primitive composition.</summary>
public static class FieldBackgroundTileDecoder
{
    public static bool SupportsAtlas(IReadOnlyList<MapTile> tiles)
    {
        foreach (MapTile tile in tiles)
        {
            int depth = ((tile.PackedTexture | tile.Unknown << 8) >> 7) & 3;
            if (depth == 3 || tile.SourceX % MimFile.TileSize != 0 || tile.SourceY % MimFile.TileSize != 0 || tile.SourceX + MimFile.TileSize > (256 >> depth))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Maps each native source identity to its first MAP-record slot in the packed background atlas.</summary>
    public static IReadOnlyList<TextureSourceAddress> AtlasAddresses(ResourceKey owner, IReadOnlyList<MapTile> tiles)
    {
        if (tiles.Count == 0)
        {
            return [];
        }
        int columns = checked((tiles.Count + 15) / 16);
        int width = checked(columns * MimFile.TileSize);
        bool supported = SupportsAtlas(tiles);
        string subresource = supported ? "background-atlas" : "background-atlas-unsupported-source";
        Dictionary<(int Texture, int X, int Y, int Depth, int Palette), int> first = [];
        TextureSourceAddress[] addresses = new TextureSourceAddress[tiles.Count];
        for (int index = 0; index < tiles.Count; index++)
        {
            MapTile tile = tiles[index];
            int depth = ((tile.PackedTexture | tile.Unknown << 8) >> 7) & 3;
            var identity = (tile.TextureId, tile.SourceX / MimFile.TileSize, tile.SourceY / MimFile.TileSize, depth, depth == 2 ? 0 : tile.PaletteId);
            if (!first.TryGetValue(identity, out int slot))
            {
                slot = index;
                first.Add(identity, slot);
            }
            addresses[index] = new(owner, subresource, 0, slot % columns * MimFile.TileSize, slot / columns * MimFile.TileSize, MimFile.TileSize, MimFile.TileSize, width, 256);
        }
        return addresses;
    }

    public static TextureSourceAddress Address(ResourceKey owner, MapTile tile)
    {
        return new(owner, $"page/{16 + tile.TextureId}", tile.PaletteId, tile.SourceX, tile.SourceY, MimFile.TileSize, MimFile.TileSize, 256, 256);
    }

    public static PixelImage Decode(MimFile mim, MapTile tile)
    {
        PixelImage image = new(MimFile.TileSize, MimFile.TileSize);
        int pageOffset = checked(tile.TextureId * 128);
        int sourceX = tile.Uses8BitIndices ? tile.SourceX : tile.SourceX / 2;
        int sourceWidth = tile.Uses8BitIndices ? MimFile.TileSize : MimFile.TileSize / 2;
        for (int y = 0; y < MimFile.TileSize; y++)
        {
            int source = checked(pageOffset + (tile.SourceY + y) * mim.TextureStride + sourceX);
            if (source < 0 || sourceWidth > mim.TextureData.Length - source)
            {
                throw new InvalidDataException("Original MAP tile addresses outside its MIM atlas.");
            }
            for (int x = 0; x < MimFile.TileSize; x++)
            {
                byte packed = mim.TextureData[source + (tile.Uses8BitIndices ? x : x / 2)];
                int index = tile.Uses8BitIndices ? packed : packed >> (4 * (x & 1)) & 15;
                image[x, y] = mim.GetColor(tile.PaletteId, index);
            }
        }
        return image;
    }
}
