namespace Memoria.FF8.Ui;

/// <summary>A procedural identity placeholder, independent of native portrait assets.</summary>
public sealed class UiPortrait : UiElement
{
    public int Seed { get; set; }
    public string Initials { get; set; } = string.Empty;

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        theme.Panel(canvas, Bounds);
        UiRect inner = Bounds.Inset(4);
        if (inner.IsEmpty)
        {
            return;
        }

        uint seed = unchecked((uint)Seed);
        byte bodyShade = (byte)(120 + seed % 36);
        byte faceShade = (byte)(184 + seed % 32);
        float centerX = inner.X + inner.Width / 2;
        FillEllipse(canvas, centerX, inner.Y + inner.Height * 0.94f, inner.Width * 0.43f, inner.Height * 0.33f, new UiColor(bodyShade, bodyShade, bodyShade));
        canvas.Fill(new UiRect(centerX - inner.Width * 0.085f, inner.Y + inner.Height * 0.51f, inner.Width * 0.17f, inner.Height * 0.15f), new UiColor(faceShade, faceShade, faceShade));
        FillEllipse(canvas, centerX, inner.Y + inner.Height * 0.35f, inner.Width * 0.22f, inner.Height * 0.25f, new UiColor(faceShade, faceShade, faceShade));
        float hairY = inner.Y + inner.Height * 0.17f;
        canvas.Fill(new UiRect(centerX - inner.Width * 0.21f, hairY, inner.Width * 0.42f, inner.Height * 0.09f), theme.BorderDark);
        if (!string.IsNullOrEmpty(Initials))
        {
            UiRect label = new(inner.X, inner.Bottom - Math.Min(24, inner.Height * 0.3f), inner.Width, Math.Min(24, inner.Height * 0.3f));
            canvas.Fill(label, theme.BorderDark);
            canvas.Text(Initials, Math.Min(theme.FontSize, label.Height * 0.7f), label.Inset(2), theme.Foreground);
        }
    }

    private static void FillEllipse(IUiCanvas canvas, float centerX, float centerY, float radiusX, float radiusY, UiColor color)
    {
        const int bands = 24;
        for (int index = 0; index < bands; index++)
        {
            float normalizedY = (index + 0.5f) * 2 / bands - 1;
            float halfWidth = radiusX * MathF.Sqrt(1 - normalizedY * normalizedY);
            float top = centerY - radiusY + radiusY * 2 * index / bands;
            canvas.Fill(new UiRect(centerX - halfWidth, top, halfWidth * 2, radiusY * 2 / bands), color);
        }
    }
}
