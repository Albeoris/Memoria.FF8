using Memoria.FF8.Ui;

namespace Memoria.FF8.Ui.Tests;

public sealed class UiOverflowArrowsTests
{
    [Fact]
    public void Arrows_reserve_stable_bands_and_blink_without_moving_content()
    {
        double seconds = 0;
        UiOverflowArrows arrows = new() { ShowUp = true, ShowDown = true, ArrowHeight = 20, AnimationSeconds = () => seconds };
        UiElement child = new();
        arrows.Children.Add(child);
        arrows.Arrange(new UiRect(10, 20, 200, 140));
        UiTheme theme = new();
        Canvas canvas = new();

        arrows.Draw(canvas, theme);

        Assert.Equal(new UiRect(10, 40, 200, 100), child.Bounds);
        Assert.All(canvas.Fills, fill => Assert.Equal(theme.Muted, fill.Color));
        Assert.All(canvas.Fills, fill => Assert.Equal(110, fill.Bounds.X + fill.Bounds.Width / 2));
        Assert.Contains(canvas.Fills, fill => fill.Bounds.Bottom <= child.Bounds.Y);
        Assert.Contains(canvas.Fills, fill => fill.Bounds.Y >= child.Bounds.Bottom);
        seconds = 2d / 60;
        canvas.Fills.Clear();
        arrows.ShowUp = false;
        arrows.Draw(canvas, theme);
        Assert.All(canvas.Fills, fill => Assert.Equal(theme.Foreground, fill.Color));
        Assert.All(canvas.Fills, fill => Assert.True(fill.Bounds.Y >= child.Bounds.Bottom));
        Assert.Equal(arrows.ContentBounds, child.Bounds);
    }

    private sealed class Canvas : IUiCanvas
    {
        public List<(UiRect Bounds, UiColor Color)> Fills { get; } = new();
        public void Fill(UiRect bounds, UiColor color)
        {
            Fills.Add((bounds, color));
        }
        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
        }
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
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
