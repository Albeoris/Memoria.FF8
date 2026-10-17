using System.Numerics;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    private readonly Dictionary<PixelImage, BackgroundAtlasRegion> backgroundAtlas = new(ReferenceEqualityComparer.Instance);
    private long backgroundAtlasGeneration = -1;

    private BackgroundAtlasRegion GpuBackgroundRegion(PixelImage image)
    {
        long generation = backgroundPublication?.Generation ?? 0;
        if (backgroundAtlasGeneration != generation || !backgroundAtlas.ContainsKey(image))
        {
            BuildBackgroundAtlas();
            backgroundAtlasGeneration = generation;
        }
        return backgroundAtlas[image];
    }

    private void BuildBackgroundAtlas()
    {
        // Retain MAP ordering while sharing textures, so consecutive tiles can
        // use one hardware draw without reordering translucent primitives.
        backgroundAtlas.Clear();
        const int pageSize = 1024;
        List<(PixelImage Image, int X, int Y)> page = [];
        HashSet<PixelImage> packed = new(ReferenceEqualityComparer.Instance);
        int x = 0;
        int y = 0;
        int rowHeight = 0;
        foreach (TileRaster raster in background)
        {
            PixelImage source = raster.Tile.ResolvedImage;
            if (!packed.Add(source))
            {
                continue;
            }
            if (source.Width > pageSize || source.Height > pageSize)
            {
                // Large replacement tiles already occupy a complete texture.
                backgroundAtlas.Add(source, new(source, Vector2.Zero, Vector2.One));
                continue;
            }
            if (x + source.Width > pageSize)
            {
                x = 0;
                y += rowHeight;
                rowHeight = 0;
            }
            if (y + source.Height > pageSize)
            {
                PublishBackgroundAtlasPage(page, pageSize, y + rowHeight);
                page.Clear();
                x = 0;
                y = 0;
                rowHeight = 0;
            }
            page.Add((source, x, y));
            x += source.Width;
            rowHeight = Math.Max(rowHeight, source.Height);
        }
        if (page.Count > 0)
        {
            PublishBackgroundAtlasPage(page, pageSize, y + rowHeight);
        }
    }

    private void PublishBackgroundAtlasPage(List<(PixelImage Image, int X, int Y)> page, int width, int height)
    {
        PixelImage atlas = new(width, height);
        foreach ((PixelImage image, int x, int y) in page)
        {
            for (int row = 0; row < image.Height; row++)
            {
                Array.Copy(image.Pixels, row * image.Width, atlas.Pixels, (y + row) * width + x, image.Width);
            }
            backgroundAtlas.Add(image, new(atlas, new Vector2(x / (float)width, y / (float)height), new Vector2((x + image.Width) / (float)width, (y + image.Height) / (float)height)));
        }
    }

    private readonly record struct BackgroundAtlasRegion(PixelImage Image, Vector2 Start, Vector2 End);
}
