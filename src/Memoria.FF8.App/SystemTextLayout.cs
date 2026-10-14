using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8;

internal static class SystemTextLayout
{
    internal const int RasterScale = 2;

    public static Bitmap Rasterize(string text, float size, float maximumWidth)
    {
        using var font = new Font("Arial", size * RasterScale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var measure = new Bitmap(1, 1);
        using Graphics measuring = Graphics.FromImage(measure);
        measuring.TextRenderingHint = TextRenderingHint.AntiAlias;
        using StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        float limit = maximumWidth * RasterScale;
        List<string> lines = Wrap(text, candidate => measuring.MeasureString(candidate, font, PointF.Empty, format).Width, limit);
        float width = lines.Max(line => measuring.MeasureString(line, font, PointF.Empty, format).Width);
        float lineHeight = font.GetHeight(measuring);
        // Allow glyph overhang while drawing, then remove transparent bearings from the whole block.
        int margin = (int)MathF.Ceiling(font.Size);
        using var bitmap = new Bitmap(Math.Max(1, (int)MathF.Ceiling(width) + margin * 2), Math.Max(1, (int)MathF.Ceiling(lines.Count * lineHeight) + margin * 2), PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            for (int index = 0; index < lines.Count; index++)
            {
                graphics.DrawString(lines[index], font, Brushes.White, margin, margin + index * lineHeight, format);
            }
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

    private static List<string> Wrap(string text, Func<string, float> measure, float maximumWidth)
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
                foreach (char character in word)
                {
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
