using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Memoria.FF8;

internal sealed class SystemTextRenderer(GraphicsDevice device) : IDisposable
{
    private readonly Dictionary<(string Text, float Size, float Width), Texture2D> cache = [];

    public XnaVector2 MeasureWrapped(string text, float size, float maximumWidth)
    {
        Texture2D texture = GetTexture(text, size, maximumWidth);
        return new XnaVector2(texture.Width, texture.Height) / SystemTextLayout.RasterScale;
    }

    public void DrawCentered(SpriteBatch batch, string text, float size, XnaVector2 center, XnaColor color)
    {
        Texture2D texture = GetTexture(text, size, float.PositiveInfinity);
        DrawTexture(batch, texture, new XnaVector2(center.X - texture.Width / (2f * SystemTextLayout.RasterScale), center.Y), color);
    }

    public void DrawWrapped(SpriteBatch batch, string text, float size, XnaVector2 position, float maximumWidth, XnaColor color)
    {
        DrawTexture(batch, GetTexture(text, size, maximumWidth), position, color);
    }

    public void Draw(SpriteBatch batch, string text, float size, XnaVector2 position, XnaColor color)
    {
        DrawTexture(batch, GetTexture(text, size, float.PositiveInfinity), position, color);
    }

    private static void DrawTexture(SpriteBatch batch, Texture2D texture, XnaVector2 position, XnaColor color)
    {
        batch.Draw(texture, position, null, color, 0, XnaVector2.Zero, 1f / SystemTextLayout.RasterScale, SpriteEffects.None, 0);
    }

    private Texture2D GetTexture(string text, float size, float maximumWidth)
    {
        var key = (text, size, maximumWidth);
        if (!cache.TryGetValue(key, out Texture2D? texture))
        {
            using var bitmap = SystemTextLayout.Rasterize(text, size, maximumWidth);
            XnaColor[] pixels = SystemTextLayout.PremultipliedPixels(bitmap);
            texture = new Texture2D(device, bitmap.Width, bitmap.Height);
            texture.SetData(pixels);
            cache.Add(key, texture);
        }
        return texture;
    }

    public void Dispose()
    {
        foreach (Texture2D texture in cache.Values)
        {
            texture.Dispose();
        }
        cache.Clear();
    }
}
