using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal readonly record struct NativeGlyphPlacement(Vector2 Position, Rectangle Source, Vector2 Scale);

/// <summary>Clipping trims integer destination/source intervals without expanding into an adjacent atlas image.</summary>
internal static class NativeGlyphClipping
{
    public static NativeGlyphPlacement? Clip(Rectangle destination, Rectangle image, Rectangle clip)
    {
        Rectangle visible = Rectangle.Intersect(destination, clip);
        if (visible.Width <= 0 || visible.Height <= 0 || destination.Width <= 0 || destination.Height <= 0)
        {
            return null;
        }
        // Integer pixel edges use the actual raster destination dimensions, preserving the original mapping ratio.
        int left = Math.Clamp((int)Math.Ceiling((visible.Left - destination.Left) * (double)image.Width / destination.Width), 0, image.Width);
        int top = Math.Clamp((int)Math.Ceiling((visible.Top - destination.Top) * (double)image.Height / destination.Height), 0, image.Height);
        int right = Math.Clamp((int)((long)(visible.Right - destination.Left) * image.Width / destination.Width), left, image.Width);
        int bottom = Math.Clamp((int)((long)(visible.Bottom - destination.Top) * image.Height / destination.Height), top, image.Height);
        if (right <= left || bottom <= top)
        {
            return null;
        }
        Vector2 scale = new(destination.Width / (float)image.Width, destination.Height / (float)image.Height);
        return new(new(destination.X + left * scale.X, destination.Y + top * scale.Y), new(image.X + left, image.Y + top, right - left, bottom - top), scale);
    }
}
