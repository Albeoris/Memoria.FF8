namespace Memoria.FF8.Ui;

/// <summary>Applies a presentation font while preserving explicit per-element font choices.</summary>
internal sealed class UiFontCanvas(IUiCanvas canvas, string fontFamily) : IUiCanvas
{
    public void Fill(UiRect bounds, UiColor color)
    {
        canvas.Fill(bounds, color);
    }

    public void Paper(UiRect bounds, UiColor color)
    {
        canvas.Paper(bounds, color);
    }

    public bool ModPreview(string url, UiRect bounds)
    {
        return canvas.ModPreview(url, bounds);
    }

    public void Image(UiImageSource source, UiRect bounds)
    {
        canvas.Image(source, bounds);
    }

    public void ImageStretched(UiImageSource source, UiRect bounds)
    {
        canvas.ImageStretched(source, bounds);
    }

    public void MenuPointer(UiRect bounds)
    {
        canvas.MenuPointer(bounds);
    }

    public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
    {
        canvas.Line(x1, y1, x2, y2, color, thickness);
    }

    public void PushClip(UiRect bounds)
    {
        canvas.PushClip(bounds);
    }

    public void PopClip()
    {
        canvas.PopClip();
    }


    public UiSize MeasureText(string text, float fontSize, float maximumWidth)
    {
        return canvas.MeasureText(text, fontSize, maximumWidth, fontFamily);
    }

    public UiSize MeasureText(string text, float fontSize, float maximumWidth, string? explicitFamily)
    {
        return canvas.MeasureText(text, fontSize, maximumWidth, explicitFamily ?? fontFamily);
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color)
    {
        canvas.Text(text, fontSize, bounds, color, fontFamily);
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? explicitFamily)
    {
        canvas.Text(text, fontSize, bounds, color, explicitFamily ?? fontFamily);
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
    {
        canvas.Text(text, fontSize, bounds, color, fontFamily, verticalAlignment, UiTextFlow.Auto);
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment, UiTextFlow flow)
    {
        canvas.Text(text, fontSize, bounds, color, fontFamily, verticalAlignment, flow);
    }

    public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? explicitFamily, UiAlignment verticalAlignment, UiTextFlow flow)
    {
        canvas.Text(text, fontSize, bounds, color, explicitFamily ?? fontFamily, verticalAlignment, flow);
    }

    public void AlignedText(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment horizontalAlignment)
    {
        float offset = 0;
        if (horizontalAlignment is UiAlignment.End or UiAlignment.Center)
        {
            offset = Math.Max(0, bounds.Width - MeasureText(text, fontSize, bounds.Width).Width);
            if (horizontalAlignment == UiAlignment.Center)
            {
                offset /= 2;
            }
        }
        Text(text, fontSize, new UiRect(bounds.X + offset, bounds.Y, bounds.Width - offset, bounds.Height), color);
    }

    public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x != 0 || y != 0)
                {
                    Text(text, fontSize, new UiRect(bounds.X + x * thickness, bounds.Y + y * thickness, bounds.Width, bounds.Height), outline, UiAlignment.Center, UiTextFlow.SingleLine);
                }
            }
        }
        Text(text, fontSize, bounds, color, UiAlignment.Center, UiTextFlow.SingleLine);
    }
}
