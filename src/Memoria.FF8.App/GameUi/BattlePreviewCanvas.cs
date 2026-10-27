using SkiaSharp;
using Memoria.FF8.Ui;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>GPU-independent canvas for the same retained controls and text rasterizer used by the live host.</summary>
internal sealed class BattlePreviewCanvas : IUiCanvas, IControllerButtonCanvas, IBattleMessageCanvas, IDisposable
{
    private readonly SKCanvas graphics;
    private readonly Stack<int> clips = [];
    private readonly string gameDirectory;
    private readonly ResourceService? resources;
    private SKBitmap? menuPointer;
    private readonly MenuArtworkProvider artwork;
    private readonly Dictionary<UiImageSource, SKBitmap> images = [];
    private readonly UiTextOverflow textOverflow = new();
    private long artworkGeneration = -1;
    private readonly Dictionary<(int Icon, ControllerPresentation Presentation, int Size), SKBitmap> controllerButtons = [];

    public void BeginFrame(TimeSpan time)
    {
        textOverflow.BeginFrame(time);
    }

    public BattlePreviewCanvas(SKBitmap bitmap, string? gameDirectory = null, ResourceService? resources = null)
    {
        this.gameDirectory = gameDirectory ?? LaunchOptions.ConfiguredGameDirectory;
        this.resources = resources;
        artwork = new MenuArtworkProvider(this.gameDirectory, resources);
        graphics = new SKCanvas(bitmap);
        graphics.Clear(SKColors.Black);
    }

    public void MenuPointer(UiRect bounds)
    {
        if (bounds.IsEmpty)
        {
            return;
        }
        if (menuPointer is null)
        {
            var image = NativeMenuPointer.Read(gameDirectory, resources).Image;
            menuPointer = BitmapFromRgba(image.Pixels.SelectMany(color => new[] { color.R, color.G, color.B, color.A }).ToArray(), image.Width, image.Height);
        }
        float scale = Math.Min(bounds.Width / menuPointer.Width, bounds.Height / menuPointer.Height);
        UiRect destination = new(bounds.X + (bounds.Width - menuPointer.Width * scale) / 2, bounds.Y + (bounds.Height - menuPointer.Height * scale) / 2, menuPointer.Width * scale, menuPointer.Height * scale);
        graphics.DrawBitmap(menuPointer, RectFor(destination));
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
        using SKBitmap paper = BitmapFromRgba(rgba, width, height);
        graphics.DrawBitmap(paper, MathF.Round(bounds.X), MathF.Round(bounds.Y));
    }

    public bool TryDrawControllerButton(int nativeIconId, ControllerPresentation presentation, UiRect bounds, float opacity)
    {
        if (!ControllerVectorArtwork.Supports(nativeIconId, presentation))
        {
            return false;
        }
        if (bounds.IsEmpty)
        {
            return true;
        }
        int size = Math.Clamp((int)MathF.Ceiling(Math.Min(bounds.Width, bounds.Height)), 1, 2048);
        var key = (nativeIconId, presentation, size);
        if (!controllerButtons.TryGetValue(key, out SKBitmap? bitmap))
        {
            bitmap = ControllerVectorArtwork.Render(nativeIconId, presentation, size);
            controllerButtons[key] = bitmap;
        }
        UiRect destination = UiImage.Fit(bounds, size, size);
        using SKPaint paint = new() { Color = SKColors.White.WithAlpha((byte)(Math.Clamp(opacity, 0, 1) * 255)), IsAntialias = true };
        graphics.DrawBitmap(bitmap, RectFor(destination), paint);
        return true;
    }

    public void Image(UiImageSource source, UiRect bounds)
    {
        DrawImage(source, bounds, stretch: false);
    }

    public void ImageStretched(UiImageSource source, UiRect bounds)
    {
        DrawImage(source, bounds, stretch: true);
    }

    private void DrawImage(UiImageSource source, UiRect bounds, bool stretch, float opacity = 1)
    {
        if (artworkGeneration != artwork.Generation)
        {
            foreach (UiImageSource key in images.Keys.Where(MenuArtworkProvider.IsResourceImage).ToArray())
            {
                images[key].Dispose();
                images.Remove(key);
            }
            artworkGeneration = artwork.Generation;
        }
        if (bounds.IsEmpty)
        {
            return;
        }
        if (!images.TryGetValue(source, out SKBitmap? bitmap))
        {
            var image = artwork.Read(source);
            bitmap = BitmapFromRgba(image.Pixels.SelectMany(color => new[] { color.R, color.G, color.B, color.A }).ToArray(), image.Width, image.Height);
            images.Add(source, bitmap);
        }
        UiRect destination = stretch || source.Kind == UiImageKind.Magazine ? bounds : UiImage.Fit(bounds, bitmap.Width, bitmap.Height);
        int previous = graphics.Save();
        try
        {
            graphics.ClipRect(RectFor(bounds), SKClipOperation.Intersect);
            using SKPaint paint = new() { Color = SKColors.White.WithAlpha((byte)(Math.Clamp(opacity, 0, 1) * 255)), IsAntialias = true };
            graphics.DrawBitmap(bitmap, RectFor(destination), paint);
        }
        finally
        {
            graphics.RestoreToCount(previous);
        }
    }

    public void Fill(UiRect bounds, UiColor color)
    {
        if (bounds.IsEmpty)
        {
            return;
        }
        using SKPaint brush = new() { Color = ColorFor(color) };
        graphics.DrawRect(RectFor(bounds), brush);
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color)
    {
        Text(text, fontSize, bounds, color, UiAlignment.Start);
    }

    public UiSize MeasureText(string text, float fontSize, float maximumWidth)
    {
        return MeasureText(text, fontSize, maximumWidth, null);
    }

    public UiSize MeasureText(string text, float fontSize, float maximumWidth, string? fontFamily)
    {
        if (ButtonPromptText.Contains(text))
        {
            return ButtonPromptText.Measure(text, fontSize, maximumWidth, fontFamily);
        }
        Microsoft.Xna.Framework.Vector2 measured = SystemTextLayout.MeasureWrapped(text, fontSize, maximumWidth, fontFamily);
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

        fontSize = UiTextOverflow.FitFontSize(fontSize, bounds.Height, SystemTextLayout.LineHeight(fontSize));
        UiTextViewport layout = textOverflow.Layout(text, fontSize, bounds, null, width =>
        {
            using SKBitmap measured = SystemTextLayout.Rasterize(text, fontSize, width);
            return new UiSize(measured.Width / (float)SystemTextLayout.RasterScale, measured.Height / (float)SystemTextLayout.RasterScale);
        }, UiTextFlow.SingleLine);
        using SKBitmap glyphs = SystemTextLayout.Rasterize(text, fontSize, layout.WrapWidth);
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
                        DrawTinted(glyphs, bounds.X - layout.OffsetX + offsetX * thickness, y + offsetY * thickness, outline);
                    }
                }
            }
            DrawTinted(glyphs, bounds.X - layout.OffsetX, y, color);
        }
        finally
        {
            PopClip();
        }
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
    {
        Text(text, fontSize, bounds, color, null, verticalAlignment);
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? fontFamily)
    {
        Text(text, fontSize, bounds, color, fontFamily, UiAlignment.Start);
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment, UiTextFlow flow)
    {
        Text(text, fontSize, bounds, color, null, verticalAlignment, flow);
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? fontFamily, UiAlignment verticalAlignment, UiTextFlow flow = UiTextFlow.Auto)
    {
        if (bounds.IsEmpty || fontSize <= 0 || string.IsNullOrEmpty(text))
        {
            return;
        }
        fontSize = UiTextOverflow.FitFontSize(fontSize, bounds.Height, SystemTextLayout.LineHeight(fontSize, fontFamily));
        if (ButtonPromptText.Contains(text))
        {
            ButtonPromptText.Draw(this, text, fontSize, bounds, color, fontFamily, verticalAlignment, textOverflow, flow);
            return;
        }
        UiTextViewport layout = textOverflow.Layout(text, fontSize, bounds, fontFamily, width =>
        {
            using SKBitmap measured = SystemTextLayout.Rasterize(text, fontSize, width, fontFamily);
            return new UiSize(measured.Width / (float)SystemTextLayout.RasterScale, measured.Height / (float)SystemTextLayout.RasterScale);
        }, flow);
        using SKBitmap glyphs = SystemTextLayout.Rasterize(text, fontSize, layout.WrapWidth, fontFamily);
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

    private void DrawTinted(SKBitmap glyphs, float x, float y, UiColor color)
    {
        using SKColorFilter filter = SKColorFilter.CreateBlendMode(ColorFor(color), SKBlendMode.SrcIn);
        using SKPaint paint = new() { ColorFilter = filter, IsAntialias = true };
        graphics.DrawBitmap(glyphs, SKRect.Create(x, y, glyphs.Width / (float)SystemTextLayout.RasterScale, glyphs.Height / (float)SystemTextLayout.RasterScale), paint);
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
            using SKPaint pen = new() { Color = ColorFor(color), StrokeWidth = thickness, Style = SKPaintStyle.Stroke };
            graphics.DrawLine(x1, y1, x2, y2, pen);
        }
    }

    public void PushClip(UiRect bounds)
    {
        clips.Push(graphics.Save());
        graphics.ClipRect(RectFor(bounds), SKClipOperation.Intersect);
    }

    public void PopClip()
    {
        if (clips.Count == 0)
        {
            throw new InvalidOperationException("Unbalanced battle preview clipping.");
        }
        graphics.RestoreToCount(clips.Pop());
    }

    public void DrawRgba(byte[] rgba, int width, int height, UiRect bounds)
    {
        using SKBitmap image = BitmapFromRgba(rgba, width, height);
        graphics.DrawBitmap(image, RectFor(bounds));
    }

    internal static SKBitmap BitmapFromRgba(byte[] rgba, int width, int height)
    {
        return PortableRaster.FromRgba(rgba, width, height);
    }

    private static SKRect RectFor(UiRect bounds)
    {
        return SKRect.Create(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private static SKColor ColorFor(UiColor color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    public void Dispose()
    {
        menuPointer?.Dispose();
        foreach (SKBitmap button in controllerButtons.Values)
        {
            button.Dispose();
        }
        controllerButtons.Clear();
        foreach (SKBitmap image in images.Values)
        {
            image.Dispose();
        }
        images.Clear();
        graphics.Dispose();
    }
}
