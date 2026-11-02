namespace Memoria.FF8.Ui;

/// <summary>Draws the gray padlock used for unavailable junction slots and locked settings.</summary>
public static class UiLockGlyph
{
    public static void Draw(IUiCanvas canvas, UiRect bounds)
    {
        if (bounds.IsEmpty)
        {
            return;
        }
        UiColor gray = new(128, 128, 128);
        float scale = Math.Min(bounds.Width / 16, bounds.Height / 20);
        float x = bounds.X;
        float y = bounds.Y + (bounds.Height - 20 * scale) / 2;
        canvas.Line(x + 4 * scale, y + 9 * scale, x + 4 * scale, y + 5 * scale, gray, 2 * scale);
        canvas.Line(x + 12 * scale, y + 9 * scale, x + 12 * scale, y + 5 * scale, gray, 2 * scale);
        for (int segment = 0; segment < 12; segment++)
        {
            float start = MathF.PI + segment * MathF.PI / 12;
            float end = MathF.PI + (segment + 1) * MathF.PI / 12;
            canvas.Line(x + (8 + 4 * MathF.Cos(start)) * scale, y + (5 + 4 * MathF.Sin(start)) * scale, x + (8 + 4 * MathF.Cos(end)) * scale, y + (5 + 4 * MathF.Sin(end)) * scale, gray, 2 * scale);
        }
        canvas.Fill(new UiRect(x + 2 * scale, y + 8 * scale, 12 * scale, scale), gray);
        canvas.Fill(new UiRect(x + scale, y + 9 * scale, 14 * scale, 3 * scale), gray);
        canvas.Fill(new UiRect(x + scale, y + 12 * scale, 6 * scale, 4 * scale), gray);
        canvas.Fill(new UiRect(x + 9 * scale, y + 12 * scale, 6 * scale, 4 * scale), gray);
        canvas.Fill(new UiRect(x + scale, y + 16 * scale, 14 * scale, 3 * scale), gray);
        canvas.Fill(new UiRect(x + 2 * scale, y + 19 * scale, 12 * scale, scale), gray);
    }
}
