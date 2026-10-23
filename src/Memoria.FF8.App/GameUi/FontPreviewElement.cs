using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed class FontPreviewElement(string fontFamily, float size) : UiElement
{
    internal string FontFamily { get; } = fontFamily;
    internal float FontSize { get; } = size;

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        canvas.Text("Aa \u0411\u0431 0123 !?", FontSize, Bounds, theme.Foreground, FontFamily);
    }
}
