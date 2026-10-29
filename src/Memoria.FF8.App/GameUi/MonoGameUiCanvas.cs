using Memoria.FF8.Ui;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.GameUi;

/// <summary>Draws retained controls, system text and the original menu pointer.</summary>
internal sealed partial class MonoGameUiCanvas(GraphicsDevice device, SpriteBatch batch, Texture2D pixel, SystemTextRenderer text, string gameDirectory, ResourceService? resources = null) : IUiCanvas, IControllerButtonCanvas, IBattleMessageCanvas, IDisposable
{
    private readonly Stack<Rectangle> clips = new();
    private Texture2D? menuPointer;
    private readonly MenuArtworkProvider artwork = new(gameDirectory, resources);
    private readonly Dictionary<UiImageSource, Texture2D> images = [];
    private string? cardFontFamily;
    private readonly UiTextOverflow textOverflow = new();
    private long artworkGeneration = -1;
    private readonly Dictionary<(int Icon, ControllerPresentation Presentation, int Size), Texture2D> controllerButtons = [];
    private readonly Dictionary<(string Text, float Size, float Width, string Family), UiSize> textMeasurements = [];

    public void BeginFrame(TimeSpan time)
    {
        textOverflow.BeginFrame(time);
        PumpModPreview();
    }

    public void PrepareTripleTriad(TripleTriadController controller)
    {
        PrepareTripleTriadImages(controller.Available.Concat(controller.RecentDecks.SelectMany(deck => deck)).Concat(controller.Match?.InitialHands.SelectMany(hand => hand) ?? []));
        foreach (int id in controller.RecentDecks.SelectMany(deck => deck).Distinct())
        {
            _ = GetImage(new UiImageSource(UiImageKind.CardFaceGrayscale, id));
        }
        float scale = Math.Min(device.Viewport.Width / 960f, device.Viewport.Height / 672f);
        foreach (int id in controller.Available)
        {
            string name = controller.Name(id);
            text.Preload(name, 27 * scale, 280 * scale);
            for (int selected = 0; selected <= Math.Min(5, controller.Remaining(id)); selected++)
            {
                text.Preload((controller.Remaining(id) - selected).ToString(), 27 * scale, 50 * scale);
            }
        }
    }

    public void PrepareTripleTriadImages(IEnumerable<int> cardIds)
    {
        _ = GetImage(new UiImageSource(UiImageKind.TripleTriadBackground, 0));
        _ = GetImage(new UiImageSource(UiImageKind.TripleTriadBoardBackground, 0));
        _ = GetImage(new UiImageSource(UiImageKind.Icon, 215));
        foreach (int id in cardIds.Distinct())
        {
            _ = GetImage(new UiImageSource(UiImageKind.CardFace, id, 0));
            _ = GetImage(new UiImageSource(UiImageKind.CardFace, id, 1));
        }
        for (int outcome = 0; outcome < 3; outcome++)
        {
            _ = GetImage(new UiImageSource(UiImageKind.TripleTriadOutcome, outcome));
        }
        _ = GetImage(new UiImageSource(UiImageKind.CardBack, 0));
        foreach (var element in Enum.GetValues<Memoria.FF8.Runtime.TripleTriadElement>().Where(element => element != Memoria.FF8.Runtime.TripleTriadElement.None))
        {
            _ = GetImage(new UiImageSource(UiImageKind.CardElement, (int)element));
        }
    }

    public void PreloadImages(IEnumerable<UiImageSource> sources)
    {
        foreach (UiImageSource source in sources)
        {
            _ = GetImage(source);
        }
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
        if (!controllerButtons.TryGetValue(key, out Texture2D? texture))
        {
            using SkiaSharp.SKBitmap bitmap = ControllerVectorArtwork.Render(nativeIconId, presentation, size);
            texture = new Texture2D(device, size, size);
            Color[] colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    SkiaSharp.SKColor color = bitmap.GetPixel(x, y);
                    colors[y * size + x] = Color.FromNonPremultiplied(color.Red, color.Green, color.Blue, color.Alpha);
                }
            }
            texture.SetData(colors);
            controllerButtons[key] = texture;
        }
        UiRect destination = UiImage.Fit(bounds, size, size);
        Rectangle target = RectangleFor(destination);
        Rectangle clipped = Rectangle.Intersect(target, CurrentClip);
        if (clipped.Width > 0 && clipped.Height > 0)
        {
            Rectangle source = new((clipped.X - target.X) * size / target.Width, (clipped.Y - target.Y) * size / target.Height, Math.Max(1, clipped.Width * size / target.Width), Math.Max(1, clipped.Height * size / target.Height));
            batch.Draw(texture, clipped, source, Color.White * opacity);
        }
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

    private Texture2D GetImage(UiImageSource source)
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
        if (source.Kind is UiImageKind.CardFace or UiImageKind.CardFaceGrayscale && cardFontFamily != text.FontFamily)
        {
            foreach (UiImageSource key in images.Keys.Where(key => key.Kind is UiImageKind.CardFace or UiImageKind.CardFaceGrayscale).ToArray())
            {
                images[key].Dispose();
                images.Remove(key);
            }
            cardFontFamily = text.FontFamily;
        }
        if (!images.TryGetValue(source, out Texture2D? texture))
        {
            var image = artwork.Read(source, text.FontFamily);
            texture = new Texture2D(device, image.Width, image.Height);
            try
            {
                texture.SetData(image.Pixels.Select(color => Color.FromNonPremultiplied(color.R, color.G, color.B, color.A)).ToArray());
                images.Add(source, texture);
            }
            catch
            {
                texture.Dispose();
                throw;
            }
        }
        return texture;
    }

    private void DrawImage(UiImageSource source, UiRect bounds, bool stretch, float opacity = 1)
    {
        opacity *= source.Opacity / 255f;
        source = source with { Opacity = 255 };
        if (bounds.IsEmpty)
        {
            return;
        }
        Texture2D texture = GetImage(source);
        UiRect destination = stretch || source.Kind == UiImageKind.Magazine ? bounds : UiImage.Fit(bounds, texture.Width, texture.Height);
        Rectangle clip = Rectangle.Intersect(RectangleFor(bounds), CurrentClip);
        Vector2 scale = new(destination.Width / texture.Width, destination.Height / texture.Height);
        int left = Math.Clamp((int)MathF.Ceiling((clip.Left - destination.X) / scale.X), 0, texture.Width);
        int top = Math.Clamp((int)MathF.Ceiling((clip.Top - destination.Y) / scale.Y), 0, texture.Height);
        int right = Math.Clamp((int)MathF.Floor((clip.Right - destination.X) / scale.X), left, texture.Width);
        int bottom = Math.Clamp((int)MathF.Floor((clip.Bottom - destination.Y) / scale.Y), top, texture.Height);
        if (right > left && bottom > top)
        {
            Rectangle sourceBounds = new(left, top, right - left, bottom - top);
            Vector2 position = new(destination.X + left * scale.X, destination.Y + top * scale.Y);
            batch.Draw(texture, position, sourceBounds, Color.White * opacity, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
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
            menuPointer = new Texture2D(device, image.Width, image.Height);
            menuPointer.SetData(image.Pixels.Select(color => Color.FromNonPremultiplied(color.R, color.G, color.B, color.A)).ToArray());
        }
        float scale = Math.Min(bounds.Width / menuPointer.Width, bounds.Height / menuPointer.Height);
        Vector2 position = new(bounds.X + (bounds.Width - menuPointer.Width * scale) / 2, bounds.Y + (bounds.Height - menuPointer.Height * scale) / 2);
        SystemTextRenderer.DrawClipped(batch, menuPointer, position, Color.White, CurrentClip, scale);
    }

    public void Fill(UiRect bounds, UiColor color)
    {
        Rectangle rectangle = Rectangle.Intersect(FillRectangleFor(bounds), CurrentClip);
        if (rectangle.Width > 0 && rectangle.Height > 0)
        {
            batch.Draw(pixel, rectangle, ColorFor(color));
        }
    }

    public void Text(string value, float size, UiRect bounds, UiColor color)
    {
        Text(value, size, bounds, color, null);
    }

    public UiSize MeasureText(string value, float size, float maximumWidth)
    {
        return MeasureText(value, size, maximumWidth, null);
    }

    public UiSize MeasureText(string value, float size, float maximumWidth, string? fontFamily)
    {
        fontFamily ??= text.FontFamily;
        var key = (value, size, maximumWidth, fontFamily);
        if (textMeasurements.TryGetValue(key, out UiSize result))
        {
            return result;
        }
        if (ButtonPromptText.Contains(value))
        {
            result = ButtonPromptText.Measure(value, size, maximumWidth, fontFamily);
        }
        else
        {
            Vector2 measured = SystemTextLayout.MeasureWrapped(value, size, maximumWidth, fontFamily);
            result = new UiSize(measured.X, measured.Y);
        }
        if (textMeasurements.Count >= 1024)
        {
            textMeasurements.Clear();
        }
        textMeasurements[key] = result;
        return result;
    }

    public void OutlinedText(string value, float size, UiRect bounds, UiColor color, UiColor outline, float thickness)
    {
        if (bounds.IsEmpty || size <= 0 || string.IsNullOrEmpty(value))
        {
            return;
        }

        size = UiTextOverflow.FitFontSize(size, bounds.Height, MeasureText("Mg", size, float.PositiveInfinity).Height);
        UiTextViewport layout = textOverflow.Layout(value, size, bounds, text.FontFamily, width =>
        {
            Vector2 measured = text.MeasureWrapped(value, size, width);
            return new UiSize(measured.X, measured.Y);
        }, UiTextFlow.SingleLine);
        Rectangle clip = Rectangle.Intersect(RectangleFor(new UiRect(bounds.X - thickness, bounds.Y - thickness, bounds.Width + thickness * 2, bounds.Height + thickness * 2)), CurrentClip);
        Inspect(value, new Vector2(bounds.X - layout.OffsetX, bounds.Y + bounds.Height / 2), size, layout.WrapWidth, clip, text.FontFamily, verticallyCentered: true);
        text.DrawOutlinedClipped(batch, value, size, new Vector2(bounds.X - layout.OffsetX, bounds.Y + bounds.Height / 2), layout.WrapWidth, ColorFor(color), ColorFor(outline), thickness, clip);
    }

    public void Text(string value, float size, UiRect bounds, UiColor color, string? fontFamily)
    {
        Text(value, size, bounds, color, fontFamily, UiAlignment.Start);
    }

    public void Text(string value, float size, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
    {
        Text(value, size, bounds, color, null, verticalAlignment);
    }

    public void Text(string value, float size, UiRect bounds, UiColor color, UiAlignment verticalAlignment, UiTextFlow flow)
    {
        Text(value, size, bounds, color, null, verticalAlignment, flow);
    }

    public void Text(string value, float size, UiRect bounds, UiColor color, string? fontFamily, UiAlignment verticalAlignment, UiTextFlow flow = UiTextFlow.Auto)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || size <= 0 || string.IsNullOrEmpty(value))
        {
            return;
        }
        float lineHeight = fontFamily == null || fontFamily == text.FontFamily ? MeasureText("Mg", size, float.PositiveInfinity).Height : SystemTextLayout.LineHeight(size, fontFamily);
        size = UiTextOverflow.FitFontSize(size, bounds.Height, lineHeight);
        if (ButtonPromptText.Contains(value))
        {
            ButtonPromptText.Draw(this, value, size, bounds, color, fontFamily ?? text.FontFamily, verticalAlignment, textOverflow, flow);
            return;
        }
        UiTextViewport layout = textOverflow.Layout(value, size, bounds, fontFamily ?? text.FontFamily, width =>
        {
            Vector2 measured = text.MeasureWrapped(value, size, width, fontFamily);
            return new UiSize(measured.X, measured.Y);
        }, flow);
        Vector2 position = new(bounds.X - layout.OffsetX, bounds.Y - layout.OffsetY);
        if (verticalAlignment is UiAlignment.Center or UiAlignment.End)
        {
            position.Y += Math.Max(0, bounds.Height - layout.Content.Height) * (verticalAlignment == UiAlignment.Center ? 0.5f : 1);
        }
        Inspect(value, position, size, layout.WrapWidth, Rectangle.Intersect(RectangleFor(bounds), CurrentClip), fontFamily);
        text.DrawWrappedClipped(batch, value, size, position, layout.WrapWidth, ColorFor(color), Rectangle.Intersect(RectangleFor(bounds), CurrentClip), fontFamily);
    }

    public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
    {
        Vector2 start = new(x1, y1);
        Vector2 delta = new Vector2(x2, y2) - start;
        if (y1 == y2)
        {
            Fill(new UiRect(Math.Min(x1, x2), y1, Math.Abs(x2 - x1), thickness), color);
            return;
        }
        if (x1 == x2)
        {
            Fill(new UiRect(x1, Math.Min(y1, y2), thickness, Math.Abs(y2 - y1)), color);
            return;
        }
        if (!ClipLine(ref start, ref delta, CurrentClip, thickness))
        {
            return;
        }
        if (delta.LengthSquared() > 0 && thickness > 0)
        {
            batch.Draw(pixel, start, null, ColorFor(color), MathF.Atan2(delta.Y, delta.X), Vector2.Zero, new Vector2(delta.Length(), thickness), SpriteEffects.None, 0);
        }
    }

    public void PushClip(UiRect bounds)
    {
        Rectangle next = Rectangle.Intersect(RectangleFor(bounds), clips.Count == 0 ? device.Viewport.Bounds : clips.Peek());
        clips.Push(next);
    }

    public void PopClip()
    {
        if (clips.Count == 0)
        {
            throw new InvalidOperationException("Unbalanced UI clipping.");
        }
        clips.Pop();
    }

    private Rectangle CurrentClip => clips.Count == 0 ? device.Viewport.Bounds : clips.Peek();

    private static bool ClipLine(ref Vector2 start, ref Vector2 delta, Rectangle clip, float thickness)
    {
        float lower = 0;
        float upper = 1;
        bool Boundary(float p, float q)
        {
            if (p == 0)
            {
                return q >= 0;
            }
            float ratio = q / p;
            if (p < 0)
            {
                lower = Math.Max(lower, ratio);
            }
            else
            {
                upper = Math.Min(upper, ratio);
            }
            return lower <= upper;
        }
        float inset = thickness / 2;
        if (clip.IsEmpty || !Boundary(-delta.X, start.X - clip.Left - inset) || !Boundary(delta.X, clip.Right - inset - start.X) || !Boundary(-delta.Y, start.Y - clip.Top - inset) || !Boundary(delta.Y, clip.Bottom - inset - start.Y))
        {
            return false;
        }
        Vector2 original = delta;
        start += original * lower;
        delta = original * (upper - lower);
        return true;
    }

    internal static Rectangle FillRectangleFor(UiRect bounds)
    {
        int left = (int)MathF.Round(bounds.X);
        int top = (int)MathF.Round(bounds.Y);
        int right = (int)MathF.Round(bounds.Right);
        int bottom = (int)MathF.Round(bounds.Bottom);
        return new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static Rectangle RectangleFor(UiRect bounds)
    {
        int left = (int)MathF.Floor(bounds.X);
        int top = (int)MathF.Floor(bounds.Y);
        return new Rectangle(left, top, Math.Max(0, (int)MathF.Ceiling(bounds.X + bounds.Width) - left), Math.Max(0, (int)MathF.Ceiling(bounds.Y + bounds.Height) - top));
    }

    internal static Color ColorFor(UiColor color) => Color.FromNonPremultiplied(color.R, color.G, color.B, color.A);

    public void Dispose()
    {
        DisposeModPreviews();
        paper?.Dispose();
        menuPointer?.Dispose();
        foreach (Texture2D button in controllerButtons.Values)
        {
            button.Dispose();
        }
        controllerButtons.Clear();
        foreach (Texture2D texture in images.Values)
        {
            texture.Dispose();
        }
        images.Clear();
    }
}
