using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Memoria.FF8.Ui;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>GPU-independent canvas for the same retained controls and text rasterizer used by the live host.</summary>
internal sealed class BattlePreviewCanvas : IUiCanvas, IDisposable
{
    private readonly Graphics graphics;
    private readonly Stack<GraphicsState> clips = [];
    private readonly string gameDirectory;
    private Bitmap? menuPointer;

    public BattlePreviewCanvas(Bitmap bitmap, string gameDirectory = LaunchOptions.DefaultGameDirectory)
    {
        this.gameDirectory = gameDirectory;
        graphics = Graphics.FromImage(bitmap);
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.Clear(Color.Black);
    }

    public void MenuPointer(UiRect bounds)
    {
        if (bounds.IsEmpty)
        {
            return;
        }
        if (menuPointer is null)
        {
            var image = NativeMenuPointer.Read(gameDirectory).Image;
            menuPointer = BitmapFromRgba(image.Pixels.SelectMany(color => new[] { color.R, color.G, color.B, color.A }).ToArray(), image.Width, image.Height);
        }
        float scale = Math.Min(bounds.Width / menuPointer.Width, bounds.Height / menuPointer.Height);
        UiRect destination = new(bounds.X + (bounds.Width - menuPointer.Width * scale) / 2, bounds.Y + (bounds.Height - menuPointer.Height * scale) / 2, menuPointer.Width * scale, menuPointer.Height * scale);
        graphics.DrawImage(menuPointer, RectFor(destination));
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
        Text(text, fontSize, bounds, color, UiAlignment.Start);
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
    {
        if (bounds.IsEmpty || fontSize <= 0 || string.IsNullOrEmpty(text))
        {
            return;
        }
        using Bitmap glyphs = SystemTextLayout.Rasterize(text, fontSize, bounds.Width);
        float y = bounds.Y;
        if (verticalAlignment is UiAlignment.Center or UiAlignment.End)
        {
            float height = glyphs.Height / (float)SystemTextLayout.RasterScale;
            y += (bounds.Height - height) * (verticalAlignment == UiAlignment.Center ? 0.5f : 1);
        }
        PushClip(bounds);
        try
        {
            DrawTinted(glyphs, bounds.X + 1, y + 2, new UiColor(0, 0, 0, (byte)(color.A * 0.75f)));
            DrawTinted(glyphs, bounds.X, y, color);
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
        menuPointer?.Dispose();
        graphics.Dispose();
    }
}
