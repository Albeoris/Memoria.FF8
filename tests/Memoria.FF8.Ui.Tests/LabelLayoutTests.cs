namespace Memoria.FF8.Ui.Tests;

public sealed class LabelLayoutTests
{
    [Theory]
    [InlineData(6, 1)]
    [InlineData(24, 1)]
    [InlineData(48, 1)]
    [InlineData(48, 1.5f)]
    [InlineData(48, 2)]
    public void AutoRowsUseWrappedGlyphMeasurementsWithinTheirOwnColumn(float size, float outputScale)
    {
        UiGrid grid = new() { Gap = 0 };
        grid.ColumnDefinitions.Add(UiGridLength.Pixel(100));
        grid.ColumnDefinitions.Add(UiGridLength.Star());
        grid.RowDefinitions.Add(UiGridLength.Auto);
        grid.RowDefinitions.Add(UiGridLength.Star());
        UiLabel label = new() { Text = "abcdefghijklmnopqrst", FontSize = size, TextFlow = UiTextFlow.Wrap, GridColumn = 0 };
        grid.Children.Add(label);
        UiElement content = new() { GridRow = 1 };
        grid.Children.Add(content);
        UiView view = new(grid) { DesignWidth = 300, DesignHeight = 500 };
        view.Resize(300 * outputScale, 500 * outputScale);
        MeasuringCanvas canvas = new();

        view.Draw(canvas, new UiTheme());

        float expectedHeight = MathF.Ceiling(20 * size / 2 / 100) * size;
        Assert.Equal(expectedHeight, label.Bounds.Height);
        Assert.Equal(expectedHeight, content.Bounds.Y);
        Assert.Equal(UiTextFlow.Wrap, canvas.Flow);
        Assert.Equal(size * outputScale, canvas.FontSize);
        Assert.Equal(100 * outputScale, canvas.TextBounds.Width);

        label.Text = "abcd";
        view.Draw(canvas, new UiTheme());
        Assert.Equal(MathF.Ceiling(4 * size / 2 / 100) * size, label.Bounds.Height);
    }

    [Fact]
    public void SingleLineLabelUsesOneMeasuredLineInsteadOfWrappedDesiredHeight()
    {
        UiGrid grid = new() { Gap = 0 };
        grid.RowDefinitions.Add(UiGridLength.Auto);
        grid.RowDefinitions.Add(UiGridLength.Star());
        UiLabel label = new() { Text = new string('x', 100), FontSize = 24, TextFlow = UiTextFlow.SingleLine };
        grid.Children.Add(label);
        grid.Children.Add(new UiElement { GridRow = 1 });
        UiView view = new(grid);
        view.Resize(100, 300);
        MeasuringCanvas canvas = new();

        view.Draw(canvas, new UiTheme());

        Assert.Equal(24, label.Bounds.Height);
        Assert.Equal(UiTextFlow.SingleLine, canvas.Flow);
    }

    private sealed class MeasuringCanvas : IUiCanvas
    {
        public UiTextFlow Flow { get; private set; }
        public float FontSize { get; private set; }
        public UiRect TextBounds { get; private set; }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            float width = text.Length * fontSize / 2;
            float lines = float.IsPositiveInfinity(maximumWidth) ? 1 : Math.Max(1, MathF.Ceiling(width / maximumWidth));
            return new UiSize(Math.Min(width, maximumWidth), lines * fontSize);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment, UiTextFlow flow)
        {
            Flow = flow;
            FontSize = fontSize;
            TextBounds = bounds;
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
        }
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
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
        public void PushClip(UiRect bounds)
        {
        }
        public void PopClip()
        {
        }
    }
}
