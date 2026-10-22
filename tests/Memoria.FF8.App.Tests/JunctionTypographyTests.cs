using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionTypographyTests
{
    [Theory]
    [InlineData(24, 100)]
    [InlineData(20, 125)]
    [InlineData(32, 150)]
    public void Entering_a_junction_section_preserves_the_rendered_tab_size(int fontSize, int textScale)
    {
        UiPreferences preferences = new() { UiFontSize = fontSize, TextScale = textScale };
        MenuController menu = new();
        menu.ConnectPreferences(() => preferences, _ => { });
        menu.Open("junction", demo: true);
        menu.Data.Settings["Text scale"] = textScale + "%";
        float originalSize = RenderOffTab(menu);
        foreach (string destination in new[] { "junction-options", "junction-off", "junction-auto", "junction-magic", "junction-stock" })
        {
            menu.Open(destination, demo: true);
            menu.Data.Settings["Text scale"] = textScale + "%";
            Assert.Equal(originalSize, RenderOffTab(menu));
        }
    }

    private static float RenderOffTab(MenuController menu)
    {
        RecordingCanvas canvas = new();
        UiView view = menu.BuildView();
        view.Resize(1920, 1080);
        view.Draw(canvas, menu.CreateTheme());
        float size = Assert.Single(canvas.Texts, text => text.Text == "Off").Size;
        if (menu.TextScale <= 1.1f)
        {
            var junction = Assert.Single(canvas.Texts, text => text.Text == "Junction" && text.Size == size);
            Assert.True(canvas.MeasureText(junction.Text, junction.Size, float.PositiveInfinity).Width <= junction.Bounds.Width);
        }
        return size;
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(string Text, float Size, UiRect Bounds)> Texts { get; } = [];

        public void Fill(UiRect bounds, UiColor color)
        {
        }
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
        public void MenuPointer(UiRect bounds)
        {
        }
        public void Image(UiImageSource source, UiRect bounds)
        {
        }
        public void PushClip(UiRect bounds)
        {
        }
        public void PopClip()
        {
        }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return new UiSize(Math.Min(maximumWidth, text.Length * fontSize * 0.5f), fontSize * 1.2f);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, fontSize, bounds));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }
    }
}
