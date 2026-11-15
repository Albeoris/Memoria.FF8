using System.Globalization;
using Memoria.FF7.Runtime;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Texture2D? numberWindowAtlas;

    private bool DrawSpecialWindow(SpriteBatch batch, Ff7Dialogue dialogue, Ff7Window window, Rectangle bounds)
    {
        if (window.SpecialType is 0 or 2)
        {
            return false;
        }
        if (window.SpecialType != 1)
        {
            throw new NotSupportedException($"FFVII window special display {window.SpecialType} requires its native value binding.");
        }
        UiPreferences preferences = PresentationPreferences;
        float scale = bounds.Width / 320f;
        float size = 21 * scale * preferences.TextScale / 100f;
        Vector2 origin = new(bounds.X + (window.X + window.SpecialX) * scale, bounds.Y + (window.Y + window.SpecialY) * scale);
        string value = Ff7CountdownText.Format(Session!.Clock.CountdownSeconds);
        float[] offsets = [0, 16, 32, 40, 56];
        for (int index = 0; index < value.Length; index++)
        {
            dialogueText!.DrawLine(batch, value[index].ToString(CultureInfo.InvariantCulture), size, origin + new Vector2(offsets[index] * scale, 0), Color.White, preferences.UiFontFamily);
        }
        if (Session.Host.Process.FadeState is { QuadBlend: Ff7FadeQuadBlend.Alpha, QuadColorArgb: uint quad } && (quad >> 24) != 0)
        {
            batch.Draw(pixel, origin, null, new Color(0, 0, 0, (int)(quad >> 24)), 0, Vector2.Zero, new Vector2(72, 21) * scale, SpriteEffects.None, 0);
        }
        return true;
    }

    private void DrawNumberSpecialWindow(SpriteBatch batch, Ff7Window window, Rectangle bounds, Rectangle clip)
    {
        IReadOnlyList<Ff7SpecialNumberGlyph> glyphs = Ff7NumberWindowPresentation.Create(window);
        if (glyphs.Count == 0)
        {
            return;
        }
        Texture2D atlas = NumberWindowAtlas();
        float scale = bounds.Width / 320f;
        Vector2 glyphScale = new(scale / 2 * PresentationPreferences.TextScale / 100f);
        foreach (Ff7SpecialNumberGlyph glyph in glyphs)
        {
            Vector2 position = new(bounds.X + glyph.X * scale, bounds.Y + glyph.Y * scale);
            Rectangle source = new((glyph.SourceX + 256) % 256, glyph.SourceY, 32, 42);
            SpriteRegionDrawing.DrawClipped(batch, atlas, position, source, Color.White, clip, glyphScale);
        }
        if (Session!.Host.Process.FadeState is { QuadBlend: Ff7FadeQuadBlend.Alpha, QuadColorArgb: uint quad } && (quad >> 24) != 0)
        {
            Vector2 origin = new(bounds.X + glyphs[0].X * scale, bounds.Y + glyphs[0].Y * scale);
            Rectangle faded = Rectangle.Intersect(clip, new Rectangle((int)origin.X, (int)origin.Y, (int)MathF.Ceiling(glyphs.Count * 16 * scale), (int)MathF.Ceiling(21 * scale)));
            if (faded.Width > 0 && faded.Height > 0)
            {
                batch.Draw(pixel, faded, new Color(0, 0, 0, (int)(quad >> 24)));
            }
        }
    }

    private Texture2D NumberWindowAtlas()
    {
        if (numberWindowAtlas is not null)
        {
            return numberWindowAtlas;
        }
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MenuArchive).Path);
        IReadOnlyList<LgpEntry> members = archive.Find("btl_win_b_l.tex");
        if (members.Count != 1)
        {
            throw new InvalidDataException("Native numeric windows require one unambiguous original field UI atlas.");
        }
        PcTexture texture = PcTexture.Read(archive.ReadEntry(members[0]));
        numberWindowAtlas = UploadTexture(texture.Width, texture.Height, Ff7NumberWindowPresentation.AtlasColors(texture));
        return numberWindowAtlas;
    }
}

internal static class Ff7CountdownText
{
    internal static string Format(uint seconds)
    {
        seconds = Math.Min(seconds, 5999);
        return (seconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
    }
}


internal sealed record Ff7SpecialNumberGlyph(int X, int Y, int SourceX, int SourceY);

internal static class Ff7NumberWindowPresentation
{
    internal static IReadOnlyList<Ff7SpecialNumberGlyph> Create(Ff7Window window)
    {
        int remaining = window.NativeNumber;
        int digits = unchecked((sbyte)window.NativeNumberDigits);
        int[] divisors = [10000000, 1000000, 100000, 10000, 1000, 100, 10, 1];
        List<Ff7SpecialNumberGlyph> glyphs = [];
        for (int index = 0; index < divisors.Length; index++)
        {
            int quotient = remaining / divisors[index];
            if (8 - index <= digits)
            {
                glyphs.Add(new(window.X + window.SpecialX + glyphs.Count * 16, window.Y + window.SpecialY, 96 + quotient % 5 * 32, quotient > 4 ? 208 : 160));
            }
            remaining %= divisors[index];
        }
        return glyphs.AsReadOnly();
    }

    internal static Color[] AtlasColors(PcTexture texture)
    {
        if (texture.Width != 256 || texture.Height != 256)
        {
            throw new InvalidDataException("Native numeric windows require the original 256-pixel field UI atlas.");
        }
        var image = Ff7FieldRenderer.DecodeTexture(texture, useColorKey: true);
        return image.Pixels.Select(pixel => pixel.A == 0 ? Color.Transparent : new Color(pixel.R, pixel.G, pixel.B, (byte)255)).ToArray();
    }
}
