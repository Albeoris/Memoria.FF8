using System.Globalization;
using SkiaSharp;
using System.Numerics;

namespace Memoria.FF8;

internal static class PortableTextRasterizer
{
    internal const int RasterScale = 2;

    public static float MeasureAdvance(string text, float size, string? fontFamily = null)
    {
        using PortableTextShaper measurer = CreateMeasurer(size, fontFamily);
        return measurer.Measure(text);
    }

    public static float LineHeight(float size, string? fontFamily = null)
    {
        using PortableTextShaper measurer = CreateMeasurer(size, fontFamily);
        return measurer.LineHeight;
    }

    public static PortableTextShaper CreateMeasurer(float size, string? fontFamily = null) => new(size, fontFamily);

    internal static int RasterMargin(float size) => (int)MathF.Ceiling(size * RasterScale);

    public static Vector2 MeasureWrapped(string text, float size, float maximumWidth, string? fontFamily = null)
    {
        using PortableTextShaper measurer = CreateMeasurer(size, fontFamily);
        List<string> lines = Wrap(text, measurer.Measure, maximumWidth);
        // Layout needs the advances used by wrapping, including spaces and side bearings.
        // Round outward to the raster grid so scaled bounds still fit the measured line.
        float width = MathF.Ceiling(lines.Max(measurer.Measure) * RasterScale) / RasterScale;
        return new Vector2(width, lines.Count * measurer.LineHeight);
    }

    public static SKBitmap Rasterize(string text, float size, float maximumWidth, string? fontFamily = null, bool preserveLineMetrics = false)
    {
        return RasterizeDetailed(text, size, maximumWidth, fontFamily, preserveLineMetrics, false).Bitmap;
    }

    internal static PortableTextRasterResult RasterizeMapped(string text, float size, float maximumWidth, string? fontFamily = null, bool preserveLineMetrics = false) => RasterizeDetailed(text, size, maximumWidth, fontFamily, preserveLineMetrics, true);

    private static PortableTextRasterResult RasterizeDetailed(string text, float size, float maximumWidth, string? fontFamily, bool preserveLineMetrics, bool sourceMap)
    {
        using PortableTextShaper measurer = CreateMeasurer(size, fontFamily);
        List<PortableTextWrappedLine> mapped = WrapCore(text, measurer.Measure, maximumWidth, sourceMap);
        List<string> lines = mapped.Select(line => line.Text).ToList();
        List<PortableTextCluster> clusters = [];
        float width = lines.Max(measurer.Measure) * RasterScale;
        float lineHeight = measurer.LineHeight * RasterScale;
        int margin = RasterMargin(size);
        int bitmapWidth = Math.Max(1, (int)MathF.Ceiling(width) + margin * 2);
        int bitmapHeight = Math.Max(1, (int)MathF.Ceiling(lines.Count * lineHeight) + margin * 2);
        using SKBitmap bitmap = new(bitmapWidth, bitmapHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using (SKCanvas canvas = new(bitmap))
        using (SKPaint paint = new() { Color = SKColors.White, IsAntialias = true })
        {
            canvas.Clear(SKColors.Transparent);
            for (int index = 0; index < lines.Count; index++)
            {
                measurer.Draw(canvas, lines[index], margin, margin + index * lineHeight + measurer.Baseline * RasterScale, paint);
                if (sourceMap)
                {
                    foreach (PortableShapedCluster cluster in measurer.SourceClusters(lines[index]))
                    {
                        SKRect bounds = cluster.Bounds;
                        bounds.Offset(margin / (float)RasterScale, margin / (float)RasterScale + index * measurer.LineHeight + measurer.Baseline);
                        clusters.Add(new(mapped[index].SourcePositions.Skip(cluster.Start).Take(cluster.Length).ToArray(), bounds, index));
                    }
                }
            }
        }
        if (preserveLineMetrics)
        {
            return new(bitmap.Copy(), clusters);
        }
        SKColor[] pixels = bitmap.Pixels;
        int left = bitmap.Width;
        int top = bitmap.Height;
        int right = -1;
        int bottom = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (pixels[y * bitmap.Width + x].Alpha == 0)
                {
                    continue;
                }
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }
        if (right < left)
        {
            return new(new SKBitmap(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul), []);
        }
        // Cropping glyph bearings must retain authored indentation, including a space
        // at the start of a separately colored run.
        float leadingAdvance = lines.Where(line => !string.IsNullOrWhiteSpace(line)).Min(line => measurer.Measure(line[..(line.Length - line.TrimStart().Length)]));
        left = Math.Max(0, left - (int)MathF.Round(leadingAdvance * RasterScale));
        SKBitmap cropped = new(right - left + 1, bottom - top + 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKCanvas crop = new(cropped);
        crop.Clear(SKColors.Transparent);
        crop.DrawBitmap(bitmap, -left, -top);
        return new(cropped, clusters.Select(cluster =>
        {
            SKRect bounds = cluster.Bounds;
            bounds.Offset(-left / (float)RasterScale, -top / (float)RasterScale);
            return cluster with { Bounds = bounds };
        }).ToArray());
    }

    internal static List<string> Wrap(string text, Func<string, float> measure, float maximumWidth)
    {
        return WrapCore(text, measure, maximumWidth, false).Select(line => line.Text).ToList();
    }

    internal static List<PortableTextWrappedLine> WrapMapped(string text, Func<string, float> measure, float maximumWidth) => WrapCore(text, measure, maximumWidth, true);

    private static List<PortableTextWrappedLine> WrapCore(string text, Func<string, float> measure, float maximumWidth, bool sourceMap)
    {
        List<PortableTextWrappedLine> lines = [];
        string normalized = text.Replace("\r", "");
        int[] positions = sourceMap ? Enumerable.Range(0, text.Length).Where(index => text[index] != '\r').ToArray() : [];
        int paragraphStart = 0;
        foreach (string paragraph in normalized.Split('\n'))
        {
            string line = "";
            List<int>? sourcePositions = sourceMap ? [] : null;
            bool needsSeparator = false;
            int wordStart = paragraphStart;
            void FinishLine()
            {
                lines.Add(new(line, sourcePositions?.ToArray() ?? []));
                sourcePositions?.Clear();
            }
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = needsSeparator ? line + " " + word : word;
                bool separator = needsSeparator;
                needsSeparator = true;
                if (line.Length != 0 && measure(candidate) > maximumWidth)
                {
                    FinishLine();
                    line = "";
                    needsSeparator = false;
                }
                else if (measure(candidate) <= maximumWidth)
                {
                    line = candidate;
                    if (sourcePositions is not null)
                    {
                        if (separator)
                        {
                            sourcePositions.Add(positions[wordStart - 1]);
                        }
                        sourcePositions.AddRange(positions.AsSpan(wordStart, word.Length).ToArray());
                    }
                    wordStart += word.Length + 1;
                    continue;
                }
                TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(word);
                while (elements.MoveNext())
                {
                    string character = elements.GetTextElement();
                    if (line.Length != 0 && measure(line + character) > maximumWidth)
                    {
                        FinishLine();
                        line = "";
                    }
                    line += character;
                    if (sourcePositions is not null)
                    {
                        sourcePositions.AddRange(positions.AsSpan(wordStart + elements.ElementIndex, character.Length).ToArray());
                    }
                    needsSeparator = true;
                }
                wordStart += word.Length + 1;
            }
            FinishLine();
            paragraphStart += paragraph.Length + 1;
        }
        return lines;
    }

}
