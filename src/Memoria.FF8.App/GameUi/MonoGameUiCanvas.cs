using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.GameUi;

/// <summary>Draws shared retained controls using a single generated white pixel and the system text rasterizer.</summary>
internal sealed class MonoGameUiCanvas(GraphicsDevice device, SpriteBatch batch, Texture2D pixel, SystemTextRenderer text) : IUiCanvas, IDisposable
{
    private readonly Stack<Rectangle> clips = new();

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
        if (bounds.Width <= 0 || bounds.Height <= 0 || size <= 0 || string.IsNullOrEmpty(value))
        {
            return;
        }
        Vector2 position = new(bounds.X, bounds.Y);
        text.DrawWrappedClipped(batch, value, size, position, bounds.Width, ColorFor(color), Rectangle.Intersect(RectangleFor(bounds), CurrentClip));
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
    }
}
