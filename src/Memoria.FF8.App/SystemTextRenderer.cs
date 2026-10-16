using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Memoria.FF8;

internal sealed class SystemTextRenderer(GraphicsDevice device) : IDisposable
{
    private readonly Dictionary<(string Text, float Size, float Width, string Family, bool PreserveLineMetrics), Texture2D> cache = [];
    private readonly Queue<(string Text, float Size, float Width, string Family, bool PreserveLineMetrics)> order = [];

    public string FontFamily { get; set; } = SystemFontCatalog.DefaultFamily;

    public void BeginFrame()
    {
        while (cache.Count > 512 && order.TryDequeue(out var oldest))
        {
            if (cache.Remove(oldest, out Texture2D? removed))
            {
                removed.Dispose();
            }
        }
    }

    public void Preload(string value, float size, float maximumWidth)
    {
        _ = GetTexture(value, size, maximumWidth);
    }

    public void DrawWrappedClipped(SpriteBatch batch, string value, float size, XnaVector2 position, float maximumWidth, XnaColor color, Microsoft.Xna.Framework.Rectangle clip, string? fontFamily = null)
    {
        Texture2D texture = GetTexture(value, size, maximumWidth, fontFamily);
        DrawClipped(batch, texture, position + new XnaVector2(1, 2), XnaColor.Black * (color.A / 255f * 0.75f), clip);
        DrawClipped(batch, texture, position, color, clip);
    }

    public void DrawOutlinedClipped(SpriteBatch batch, string value, float size, XnaVector2 centerLeft, float maximumWidth, XnaColor color, XnaColor outline, float thickness, Microsoft.Xna.Framework.Rectangle clip)
    {
        Texture2D texture = GetTexture(value, size, maximumWidth);
        XnaVector2 position = centerLeft - new XnaVector2(0, texture.Height / (2f * SystemTextLayout.RasterScale));
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x != 0 || y != 0)
                {
                    DrawClipped(batch, texture, position + new XnaVector2(x, y) * thickness, outline, clip);
                }
            }
        }
        DrawClipped(batch, texture, position, color, clip);
    }

    internal static void DrawClipped(SpriteBatch batch, Texture2D texture, XnaVector2 position, XnaColor color, Microsoft.Xna.Framework.Rectangle clip, float scale = 1f / SystemTextLayout.RasterScale)
    {
        int left = Math.Clamp((int)MathF.Ceiling((clip.Left - position.X) / scale), 0, texture.Width);
        int top = Math.Clamp((int)MathF.Ceiling((clip.Top - position.Y) / scale), 0, texture.Height);
        int right = Math.Clamp((int)MathF.Floor((clip.Right - position.X) / scale), left, texture.Width);
        int bottom = Math.Clamp((int)MathF.Floor((clip.Bottom - position.Y) / scale), top, texture.Height);
        if (right > left && bottom > top)
        {
            var source = new Microsoft.Xna.Framework.Rectangle(left, top, right - left, bottom - top);
            batch.Draw(texture, position + new XnaVector2(left, top) * scale, source, color, 0, XnaVector2.Zero, scale, SpriteEffects.None, 0);
        }
    }

    public XnaVector2 MeasureWrapped(string text, float size, float maximumWidth, string? fontFamily = null)
    {
        Texture2D texture = GetTexture(text, size, maximumWidth, fontFamily);
        return new XnaVector2(texture.Width, texture.Height) / SystemTextLayout.RasterScale;
    }

    public void DrawCentered(SpriteBatch batch, string text, float size, XnaVector2 center, XnaColor color, string? fontFamily = null)
    {
        Texture2D texture = GetTexture(text, size, float.PositiveInfinity, fontFamily);
        DrawTexture(batch, texture, new XnaVector2(center.X - texture.Width / (2f * SystemTextLayout.RasterScale), center.Y), color);
    }

    public void DrawWrapped(SpriteBatch batch, string text, float size, XnaVector2 position, float maximumWidth, XnaColor color, string? fontFamily = null)
    {
        DrawTexture(batch, GetTexture(text, size, maximumWidth, fontFamily), position, color);
    }

    public void Draw(SpriteBatch batch, string text, float size, XnaVector2 position, XnaColor color, string? fontFamily = null)
    {
        DrawTexture(batch, GetTexture(text, size, float.PositiveInfinity, fontFamily), position, color);
    }

    public void DrawLine(SpriteBatch batch, string value, float size, XnaVector2 position, XnaColor color, string? fontFamily = null)
    {
        float margin = SystemTextLayout.RasterMargin(size) / (float)SystemTextLayout.RasterScale;
        DrawTexture(batch, GetTexture(value, size, float.PositiveInfinity, fontFamily, preserveLineMetrics: true), position - new XnaVector2(margin), color);
    }

    private static void DrawTexture(SpriteBatch batch, Texture2D texture, XnaVector2 position, XnaColor color)
    {
        batch.Draw(texture, position + new XnaVector2(1, 2), null, XnaColor.Black * (color.A / 255f * 0.75f), 0, XnaVector2.Zero, 1f / SystemTextLayout.RasterScale, SpriteEffects.None, 0);
        batch.Draw(texture, position, null, color, 0, XnaVector2.Zero, 1f / SystemTextLayout.RasterScale, SpriteEffects.None, 0);
    }

    private Texture2D GetTexture(string text, float size, float maximumWidth, string? fontFamily = null, bool preserveLineMetrics = false)
    {
        string family = fontFamily ?? FontFamily;
        var key = (text, size, maximumWidth, family, preserveLineMetrics);
        if (!cache.TryGetValue(key, out Texture2D? texture))
        {
            using var bitmap = SystemTextLayout.Rasterize(text, size, maximumWidth, family, preserveLineMetrics);
            XnaColor[] pixels = SystemTextLayout.PremultipliedPixels(bitmap);
            texture = new Texture2D(device, bitmap.Width, bitmap.Height);
            texture.SetData(pixels);
            cache.Add(key, texture);
            order.Enqueue(key);
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
        order.Clear();
    }
}
