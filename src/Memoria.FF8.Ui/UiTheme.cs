namespace Memoria.FF8.Ui;

/// <summary>Grayscale panels with the original game's menu pointer supplied by the canvas.</summary>
public sealed class UiTheme
{
    public UiColor Foreground { get; init; } = new(244, 244, 244);
    public UiColor Muted { get; init; } = new(155, 155, 155);
    public UiColor Highlight { get; init; } = new(112, 112, 112);
    public UiColor BorderLight { get; init; } = new(200, 200, 200);
    public UiColor BorderDark { get; init; } = new(30, 30, 30);
    public float FontSize { get; init; } = 16;

    public void Panel(IUiCanvas canvas, UiRect bounds, string? caption = null)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        canvas.PushClip(bounds);
        try
        {
            const int bands = 24;
            for (int index = 0; index < bands; index++)
            {
                byte shade = (byte)(100 - index * 40 / (bands - 1));
                float top = bounds.Y + bounds.Height * index / bands;
                float bottom = bounds.Y + bounds.Height * (index + 1) / bands;
                canvas.Fill(new UiRect(bounds.X, top, bounds.Width, bottom - top), new UiColor(shade, shade, shade));
            }

            float right = bounds.Right - 1;
            float bottomEdge = bounds.Bottom - 1;
            canvas.Line(bounds.X, bounds.Y, right, bounds.Y, BorderLight, 1);
            canvas.Line(bounds.X, bounds.Y, bounds.X, bottomEdge, BorderLight, 1);
            canvas.Line(bounds.X, bottomEdge, right, bottomEdge, BorderDark, 1);
            canvas.Line(right, bounds.Y, right, bottomEdge, BorderDark, 1);
            if (bounds.Width >= 6 && bounds.Height >= 6)
            {
                canvas.Line(bounds.X + 2, bounds.Y + 2, right - 2, bounds.Y + 2, BorderDark, 1);
                canvas.Line(bounds.X + 2, bounds.Y + 2, bounds.X + 2, bottomEdge - 2, BorderDark, 1);
                canvas.Line(bounds.X + 2, bottomEdge - 2, right - 2, bottomEdge - 2, Muted, 1);
                canvas.Line(right - 2, bounds.Y + 2, right - 2, bottomEdge - 2, Muted, 1);
            }
            if (!string.IsNullOrEmpty(caption))
            {
                canvas.Text(caption, FontSize, new UiRect(bounds.X + 10, bounds.Y + 6, Math.Max(0, bounds.Width - 20), Math.Min(FontSize * 1.5f, bounds.Height)), Foreground);
            }
        }
        finally
        {
            canvas.PopClip();
        }
    }

    public void Pointer(IUiCanvas canvas, float x, float y, float size = 10)
    {
        canvas.MenuPointer(new UiRect(x, y, size * 1.5f, size));
    }
}
