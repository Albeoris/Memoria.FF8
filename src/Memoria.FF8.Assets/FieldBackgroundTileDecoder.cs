using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Mim;

namespace Memoria.FF8.Assets;

/// <summary>Preserves the original CLUT STP bit for textured primitive composition.</summary>
public static class FieldBackgroundTileDecoder
{
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
