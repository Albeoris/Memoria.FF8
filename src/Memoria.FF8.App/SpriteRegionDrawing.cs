using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal static class SpriteRegionDrawing
{
    internal static bool TryClip(Rectangle source, Vector2 position, Vector2 scale, Rectangle clip, out Rectangle croppedSource, out Vector2 croppedPosition)
    {
        int left = Math.Clamp((int)MathF.Ceiling((clip.Left - position.X) / scale.X), 0, source.Width);
        int top = Math.Clamp((int)MathF.Ceiling((clip.Top - position.Y) / scale.Y), 0, source.Height);
        int right = Math.Clamp((int)MathF.Floor((clip.Right - position.X) / scale.X), left, source.Width);
        int bottom = Math.Clamp((int)MathF.Floor((clip.Bottom - position.Y) / scale.Y), top, source.Height);
        croppedSource = new(source.X + left, source.Y + top, right - left, bottom - top);
        croppedPosition = position + new Vector2(left, top) * scale;
        return right > left && bottom > top;
    }

    internal static void DrawClipped(SpriteBatch batch, Texture2D texture, Vector2 position, Rectangle source, Color color, Rectangle clip, Vector2 scale)
    {
        if (TryClip(source, position, scale, clip, out Rectangle croppedSource, out Vector2 croppedPosition))
        {
            batch.Draw(texture, croppedPosition, croppedSource, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
    }
}
