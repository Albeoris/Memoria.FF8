namespace Memoria.FF8.Ui;

/// <summary>Scalable translucent panels and restrained color accents for the vector interface.</summary>
public sealed class VectorUiTheme : UiTheme
{
    private static readonly UiColor Accent = new(228, 65, 77);

    public VectorUiTheme()
    {
        Foreground = new UiColor(235, 241, 248);
        Muted = new UiColor(141, 159, 182);
        Highlight = new UiColor(42, 85, 130, 210);
        BorderLight = new UiColor(172, 191, 211, 160);
        BorderDark = new UiColor(8, 18, 31, 230);
        SliderFill = new UiColor(126, 240, 250);
        SliderLight = new UiColor(203, 255, 255);
        SliderDark = new UiColor(24, 132, 165);
    }

    public override string? FontFamily { get; init; } = "Georgia";
    public override bool AlwaysHighlightSelection => true;
    public override float CaptionContentHeight(float captionFontSize) => captionFontSize * 1.5f + 12;

    public override void Panel(IUiCanvas canvas, UiRect bounds, string? caption = null, float captionFontSize = 0)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        Gradient(canvas, bounds, new UiColor(24, 43, 65, (byte)(202 * PanelBackgroundAlpha / 255)), new UiColor(8, 19, 34, (byte)(182 * PanelBackgroundAlpha / 255)));
        float right = bounds.Right - 1;
        float bottom = bounds.Bottom - 1;
        canvas.Line(bounds.X, bounds.Y, right, bounds.Y, BorderLight, 1);
        canvas.Line(bounds.X, bounds.Y, bounds.X, bottom, BorderLight, 1);
        canvas.Line(bounds.X, bottom, right, bottom, new UiColor(102, 125, 150, 100), 1);
        canvas.Line(right, bounds.Y, right, bottom, new UiColor(102, 125, 150, 100), 1);
        if (bounds.Width <= 240 && bounds.Height >= 240)
        {
            DrawFeather(canvas, bounds);
        }
        Caption(canvas, bounds, caption, captionFontSize);
    }

    public override void Caption(IUiCanvas canvas, UiRect bounds, string? caption, float captionFontSize = 0)
    {
        if (bounds.IsEmpty || string.IsNullOrEmpty(caption))
        {
            return;
        }

        float size = captionFontSize > 0 ? captionFontSize : DefaultCaptionFontSize;
        canvas.Text(caption, size, new UiRect(bounds.X + 12, bounds.Y + 5, Math.Max(0, bounds.Width - 24), size * 1.5f), Muted, UiAlignment.Center, UiTextFlow.SingleLine);
        float dividerY = Math.Min(bounds.Bottom - 1, bounds.Y + size * 1.5f + 9);
        canvas.Line(bounds.X + 12, dividerY, Math.Max(bounds.X + 12, bounds.Right - 12), dividerY, new UiColor(158, 180, 206, 65), 1);
    }

    public override void Pointer(IUiCanvas canvas, float x, float y, float size = 10)
    {
        base.Pointer(canvas, x, y, size);
    }

    public override void Selection(IUiCanvas canvas, UiRect bounds, bool underline = false)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        if (underline)
        {
            canvas.Fill(new UiRect(bounds.X, bounds.Bottom - Math.Min(3, bounds.Height), bounds.Width, Math.Min(3, bounds.Height)), Accent);
            return;
        }

        Gradient(canvas, bounds, new UiColor(44, 85, 131, 215), new UiColor(21, 47, 78, 175));
        canvas.Fill(new UiRect(bounds.X, bounds.Y, Math.Min(3, bounds.Width), bounds.Height), Accent);
        canvas.Line(bounds.X + Math.Min(3, bounds.Width), bounds.Y, bounds.Right - 1, bounds.Y, new UiColor(110, 158, 204, 90), 1);
    }

    public override void RowSeparator(IUiCanvas canvas, UiRect bounds)
    {
        if (!bounds.IsEmpty)
        {
            canvas.Line(bounds.X, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1, new UiColor(158, 180, 206, 37), 1);
        }
    }

    public override void NumericSlider(IUiCanvas canvas, UiRect track, float ratio, bool enabled)
    {
        float height = Math.Min(7, track.Height);
        Progress(canvas, new UiRect(track.X, track.Y + (track.Height - height) / 2, track.Width, height), ratio, enabled);
    }

    public override void Progress(IUiCanvas canvas, UiRect bounds, float ratio, bool enabled)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        ratio = float.IsFinite(ratio) ? Math.Clamp(ratio, 0, 1) : 0;
        canvas.Fill(bounds, BorderDark);
        UiRect track = bounds.Inset(1);
        canvas.Fill(track, new UiColor(12, 30, 45, 230));
        UiRect fill = new(track.X, track.Y, track.Width * ratio, track.Height);
        if (!fill.IsEmpty)
        {
            Gradient(canvas, fill, enabled ? new UiColor(126, 240, 250) : Muted, enabled ? new UiColor(24, 132, 165) : new UiColor(77, 94, 109));
            if (enabled)
            {
                canvas.Line(fill.X, fill.Y, Math.Max(fill.X, fill.Right - 1), fill.Y, new UiColor(203, 255, 255, 210), 1);
                canvas.Fill(new UiRect(Math.Max(fill.X, fill.Right - 2), fill.Y, Math.Min(2, fill.Width), fill.Height), new UiColor(172, 251, 255, 150));
            }
        }
    }

    private static void DrawFeather(IUiCanvas canvas, UiRect bounds)
    {
        UiColor ink = new(158, 180, 206, 20);
        float height = bounds.Height * 0.72f;
        float startX = bounds.X + bounds.Width * 0.12f;
        float startY = bounds.Bottom - bounds.Height * 0.08f;
        float previousLeftX = startX;
        float previousRightX = startX;
        float previousY = startY;
        for (int index = 1; index <= 80; index++)
        {
            float amount = index / 80f;
            float x = startX + bounds.Width * 0.77f * amount;
            float y = startY - height * amount;
            float spread = MathF.Pow(Math.Max(0, MathF.Sin(amount * MathF.PI)), 1.4f) * bounds.Width * 0.3f;
            float leftX = x - spread;
            float rightX = x + spread * 0.5f;
            float tipY = y - height * 0.085f * MathF.Sin(amount * MathF.PI);
            canvas.Line(x, y, leftX, tipY, ink, 0.7f);
            canvas.Line(x, y, rightX, tipY, ink, 0.7f);
            canvas.Line(previousLeftX, previousY, leftX, tipY, ink, 0.7f);
            canvas.Line(previousRightX, previousY, rightX, tipY, ink, 0.7f);
            previousLeftX = leftX;
            previousRightX = rightX;
            previousY = tipY;
        }
    }

    private static void Gradient(IUiCanvas canvas, UiRect bounds, UiColor top, UiColor bottom)
    {
        const int bands = 32;
        for (int index = 0; index < bands; index++)
        {
            float amount = index / (float)(bands - 1);
            UiColor color = new((byte)(top.R + (bottom.R - top.R) * amount), (byte)(top.G + (bottom.G - top.G) * amount), (byte)(top.B + (bottom.B - top.B) * amount), (byte)(top.A + (bottom.A - top.A) * amount));
            float y = bounds.Y + bounds.Height * index / bands;
            float next = bounds.Y + bounds.Height * (index + 1) / bands;
            canvas.Fill(new UiRect(bounds.X, y, bounds.Width, next - y), color);
        }
    }
}
