namespace Memoria.FF8.Ui;

/// <summary>A noninteractive message sized by the active renderer and centered relative to its viewport.</summary>
public sealed class UiTextPopup : UiElement
{
    public string Text { get; set; } = string.Empty;
    public float FontSize { get; set; } = 25;
    public float CenterX { get; set; } = 0.5f;
    public float CenterY { get; set; } = 0.5f;
    public float MinimumWidth { get; set; } = 300;
    public float MaximumWidth { get; set; } = 870;
    public UiThickness Padding { get; set; } = new(24, 15);

    public override UiElement? HitTest(float x, float y)
    {
        return null;
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        float maximumWidth = Math.Min(Bounds.Width, MaximumWidth);
        float maximumTextWidth = maximumWidth - Padding.Horizontal;
        if (maximumTextWidth <= 0 || Bounds.Height <= Padding.Vertical)
        {
            return;
        }

        UiSize measured = canvas.MeasureText(Text, FontSize, maximumTextWidth);
        float width = Math.Clamp(MathF.Ceiling(measured.Width) + Padding.Horizontal, Math.Min(MinimumWidth, maximumWidth), maximumWidth);
        float textWidth = width - Padding.Horizontal;
        // Cropped glyph widths and wrapping advances can differ, so measure at the final drawing width.
        measured = canvas.MeasureText(Text, FontSize, textWidth);
        float height = Math.Min(Bounds.Height, MathF.Ceiling(measured.Height) + Padding.Vertical);
        float left = Math.Clamp(Bounds.X + Bounds.Width * CenterX - width / 2, Bounds.X, Bounds.Right - width);
        float top = Math.Clamp(Bounds.Y + Bounds.Height * CenterY - height / 2, Bounds.Y, Bounds.Bottom - height);
        UiRect panel = new(left, top, width, height);
        UiRect content = UiLayout.Inset(panel, Padding);
        theme.Panel(canvas, panel);
        canvas.PushClip(content);
        try
        {
            canvas.Text(Text, FontSize, content, theme.Foreground);
        }
        finally
        {
            canvas.PopClip();
        }
    }
}
