using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Memoria.FF8.Ui;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>GPU-independent canvas for the same retained controls and text rasterizer used by the live host.</summary>
internal sealed class BattlePreviewCanvas : IUiCanvas, IBattleMessageCanvas, IDisposable
{
    private readonly Graphics graphics;
    private readonly Stack<GraphicsState> clips = [];
    private readonly string gameDirectory;
    private Bitmap? menuPointer;
    private readonly MenuArtworkProvider artwork;
    private readonly Dictionary<UiImageSource, Bitmap> images = [];
    private readonly UiTextOverflow textOverflow = new();

    public void BeginFrame(TimeSpan time)
    {
        textOverflow.BeginFrame(time);
    }

    public BattlePreviewCanvas(Bitmap bitmap, string? gameDirectory = null)
    {
        this.gameDirectory = gameDirectory ?? LaunchOptions.ConfiguredGameDirectory;
        artwork = new MenuArtworkProvider(this.gameDirectory);
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

    public void Paper(UiRect bounds, UiColor color)
    {
        if (bounds.IsEmpty)
        {
            return;
        }
        int width = Math.Max(1, (int)MathF.Ceiling(bounds.Width));
        int height = Math.Max(1, (int)MathF.Ceiling(bounds.Height));
        byte[] rgba = new byte[checked(width * height * 4)];
        UiColor[] pixels = UiPaper.CreatePixels(width, height, color);
        for (int index = 0; index < pixels.Length; index++)
        {
            rgba[index * 4] = pixels[index].R;
            rgba[index * 4 + 1] = pixels[index].G;
            rgba[index * 4 + 2] = pixels[index].B;
            rgba[index * 4 + 3] = pixels[index].A;
        }
        using Bitmap paper = BitmapFromRgba(rgba, width, height);
        graphics.DrawImageUnscaled(paper, (int)MathF.Round(bounds.X), (int)MathF.Round(bounds.Y));
    }

    public void Image(UiImageSource source, UiRect bounds)
    {
        DrawImage(source, bounds, stretch: false);
    }

    public void ImageStretched(UiImageSource source, UiRect bounds)
    {
        DrawImage(source, bounds, stretch: true);
    }

    private void DrawImage(UiImageSource source, UiRect bounds, bool stretch)
    {
        if (bounds.IsEmpty)
        {
            return;
        }
        if (!images.TryGetValue(source, out Bitmap? bitmap))
        {
            var image = artwork.Read(source);
            bitmap = BitmapFromRgba(image.Pixels.SelectMany(color => new[] { color.R, color.G, color.B, color.A }).ToArray(), image.Width, image.Height);
            images.Add(source, bitmap);
        }
        UiRect destination = stretch || source.Kind == UiImageKind.Magazine ? bounds : UiImage.Fit(bounds, bitmap.Width, bitmap.Height);
        GraphicsState previous = graphics.Save();
        try
        {
            graphics.SetClip(RectFor(bounds), CombineMode.Intersect);
            graphics.DrawImage(bitmap, RectFor(destination));
        }
        finally
        {
            graphics.Restore(previous);
        }
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

    public UiSize MeasureText(string text, float fontSize, float maximumWidth)
    {
        Microsoft.Xna.Framework.Vector2 measured = SystemTextLayout.MeasureWrapped(text, fontSize, maximumWidth);
        return new UiSize(measured.X, measured.Y);
    }

    public void BattleMessage(DialogueDocument document, float size, UiRect bounds, double seconds)
    {
        using SystemTextMeasurer measure = new(size, null);
        DialogueVisualLine[] lines = document.Pages.SelectMany(page => page.Lines).SelectMany(line => DialogueLayout.Wrap(line, bounds.Width, measure.Measure, false, measure.LineHeight)).ToArray();
        float y = bounds.Y + Math.Max(0, (bounds.Height - lines.Length * measure.LineHeight) / 2);
        PushClip(bounds);
        try
        {
            foreach (DialogueVisualLine line in lines)
            {
                foreach (DialoguePlacedRun placed in line.Runs)
                {
                    Microsoft.Xna.Framework.Color color = placed.Run.Color * (placed.Run.Blink ? DialogueBlink.Opacity(seconds) : 1);
                    float x = Math.Max(0, (bounds.Width - line.Width) / 2) + placed.X;
                    if (placed.Run.IsButton)
                    {
                        ButtonPrompt.DrawCanvas(this, placed.Run.Text, size, new UiRect(bounds.X + x, y, bounds.Width - x, measure.LineHeight), placed.Run.Blink ? DialogueBlink.Opacity(seconds) : 1, null);
                    }
                    else
                    {
                        Text(placed.Run.Text, size, new UiRect(bounds.X + x, y, bounds.Width - x, measure.LineHeight), new UiColor(color.R, color.G, color.B, color.A));
                    }
                }
                y += measure.LineHeight;
            }
        }
        finally
        {
            PopClip();
        }
    }

    public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
    {
        if (bounds.IsEmpty || fontSize <= 0 || string.IsNullOrEmpty(text))
        {
            return;
        }

        using Bitmap glyphs = SystemTextLayout.Rasterize(text, fontSize, bounds.Width);
        float y = bounds.Y + (bounds.Height - glyphs.Height / (float)SystemTextLayout.RasterScale) / 2;
        PushClip(new UiRect(bounds.X - thickness, bounds.Y - thickness, bounds.Width + thickness * 2, bounds.Height + thickness * 2));
        try
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX != 0 || offsetY != 0)
                    {
                        DrawTinted(glyphs, bounds.X + offsetX * thickness, y + offsetY * thickness, outline);
                    }
                }
            }
            DrawTinted(glyphs, bounds.X, y, color);
        }
        finally
        {
            PopClip();
        }
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
    {
        if (bounds.IsEmpty || fontSize <= 0 || string.IsNullOrEmpty(text))
        {
            return;
        }
        UiTextViewport layout = textOverflow.Layout(text, fontSize, bounds, null, width =>
        {
            using Bitmap measured = SystemTextLayout.Rasterize(text, fontSize, width);
            return new UiSize(measured.Width / (float)SystemTextLayout.RasterScale, measured.Height / (float)SystemTextLayout.RasterScale);
        });
        using Bitmap glyphs = SystemTextLayout.Rasterize(text, fontSize, layout.WrapWidth);
        float y = bounds.Y - layout.OffsetY;
        if (verticalAlignment is UiAlignment.Center or UiAlignment.End)
        {
            float height = glyphs.Height / (float)SystemTextLayout.RasterScale;
            y += Math.Max(0, bounds.Height - height) * (verticalAlignment == UiAlignment.Center ? 0.5f : 1);
        }
        PushClip(bounds);
        try
        {
            DrawTinted(glyphs, bounds.X - layout.OffsetX + 1, y + 2, new UiColor(0, 0, 0, (byte)(color.A * 0.75f)));
            DrawTinted(glyphs, bounds.X - layout.OffsetX, y, color);
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
        foreach (Bitmap image in images.Values)
        {
            image.Dispose();
        }
        images.Clear();
        graphics.Dispose();
    }
}
