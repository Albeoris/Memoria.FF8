using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed class Ff7FontRenderer : IDisposable
{
    private readonly Ff7FontResources resources;
    private readonly Texture2D[] atlases = new Texture2D[8];

    public Ff7FontRenderer(GraphicsDevice device, Ff7FontResources resources)
    {
        this.resources = resources;
        for (int color = 0; color < atlases.Length; color++)
        {
            TimTexture atlas = resources.Atlas;
            IReadOnlyList<ushort> palette = atlas.Palettes[color * 2];
            Color[] pixels = new Color[atlas.Width * atlas.Height];
            for (int y = 0; y < atlas.Height; y++)
            {
                for (int x = 0; x < atlas.Width; x++)
                {
                    ushort value = palette[atlas.GetPixel(x, y)];
                    pixels[y * atlas.Width + x] = value == 0 ? Color.Transparent : new Color((byte)((value & 31) * 255 / 31), (byte)(((value >> 5) & 31) * 255 / 31), (byte)(((value >> 10) & 31) * 255 / 31), (byte)255);
                }
            }
            Texture2D texture = new(device, atlas.Width, atlas.Height);
            texture.SetData(pixels);
            atlases[color] = texture;
        }
    }

    public float DrawGlyph(SpriteBatch batch, byte code, Vector2 position, Vector2 scale, int color = 7, bool fixedSpacing = false, Rectangle? clip = null)
    {
        Ff7FontGlyph glyph = resources.GetGlyph(code, color, fixedSpacing);
        Rectangle source = new(glyph.SourceX, glyph.SourceY, glyph.Width, glyph.Height);
        Vector2 target = position + new Vector2(glyph.LeftPadding * scale.X, 0);
        if (clip is Rectangle bounds)
        {
            SpriteRegionDrawing.DrawClipped(batch, atlases[color], target, source, Color.White, bounds, scale);
        }
        else
        {
            batch.Draw(atlases[color], target, source, Color.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
        return glyph.Advance * scale.X;
    }

    public void DrawPlain(SpriteBatch batch, string text, Vector2 position, Vector2 scale, int color = 7, Rectangle? clip = null)
    {
        float origin = position.X;
        foreach (char character in text)
        {
            if (character == '\n')
            {
                position.X = origin;
                position.Y += Ff7FontResources.LineHeight * scale.Y;
            }
            else
            {
                byte code = character is >= ' ' and <= '~' ? (byte)(character - 32) : (byte)31;
                position.X += DrawGlyph(batch, code, position, scale, color, clip: clip);
            }
        }
    }

    public float MeasurePlain(string text)
    {
        return text.Sum(character => resources.GetGlyph(character is >= ' ' and <= '~' ? (byte)(character - 32) : (byte)31).Advance);
    }

    public void Dispose()
    {
        foreach (Texture2D? atlas in atlases)
        {
            atlas?.Dispose();
        }
    }
}
