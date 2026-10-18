using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal static class BattleDamageTextPresentation
{
    public static void Draw(IReadOnlyList<NativeBattleDamageTextDraw> draws, IUiCanvas canvas, UiRect viewport)
    {
        float scaleX = viewport.Width / 320;
        float scaleY = viewport.Height / 240;
        foreach (NativeBattleDamageTextDraw draw in draws)
        {
            byte alpha = (draw.Color & 0x04000000) != 0 ? (byte)128 : (byte)255;
            UiColor color = new((byte)Math.Min(255, (draw.Color & 255) * 2), (byte)Math.Min(255, ((draw.Color >> 8) & 255) * 2), (byte)Math.Min(255, ((draw.Color >> 16) & 255) * 2), alpha);
            UiColor outline = new(0, 0, 0, alpha);
            if (draw.Glyphs[0] == 69)
            {
                DrawLabel("TRIPLE!!", draw.X - 24, draw.Y, 48, new UiColor(0, color.G, color.B, alpha));
                DrawLabel("DOUBLE!", draw.X - 24, draw.Y - 12, 48, new UiColor(color.R, (byte)(color.G / 2), color.B, alpha));
                continue;
            }
            for (int index = 0; index < draw.Glyphs.Length && draw.Glyphs[index] != 0; index++)
            {
                byte glyph = draw.Glyphs[index];
                string value = glyph is >= 56 and <= 65 ? ((char)('0' + glyph - 56)).ToString() : glyph == 16 ? "Miss" : throw new NotSupportedException($"Native damage text glyph {glyph} requires its system text presentation.");
                DrawLabel(value, draw.X + index * 8 - (glyph == 16 ? 8 : 0), draw.Y, glyph == 16 ? 40 : 9, color);
            }

            void DrawLabel(string value, int x, int y, int width, UiColor foreground)
            {
                UiRect bounds = new(viewport.X + x * scaleX, viewport.Y + y * scaleY, width * scaleX, 16 * scaleY);
                canvas.OutlinedText(value, 12 * scaleY, bounds, foreground, outline, Math.Max(scaleX, scaleY) * 0.65f);
            }
        }
    }
}
