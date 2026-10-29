using System.Globalization;
using SkiaSharp;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8;

internal static class SystemTextLayout
{
    internal const int RasterScale = 2;

    public static float MeasureAdvance(string text, float size, string? fontFamily = null)
    {
        using SystemTextMeasurer measurer = CreateMeasurer(size, fontFamily);
        return measurer.Measure(text);
    }

    public static float LineHeight(float size, string? fontFamily = null)
    {
        using SystemTextMeasurer measurer = CreateMeasurer(size, fontFamily);
        return measurer.LineHeight;
    }

    public static SystemTextMeasurer CreateMeasurer(float size, string? fontFamily = null) => new(size, fontFamily);

    internal static int RasterMargin(float size) => (int)MathF.Ceiling(size * RasterScale);

    public static Microsoft.Xna.Framework.Vector2 MeasureWrapped(string text, float size, float maximumWidth, string? fontFamily = null)
    {
        using SystemTextMeasurer measurer = CreateMeasurer(size, fontFamily);
        List<string> lines = Wrap(text, measurer.Measure, maximumWidth);
        // Layout needs the advances used by wrapping, including spaces and side bearings.
        // Round outward to the raster grid so scaled bounds still fit the measured line.
        float width = MathF.Ceiling(lines.Max(measurer.Measure) * RasterScale) / RasterScale;
        return new Microsoft.Xna.Framework.Vector2(width, lines.Count * measurer.LineHeight);
    }

    public static SKBitmap Rasterize(string text, float size, float maximumWidth, string? fontFamily = null, bool preserveLineMetrics = false)
    {
        return PortableTextRasterizer.Rasterize(text, size, maximumWidth, fontFamily, preserveLineMetrics);
    }

    internal static PortableTextRasterResult RasterizeMapped(string text, float size, float maximumWidth, string? fontFamily = null, bool preserveLineMetrics = false) => PortableTextRasterizer.RasterizeMapped(text, size, maximumWidth, fontFamily, preserveLineMetrics);

    internal static List<string> Wrap(string text, Func<string, float> measure, float maximumWidth)
    {
        return PortableTextRasterizer.Wrap(text, measure, maximumWidth);
    }

    public static XnaColor[] PremultipliedPixels(SKBitmap bitmap)
    {
        SKColor[] colors = bitmap.Pixels;
        XnaColor[] pixels = new XnaColor[colors.Length];
        for (int index = 0; index < pixels.Length; index++)
        {
            byte alpha = colors[index].Alpha;
            pixels[index] = new XnaColor(alpha, alpha, alpha, alpha);
        }
        return pixels;
    }
}
