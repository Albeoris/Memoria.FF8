using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    private readonly Dictionary<ushort, ushort> subtractivePaletteOwners = [];
    private readonly Dictionary<(PixelImage Image, FieldBackgroundIndexedSource? Source, ushort Clut), PixelImage> subtractiveImages = [];
    private PixelImage[] subtractivePublishedSources = [];
    private long subtractivePublicationGeneration = -1;

    // NG-092: Native field transitions set the retained alpha-palette shift on entry to285 and clear it on departure.
    private byte SubtractivePaletteShift => this.field.Id == 285 ? (byte)1 : (byte)0;

    private static bool UsesSubtractivePalette(BackgroundTile tile)
    {
        return tile.Blend && tile.BlendMode == MapBlendMode.Subtract;
    }

    private void PrepareSubtractivePalettes(IEnumerable<TileRaster> visible)
    {
        long generation = backgroundPublication?.Generation ?? 0;
        if (subtractivePublicationGeneration != generation)
        {
            for (int index = 0; index < background.Length; index++)
            {
                BackgroundTile tile = background[index].Tile;
                if (index < subtractivePublishedSources.Length && !ReferenceEquals(subtractivePublishedSources[index], tile.ResolvedImage) && tile.IndexedSource is { } source)
                {
                    subtractivePaletteOwners.Remove(source.TexturePage);
                }
            }
            subtractivePublishedSources = background.Select(raster => raster.Tile.ResolvedImage).ToArray();
            subtractiveImages.Clear();
            subtractivePublicationGeneration = generation;
        }
        Dictionary<ushort, ushort> selected = [];
        foreach (TileRaster raster in visible)
        {
            BackgroundTile tile = raster.Tile;
            if (UsesSubtractivePalette(tile) && tile.IndexedSource is { } source)
            {
                // The complete fixed-sprite prepass updates the page CLUT before any missing alpha texture is created.
                selected[source.TexturePage] = tile.PackedPalette;
            }
        }
        foreach ((ushort page, ushort clut) in selected)
        {
            // Changing a primitive's CLUT does not regenerate an already prepared alpha-only page.
            subtractivePaletteOwners.TryAdd(page, clut);
        }
    }

    private ushort SubtractiveClut(BackgroundTile tile)
    {
        return tile.IndexedSource is { } source ? subtractivePaletteOwners.GetValueOrDefault(source.TexturePage, tile.PackedPalette) : (ushort)0;
    }

    private Rgba32 SubtractiveColor(BackgroundTile tile, PixelImage image, int texel)
    {
        ushort word;
        if (tile.IndexedSource is { } source && ReferenceEquals(image, tile.Image))
        {
            word = source.ReadColor(texel, SubtractiveClut(tile));
        }
        else
        {
            Rgba32 color = image.Pixels[texel];
            if (color.A == 0)
            {
                return default;
            }
            // RGB replacements enter the same five-bit alpha preparation; original indexed inputs retain their exact words above.
            word = (ushort)(((color.R * 31 + 127) / 255) | ((color.G * 31 + 127) / 255) << 5 | ((color.B * 31 + 127) / 255) << 10);
        }
        return FieldBackgroundTileDecoder.SubtractiveColor(word, SubtractivePaletteShift);
    }

    private PixelImage SubtractiveImage(BackgroundTile tile)
    {
        PixelImage source = tile.ResolvedImage;
        var key = (source, tile.IndexedSource, SubtractiveClut(tile));
        if (!subtractiveImages.TryGetValue(key, out PixelImage? image))
        {
            image = new PixelImage(source.Width, source.Height);
            for (int texel = 0; texel < image.Pixels.Length; texel++)
            {
                image.Pixels[texel] = SubtractiveColor(tile, source, texel);
            }
            subtractiveImages.Add(key, image);
        }
        return image;
    }

    private IEnumerable<PixelImage> BackgroundAtlasImages()
    {
        foreach (TileRaster raster in background)
        {
            yield return raster.Tile.ResolvedImage;
            if (UsesSubtractivePalette(raster.Tile))
            {
                yield return SubtractiveImage(raster.Tile);
            }
        }
    }

    private void WriteSubtractivePixel(int index, byte alpha)
    {
        int destination = index * 4;
        for (int channel = 0; channel < 3; channel++)
        {
            pixels[destination + channel] = (byte)((pixels[destination + channel] * (255 - alpha) + 127) / 255);
        }
        pixels[destination + 3] = 255;
    }
}
