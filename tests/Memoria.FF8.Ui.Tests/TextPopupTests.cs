namespace Memoria.FF8.Ui.Tests;

public sealed class TextPopupTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(37.5f)]
    public void ExplicitLinesAndScaledFontReceiveTheirMeasuredHeight(float fontSize)
    {
        var popup = new UiTextPopup { Text = "WWWW\nSecond line\nThird line", FontSize = fontSize };
        var canvas = new MeasuringCanvas();
        popup.Arrange(new UiRect(0, 0, 960, 672));

        popup.Draw(canvas, new UiTheme());

        var drawn = Assert.Single(canvas.Texts);
        Assert.Equal(fontSize, drawn.FontSize);
        Assert.Equal(MathF.Ceiling(fontSize * 3), drawn.Bounds.Height);
        Assert.Equal(480, drawn.Bounds.X + drawn.Bounds.Width / 2);
        Assert.Equal(336, drawn.Bounds.Y + drawn.Bounds.Height / 2);
    }

    [Fact]
    public void WidthAndWrappedHeightUseRendererMetricsRatherThanCharacterCount()
    {
        var popup = new UiTextPopup { Text = new string('W', 80) };
        var canvas = new MeasuringCanvas();
        popup.Arrange(new UiRect(0, 0, 960, 672));

        popup.Draw(canvas, new UiTheme());

        var drawn = Assert.Single(canvas.Texts);
        Assert.Equal(822, drawn.Bounds.Width);
        Assert.Equal(75, drawn.Bounds.Height);
        Assert.Equal(2, canvas.Measurements.Count);
        Assert.Equal(drawn.Bounds.Width, canvas.Measurements[^1].Width);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void PanelStaysInsideViewportAtNativeEdges(float centerX, float centerY)
    {
        var popup = new UiTextPopup { Text = new string('W', 1600), CenterX = centerX, CenterY = centerY };
        var canvas = new MeasuringCanvas();
        UiRect viewport = new(10, 20, 640, 448);
        popup.Arrange(viewport);

        popup.Draw(canvas, new UiTheme());

        Assert.All(canvas.Clips, clip => Assert.Equal(clip, clip.Intersect(viewport)));
        var drawn = Assert.Single(canvas.Texts);
        Assert.Equal(592, drawn.Bounds.Width);
        Assert.Equal(418, drawn.Bounds.Height);
    }

    [Fact]
    public void LetterboxedViewMeasuresAndDrawsInTheSameCoordinateSpace()
    {
        var popup = new UiTextPopup { Text = "WWWW\nWWWW" };
        var view = new UiView(popup) { DesignWidth = 960, DesignHeight = 672 };
        var canvas = new MeasuringCanvas();
        view.Resize(1920, 1080);

        view.Draw(canvas, new UiTheme());

        float scale = 1080f / 672;
        var drawn = Assert.Single(canvas.Texts);
        Assert.Equal(25 * scale, drawn.FontSize);
        Assert.Equal(50 * scale, drawn.Bounds.Height, 3);
        Assert.Equal(960, drawn.Bounds.X + drawn.Bounds.Width / 2, 3);
        Assert.Equal(540, drawn.Bounds.Y + drawn.Bounds.Height / 2, 3);
        Assert.Equal(drawn.Bounds.Width, canvas.Measurements[^1].Width, 3);
    }

    private sealed class MeasuringCanvas : IUiCanvas
    {
        public List<(float FontSize, float Width)> Measurements { get; } = [];
        public List<(float FontSize, UiRect Bounds)> Texts { get; } = [];
        public List<UiRect> Clips { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            Measurements.Add((fontSize, maximumWidth));
            float width = 0;
            int lines = 0;
            foreach (string line in text.Split('\n'))
            {
                float advance = line.Sum(character => character == 'W' ? fontSize : fontSize / 4);
                width = Math.Max(width, Math.Min(advance, maximumWidth));
                lines += Math.Max(1, (int)MathF.Ceiling(advance / maximumWidth));
            }
            return new UiSize(width, lines * fontSize);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((fontSize, bounds));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void PushClip(UiRect bounds)
        {
            Clips.Add(bounds);
        }

        public void PopClip()
        {
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
    }
}
