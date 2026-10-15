using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
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

    public static Bitmap Rasterize(string text, float size, float maximumWidth, string? fontFamily = null, bool preserveLineMetrics = false)
    {
        using SystemTextMeasurer measurer = CreateMeasurer(size, fontFamily);
        List<string> lines = Wrap(text, measurer.Measure, maximumWidth);
        float width = lines.Max(measurer.Measure) * RasterScale;
        float lineHeight = measurer.LineHeight * RasterScale;
        int margin = RasterMargin(size);
        int bitmapWidth = Math.Max(1, (int)MathF.Ceiling(width) + margin * 2);
        int bitmapHeight = Math.Max(1, (int)MathF.Ceiling(lines.Count * lineHeight) + margin * 2);
        var visual = new DrawingVisual();
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Ideal);
        using (DrawingContext drawing = visual.RenderOpen())
        {
            for (int index = 0; index < lines.Count; index++)
            {
                if (lines[index].Length == 0)
                {
                    continue;
                }
                FormattedText formatted = measurer.Format(lines[index]);
                double lineTop = margin + index * lineHeight + measurer.Baseline * RasterScale - formatted.Baseline;
                drawing.DrawText(formatted, new System.Windows.Point(margin, lineTop));
            }
        }
        var target = new RenderTargetBitmap(bitmapWidth, bitmapHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        using Bitmap bitmap = CopyAlphaBitmap(target);
        if (preserveLineMetrics)
        {
            return (Bitmap)bitmap.Clone();
        }
        byte[] bytes = ReadPixels(bitmap);
        int left = bitmap.Width;
        int top = bitmap.Height;
        int right = -1;
        int bottom = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bytes[(y * bitmap.Width + x) * 4 + 3] == 0)
                {
                    continue;
                }
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }
        return right < left ? new Bitmap(1, 1, PixelFormat.Format32bppArgb) : bitmap.Clone(new Rectangle(left, top, right - left + 1, bottom - top + 1), PixelFormat.Format32bppArgb);
    }

    internal static List<string> Wrap(string text, Func<string, float> measure, float maximumWidth)
    {
        List<string> lines = [];
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length != 0 && measure(candidate) > maximumWidth)
                {
                    lines.Add(line);
                    line = "";
                }
                else if (measure(candidate) <= maximumWidth)
                {
                    line = candidate;
                    continue;
                }
                TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(word);
                while (elements.MoveNext())
                {
                    string character = elements.GetTextElement();
                    if (line.Length != 0 && measure(line + character) > maximumWidth)
                    {
                        lines.Add(line);
                        line = "";
                    }
                    line += character;
                }
            }
            lines.Add(line);
        }
        return lines;
    }

    private static Bitmap CopyAlphaBitmap(BitmapSource source)
    {
        int stride = source.PixelWidth * 4;
        byte[] pixels = new byte[source.PixelHeight * stride];
        source.CopyPixels(pixels, stride, 0);
        // The renderer tints a white alpha mask. Convert WPF premultiplied BGRA to
        // straight-alpha white ARGB without losing antialiasing or fallback glyphs.
        for (int index = 0; index < pixels.Length; index += 4)
        {
            byte white = pixels[index + 3] == 0 ? (byte)0 : (byte)255;
            pixels[index] = white;
            pixels[index + 1] = white;
            pixels[index + 2] = white;
        }
        var bitmap = new Bitmap(source.PixelWidth, source.PixelHeight, PixelFormat.Format32bppArgb);
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(pixels, y * stride, data.Scan0 + y * data.Stride, stride);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }

    public static XnaColor[] PremultipliedPixels(Bitmap bitmap)
    {
        byte[] bytes = ReadPixels(bitmap);
        var pixels = new XnaColor[bitmap.Width * bitmap.Height];
        for (int index = 0; index < pixels.Length; index++)
        {
            byte alpha = bytes[index * 4 + 3];
            pixels[index] = new XnaColor(alpha, alpha, alpha, alpha);
        }
        return pixels;
    }

    private static byte[] ReadPixels(Bitmap bitmap)
    {
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] bytes = new byte[bitmap.Width * bitmap.Height * 4];
            for (int y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, bytes, y * bitmap.Width * 4, bitmap.Width * 4);
            }
            return bytes;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
