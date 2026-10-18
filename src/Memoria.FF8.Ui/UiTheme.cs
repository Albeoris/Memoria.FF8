namespace Memoria.FF8.Ui;

/// <summary>Grayscale panels with the original game's menu pointer supplied by the canvas.</summary>
public sealed class UiTheme
{
    public UiColor Foreground { get; init; } = new(244, 244, 244);
    public UiColor Muted { get; init; } = new(155, 155, 155);
    public UiColor Highlight { get; init; } = new(112, 112, 112);
    public UiColor BorderLight { get; init; } = new(200, 200, 200);
    public UiColor BorderDark { get; init; } = new(30, 30, 30);
    public UiColor SliderFill { get; init; } = new(144, 104, 224);
    public UiColor SliderLight { get; init; } = new(232, 216, 255);
    public UiColor SliderDark { get; init; } = new(40, 20, 104);
    public byte PanelBackgroundAlpha { get; init; } = 255;
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
                byte shade = (byte)(112 - index * 40 / (bands - 1));
                float left = bounds.X + bounds.Width * index / bands;
                float rightEdge = bounds.X + bounds.Width * (index + 1) / bands;
                canvas.Fill(new UiRect(left, bounds.Y, rightEdge - left, bounds.Height), new UiColor(shade, shade, shade, PanelBackgroundAlpha));
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
        }
        finally
        {
            canvas.PopClip();
        }
        Caption(canvas, bounds, caption, captionFontSize);
    }

    public void Caption(IUiCanvas canvas, UiRect bounds, string? caption, float captionFontSize = 0)
    {
        if (bounds.IsEmpty || string.IsNullOrEmpty(caption))
        {
            return;
        }

        float size = captionFontSize > 0 ? captionFontSize : FontSize;
        float inset = captionFontSize > 0 ? 8 : 10;
        float height = size * 1.5f;
        UiRect textBounds = new(bounds.X + inset, bounds.Y - height / 2, Math.Max(0, bounds.Width - inset * 2), height);
        canvas.OutlinedText(caption, size, textBounds, Foreground, new UiColor(0, 0, 0), 1);
    }

    public void Pointer(IUiCanvas canvas, float x, float y, float size = 10)
    {
        canvas.MenuPointer(new UiRect(x, y, size * 1.5f, size));
    }

    internal void PointerBefore(IUiCanvas canvas, float contentLeft, float y, float size = 10)
    {
        if (size > 0)
        {
            Pointer(canvas, contentLeft - 4 - size * 1.5f, y, size);
        }
    }
}
