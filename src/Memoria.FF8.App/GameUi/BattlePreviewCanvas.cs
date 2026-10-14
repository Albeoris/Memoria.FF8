using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>GPU-independent canvas for the same retained controls and text rasterizer used by the live host.</summary>
internal sealed class BattlePreviewCanvas : IUiCanvas, IDisposable
{
    private readonly Graphics graphics;
    private readonly Stack<GraphicsState> clips = [];

    public BattlePreviewCanvas(Bitmap bitmap)
    {
        graphics = Graphics.FromImage(bitmap);
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.Clear(Color.Black);
    }

    public void Fill(UiRect bounds, UiColor color)
    {
        if (bounds.IsEmpty)
        {
            return;
        }
        using SolidBrush brush = new(ColorFor(color));
        graphics.FillRectangle(brush, RectFor(bounds));
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color)
    {
        if (bounds.IsEmpty || fontSize <= 0 || string.IsNullOrEmpty(text))
        {
            return;
        }
        using Bitmap glyphs = SystemTextLayout.Rasterize(text, fontSize, bounds.Width);
        PushClip(bounds);
        try
        {
            DrawTinted(glyphs, bounds.X + 1, bounds.Y + 2, new UiColor(0, 0, 0, (byte)(color.A * 0.75f)));
            DrawTinted(glyphs, bounds.X, bounds.Y, color);
        }
        finally
        {
            PopClip();
        }
    }

    private void DrawTinted(Bitmap glyphs, float x, float y, UiColor color)
    {
        using ImageAttributes attributes = new();
        attributes.SetColorMatrix(new ColorMatrix
        {
            Matrix00 = color.R / 255f,
            Matrix11 = color.G / 255f,
            Matrix22 = color.B / 255f,
            Matrix33 = color.A / 255f,
            Matrix44 = 1
        });
        float width = glyphs.Width / (float)SystemTextLayout.RasterScale;
        float height = glyphs.Height / (float)SystemTextLayout.RasterScale;
        PointF[] destination = [new(x, y), new(x + width, y), new(x, y + height)];
        graphics.DrawImage(glyphs, destination, new RectangleF(0, 0, glyphs.Width, glyphs.Height), GraphicsUnit.Pixel, attributes);
    }

    public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
    {
        if (thickness <= 0)
        {
            return;
        }
        if (y1 == y2)
        {
            Fill(new UiRect(Math.Min(x1, x2), y1, Math.Abs(x2 - x1), thickness), color);
        }
        else if (x1 == x2)
        {
            Fill(new UiRect(x1, Math.Min(y1, y2), thickness, Math.Abs(y2 - y1)), color);
        }
        else
        {
            using Pen pen = new(ColorFor(color), thickness);
            graphics.DrawLine(pen, x1, y1, x2, y2);
        }
    }

    public void PushClip(UiRect bounds)
    {
        clips.Push(graphics.Save());
        graphics.SetClip(RectFor(bounds), CombineMode.Intersect);
    }

    public void PopClip()
    {
        if (clips.Count == 0)
        {
            throw new InvalidOperationException("Unbalanced battle preview clipping.");
        }
        graphics.Restore(clips.Pop());
    }

    public void DrawImage(PixelImage image, UiRect bounds)
    {
        byte[] pixels = new byte[checked(image.Width * image.Height * 4)];
        for (int index = 0; index < image.Pixels.Length; index++)
        {
            Rgba32 color = image.Pixels[index];
            pixels[index * 4] = color.R;
            pixels[index * 4 + 1] = color.G;
            pixels[index * 4 + 2] = color.B;
            pixels[index * 4 + 3] = color.A;
        }
        DrawRgba(pixels, image.Width, image.Height, bounds);
    }

    public void DrawRgba(byte[] rgba, int width, int height, UiRect bounds)
    {
        using Bitmap image = BitmapFromRgba(rgba, width, height);
        graphics.DrawImage(image, RectFor(bounds));
    }

    internal static Bitmap BitmapFromRgba(byte[] rgba, int width, int height)
    {
        if (rgba.Length != checked(width * height * 4))
        {
            throw new ArgumentException("RGBA buffer dimensions do not match.", nameof(rgba));
        }
        Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
        BitmapData locked = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] row = new byte[checked(width * 4)];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int source = (y * width + x) * 4;
                    int target = x * 4;
                    row[target] = rgba[source + 2];
                    row[target + 1] = rgba[source + 1];
                    row[target + 2] = rgba[source];
                    row[target + 3] = rgba[source + 3];
                }
                Marshal.Copy(row, 0, IntPtr.Add(locked.Scan0, y * locked.Stride), row.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(locked);
        }
        return bitmap;
    }

    private static RectangleF RectFor(UiRect bounds)
    {
        return new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private static Color ColorFor(UiColor color)
    {
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    public void Dispose()
    {
        graphics.Dispose();
    }
}
