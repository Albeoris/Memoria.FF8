namespace Memoria.FF8.Ui;

/// <summary>Geometric collection artwork generated from an identity, without texture or font assets.</summary>
public sealed class UiSigil : UiElement
{
    public int Seed { get; set; }
    public string Initials { get; set; } = "";

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        UiRect area = Bounds.Inset(6);
        float centerX = area.X + area.Width / 2;
        float centerY = area.Y + area.Height * 0.45f;
        float radius = Math.Min(area.Width, area.Height * 0.8f) * 0.42f;
        uint identity = unchecked((uint)Seed);
        int points = 4 + (int)(identity % 5);
        UiColor outline = new(174, 174, 174);
        UiColor bright = new(232, 232, 232);
        for (int index = 0; index < 32; index++)
        {
            float first = index * MathF.Tau / 32;
            float next = (index + 1) * MathF.Tau / 32;
            canvas.Line(centerX + MathF.Cos(first) * radius, centerY + MathF.Sin(first) * radius, centerX + MathF.Cos(next) * radius, centerY + MathF.Sin(next) * radius, outline, 1);
        }
        for (int index = 0; index < points; index++)
        {
            float angle = index * MathF.Tau / points - MathF.PI / 2;
            float next = (index + 1) * MathF.Tau / points - MathF.PI / 2;
            float x = centerX + MathF.Cos(angle) * radius * 0.82f;
            float y = centerY + MathF.Sin(angle) * radius * 0.82f;
            canvas.Line(centerX, centerY, x, y, bright, 2);
            canvas.Line(x, y, centerX + MathF.Cos(next) * radius * 0.82f, centerY + MathF.Sin(next) * radius * 0.82f, bright, 1);
        }
        float core = radius * (0.18f + identity % 3 * 0.04f);
        canvas.Fill(new UiRect(centerX - core, centerY - core, core * 2, core * 2), bright);
        if (Initials.Length > 0)
        {
            float height = Math.Min(20, area.Height * 0.22f);
            canvas.Text(Initials, height * 0.7f, new UiRect(area.X, area.Bottom - height, area.Width, height), theme.Foreground);
        }
    }
}
