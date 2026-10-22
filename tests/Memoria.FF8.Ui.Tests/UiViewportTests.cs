using Memoria.FF8.Ui;

namespace Memoria.FF8.Ui.Tests;

public sealed class UiViewportTests
{
    [Theory]
    [InlineData(200, 100, 1)]
    [InlineData(100, 200, 1)]
    [InlineData(400, 200, 2)]
    [InlineData(200, 400, 2)]
    public void Expand_anchors_controls_to_viewport_edges_and_uniformly_scales_drawing(float width, float height, float scale)
    {
        UiAnchorPanel root = new() { Padding = 5 };
        UiButton first = EdgeButton(UiAlignment.Start);
        UiButton last = EdgeButton(UiAlignment.End);
        root.Children.Add(first);
        root.Children.Add(last);
        // Labels bind measurement during Draw, which must retain the expanded layout.
        root.Children.Add(new UiLabel { Text = "Measured", Height = 10, VerticalAlignment = UiAlignment.Center });
        root.Children.Add(new DrawingProbe());
        UiView view = new(root) { DesignWidth = 100, DesignHeight = 100, ViewportMode = UiViewportMode.Expand };
        view.Resize(width, height);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        Assert.Equal(new UiRect(0, 0, width / scale, height / scale), root.Bounds);
        Assert.Equal(5, first.Bounds.X);
        Assert.Equal(5, first.Bounds.Y);
        Assert.Equal(width / scale - 5, last.Bounds.Right);
        Assert.Equal(height / scale - 5, last.Bounds.Bottom);
        Assert.Equal(new UiRect(5 * scale, 5 * scale, width - 10 * scale, height - 10 * scale), Assert.Single(canvas.Fills));
        var text = Assert.Single(canvas.Texts, text => text.Text == "Probe");
        Assert.Equal(12 * scale, text.FontSize);
        Assert.Equal(new UiRect(7 * scale, 8 * scale, 9 * scale, 10 * scale), text.Bounds);
        Assert.Equal((7 * scale, 8 * scale, 16 * scale, 18 * scale, 2 * scale), Assert.Single(canvas.Lines));
    }

    [Fact]
    public void Pointer_mapping_follows_edge_controls_after_resize_and_mode_changes()
    {
        UiAnchorPanel root = new() { Padding = 5 };
        int clicks = 0;
        UiButton button = EdgeButton(UiAlignment.End);
        button.Click = () => clicks++;
        root.Children.Add(button);
        UiView view = new(root) { DesignWidth = 100, DesignHeight = 100, ViewportMode = UiViewportMode.Expand };
        view.Resize(400, 200);
        Click(view, 380, 180);
        Assert.Equal(1, clicks);
        Assert.Same(button, view.FocusedElement);

        view.Resize(200, 400);
        Click(view, 180, 380);
        Assert.Equal(2, clicks);
        view.ViewportMode = UiViewportMode.Fit;
        Click(view, 180, 380);
        Assert.Equal(2, clicks);
        Click(view, 180, 280);
        Assert.Equal(3, clicks);
        view.ViewportMode = UiViewportMode.Expand;
        Click(view, 180, 380);
        Assert.Equal(4, clicks);
    }

    [Theory]
    [InlineData(400, 200, 100, 0)]
    [InlineData(200, 400, 0, 100)]
    public void Default_fit_preserves_design_bounds_and_centers_drawing(float width, float height, float offsetX, float offsetY)
    {
        UiAnchorPanel root = new() { Padding = 5 };
        root.Children.Add(new DrawingProbe());
        UiView view = new(root) { DesignWidth = 100, DesignHeight = 100 };
        view.Resize(width, height);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        Assert.Equal(UiViewportMode.Fit, view.ViewportMode);
        Assert.Equal(new UiRect(0, 0, 100, 100), root.Bounds);
        Assert.Equal(new UiRect(offsetX + 10, offsetY + 10, 180, 180), Assert.Single(canvas.Fills));
        Assert.Equal(new UiRect(offsetX + 14, offsetY + 16, 18, 20), Assert.Single(canvas.Texts).Bounds);
        Assert.Equal((offsetX + 14, offsetY + 16, offsetX + 32, offsetY + 36, 4f), Assert.Single(canvas.Lines));
    }

    private static UiButton EdgeButton(UiAlignment alignment)
    {
        return new UiButton { Width = 20, Height = 20, HorizontalAlignment = alignment, VerticalAlignment = alignment, DrawBackground = false, HighlightSelection = false };
    }

    private static void Click(UiView view, float x, float y)
    {
        view.Pointer(x, y, false);
        view.Pointer(x, y, true);
    }

    private sealed class DrawingProbe : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            canvas.Fill(Bounds, theme.Foreground);
            canvas.Text("Probe", 12, new UiRect(7, 8, 9, 10), theme.Foreground);
            canvas.Line(7, 8, 16, 18, theme.Foreground, 2);
        }

        public override UiElement? HitTest(float x, float y)
        {
            return null;
        }
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<UiRect> Fills { get; } = [];
        public List<(string Text, float FontSize, UiRect Bounds)> Texts { get; } = [];
        public List<(float X1, float Y1, float X2, float Y2, float Thickness)> Lines { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return new UiSize(Math.Min(maximumWidth, text.Length * fontSize / 2), fontSize);
        }

        public void Fill(UiRect bounds, UiColor color)
        {
            Fills.Add(bounds);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, fontSize, bounds));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            Lines.Add((x1, y1, x2, y2, thickness));
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
    }
}
