using Memoria.FF8.Ui;
using Memoria.FF8.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.GameUi;

/// <summary>Draws retained controls, system text and the original menu pointer.</summary>
internal sealed partial class MonoGameUiCanvas(GraphicsDevice device, SpriteBatch batch, Texture2D pixel, SystemTextRenderer text, string gameDirectory) : IUiCanvas, IBattleMessageCanvas, IDisposable
{
    private readonly Stack<Rectangle> clips = new();
    private Texture2D? menuPointer;
    private readonly MenuArtworkProvider artwork = new(gameDirectory);
    private readonly Dictionary<UiImageSource, Texture2D> images = [];

    public void Image(UiImageSource source, UiRect bounds)
    {
        if (bounds.IsEmpty)
        {
            return;
        }
        if (!images.TryGetValue(source, out Texture2D? texture))
        {
            var image = artwork.Read(source);
            texture = new Texture2D(device, image.Width, image.Height);
            texture.SetData(image.Pixels.Select(color => Color.FromNonPremultiplied(color.R, color.G, color.B, color.A)).ToArray());
            images.Add(source, texture);
        }
        UiRect destination = UiImage.Fit(bounds, texture.Width, texture.Height);
        Rectangle clip = Rectangle.Intersect(RectangleFor(bounds), CurrentClip);
        SystemTextRenderer.DrawClipped(batch, texture, new Vector2(destination.X, destination.Y), Color.White, clip, destination.Width / texture.Width);
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
            menuPointer = new Texture2D(device, image.Width, image.Height);
            menuPointer.SetData(image.Pixels.Select(color => Color.FromNonPremultiplied(color.R, color.G, color.B, color.A)).ToArray());
        }
        float scale = Math.Min(bounds.Width / menuPointer.Width, bounds.Height / menuPointer.Height);
        Vector2 position = new(bounds.X + (bounds.Width - menuPointer.Width * scale) / 2, bounds.Y + (bounds.Height - menuPointer.Height * scale) / 2);
        SystemTextRenderer.DrawClipped(batch, menuPointer, position, Color.White, CurrentClip, scale);
    }

    public void Fill(UiRect bounds, UiColor color)
    {
        Rectangle rectangle = Rectangle.Intersect(RectangleFor(bounds), CurrentClip);
        if (rectangle.Width > 0 && rectangle.Height > 0)
        {
            batch.Draw(pixel, rectangle, ColorFor(color));
        }
    }

    public void Text(string value, float size, UiRect bounds, UiColor color)
    {
        Text(value, size, bounds, color, null);
    }

    public void Text(string value, float size, UiRect bounds, UiColor color, string? fontFamily)
    {
        Text(value, size, bounds, color, fontFamily, UiAlignment.Start);
    }

    public void Text(string value, float size, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
    {
        Text(value, size, bounds, color, null, verticalAlignment);
    }

    private void Text(string value, float size, UiRect bounds, UiColor color, string? fontFamily, UiAlignment verticalAlignment)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || size <= 0 || string.IsNullOrEmpty(value))
        {
            return;
        }
        Vector2 position = new(bounds.X, bounds.Y);
        if (verticalAlignment is UiAlignment.Center or UiAlignment.End)
        {
            float height = text.MeasureWrapped(value, size, bounds.Width, fontFamily).Y;
            position.Y += (bounds.Height - height) * (verticalAlignment == UiAlignment.Center ? 0.5f : 1);
        }
        text.DrawWrappedClipped(batch, value, size, position, bounds.Width, ColorFor(color), Rectangle.Intersect(RectangleFor(bounds), CurrentClip), fontFamily);
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

    private static Rectangle RectangleFor(UiRect bounds)
    {
        int left = (int)MathF.Floor(bounds.X);
        int top = (int)MathF.Floor(bounds.Y);
        return new Rectangle(left, top, Math.Max(0, (int)MathF.Ceiling(bounds.X + bounds.Width) - left), Math.Max(0, (int)MathF.Ceiling(bounds.Y + bounds.Height) - top));
    }

    internal static Color ColorFor(UiColor color) => Color.FromNonPremultiplied(color.R, color.G, color.B, color.A);

    public void Dispose()
    {
        menuPointer?.Dispose();
        foreach (Texture2D texture in images.Values)
        {
            texture.Dispose();
        }
        images.Clear();
    }
}
