using System.Numerics;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Rendering;

internal readonly record struct BackgroundAtlasRegion(PixelImage Image, Vector2 Start, Vector2 End);

internal static class BackgroundTextureAtlas
{
    internal static Dictionary<PixelImage, BackgroundAtlasRegion> Build(IEnumerable<PixelImage> images)
    {
        // Retain submission ordering while sharing textures, so consecutive tiles can
        // use one hardware draw without reordering translucent primitives.
        Dictionary<PixelImage, BackgroundAtlasRegion> atlas = new(ReferenceEqualityComparer.Instance);
        const int pageSize = 1024;
        List<(PixelImage Image, int X, int Y)> page = [];
        HashSet<PixelImage> packed = new(ReferenceEqualityComparer.Instance);
        int x = 0;
        int y = 0;
        int rowHeight = 0;
        foreach (PixelImage source in images)
        {
            if (!packed.Add(source))
            {
                continue;
            }
            if (source.Width > pageSize || source.Height > pageSize)
            {
                // Large replacement tiles already occupy a complete texture.
                atlas.Add(source, new(source, Vector2.Zero, Vector2.One));
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
                PublishPage(atlas, page, pageSize, y + rowHeight);
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
            PublishPage(atlas, page, pageSize, y + rowHeight);
        }
        return atlas;
    }

    private static void PublishPage(Dictionary<PixelImage, BackgroundAtlasRegion> regions, List<(PixelImage Image, int X, int Y)> page, int width, int height)
    {
        // Avoid nonbinary normalization of point-sampled tile coordinates
        // when the presentation applies a fractional vertical scale.
        height = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)height);
        PixelImage atlas = new(width, height);
        foreach ((PixelImage image, int x, int y) in page)
        {
            for (int row = 0; row < image.Height; row++)
            {
                Array.Copy(image.Pixels, row * image.Width, atlas.Pixels, (y + row) * width + x, image.Width);
            }
            regions.Add(image, new(atlas, new Vector2(x / (float)width, y / (float)height), new Vector2((x + image.Width) / (float)width, (y + image.Height) / (float)height)));
        }
    }
}
