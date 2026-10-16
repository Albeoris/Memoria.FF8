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

    public void Panel(IUiCanvas canvas, UiRect bounds, string? caption = null, float captionFontSize = 0)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        canvas.PushClip(bounds);
        try
        {
            const int bands = 128;
            for (int index = 0; index < bands; index++)
            {
                byte shade = (byte)(96 - index * 40 / (bands - 1));
                float left = bounds.X + bounds.Width * index / bands;
                float rightEdge = bounds.X + bounds.Width * (index + 1) / bands;
                canvas.Fill(new UiRect(left, bounds.Y, rightEdge - left, bounds.Height), new UiColor(shade, shade, shade));
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
                float size = captionFontSize > 0 ? captionFontSize : FontSize;
                float x = captionFontSize > 0 ? 8 : 10;
                float y = captionFontSize > 0 ? 1 : 6;
                float height = captionFontSize > 0 ? size * 1.3f : size * 1.5f;
                canvas.Text(caption, size, new UiRect(bounds.X + x, bounds.Y + y, Math.Max(0, bounds.Width - x * 2), Math.Min(height, Math.Max(0, bounds.Height - y))), Foreground);
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
