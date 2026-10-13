using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Memoria.FF8;

internal sealed class SystemTextRenderer(GraphicsDevice device) : IDisposable
{
    private readonly Dictionary<(string Text, float Size), Texture2D> cache = [];

    public XnaVector2 MeasureWrapped(string text, float size, float maximumWidth)
    {
        using var font = new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Pixel);
        using var bitmap = new Bitmap(1, 1);
        using Graphics graphics = Graphics.FromImage(bitmap);
        float width = 0;
        int lines = 0;
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length != 0 && graphics.MeasureString(candidate, font).Width > maximumWidth)
                {
                    width = Math.Max(width, graphics.MeasureString(line, font).Width);
                    lines++;
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            width = Math.Max(width, graphics.MeasureString(line, font).Width);
            lines++;
        }
        return new XnaVector2(Math.Min(maximumWidth, width), lines * size * 1.3f);
    }

    public void DrawCentered(SpriteBatch batch, string text, float size, XnaVector2 center, XnaColor color)
    {
        using var font = new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Pixel);
        using var bitmap = new Bitmap(1, 1);
        using Graphics graphics = Graphics.FromImage(bitmap);
        float width = graphics.MeasureString(text, font).Width;
        Draw(batch, text, size, new XnaVector2(center.X - width / 2, center.Y), color);
    }

    public void DrawWrapped(SpriteBatch batch, string text, float size, XnaVector2 position, float maximumWidth, XnaColor color)
    {
        using var font = new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Pixel);
        using var bitmap = new Bitmap(1, 1);
        using Graphics graphics = Graphics.FromImage(bitmap);
        float y = position.Y;
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length != 0 && graphics.MeasureString(candidate, font).Width > maximumWidth)
                {
                    Draw(batch, line, size, new XnaVector2(position.X, y), color);
                    y += size * 1.3f;
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            Draw(batch, line, size, new XnaVector2(position.X, y), color);
            y += size * 1.3f;
        }
    }

    public void Draw(SpriteBatch batch, string text, float size, XnaVector2 position, XnaColor color)
    {
        var key = (text, size);
        if (!cache.TryGetValue(key, out Texture2D? texture))
        {
            using var font = new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Pixel);
            using var measure = new Bitmap(1, 1);
            using Graphics measuring = Graphics.FromImage(measure);
            SizeF dimensions = measuring.MeasureString(text, font);
            using var bitmap = new Bitmap(Math.Max(1, (int)Math.Ceiling(dimensions.Width)), Math.Max(1, (int)Math.Ceiling(dimensions.Height)), PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.DrawString(text, font, Brushes.White, 0, 0);
            }
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            texture = Texture2D.FromStream(device, stream);
            cache.Add(key, texture);
        }
        batch.Draw(texture, position, color);
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
