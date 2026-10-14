using Memoria.FF8.Ui;

namespace Memoria.FF8.Ui.Tests;

public sealed class UiTests
{
    [Fact]
    public void RectangleUsesHalfOpenHitBoundariesAndEmptyIntersections()
    {
        UiRect bounds = new(10, 20, 30, 40);
        Assert.True(bounds.Contains(10, 20));
        Assert.True(bounds.Contains(39.9f, 59.9f));
        Assert.False(bounds.Contains(40, 20));
        Assert.False(bounds.Contains(10, 60));
        Assert.True(bounds.Intersect(new UiRect(40, 20, 10, 10)).IsEmpty);
        Assert.False(new UiRect(0, 0, -1, 10).Contains(0, 0));
    }

    [Fact]
    public void FocusSkipsDisabledHiddenClippedAndDisabledDescendants()
    {
        UiPanel root = new() { Bounds = new UiRect(0, 0, 100, 100) };
        UiButton first = Button(0);
        UiButton disabled = Button(20);
        disabled.Enabled = false;
        UiButton hidden = Button(40);
        hidden.Visible = false;
        UiButton clipped = Button(200);
        UiPanel disabledPanel = new() { Bounds = new UiRect(0, 0, 100, 100), Enabled = false };
        disabledPanel.Children.Add(Button(60));
        UiButton last = Button(80);
        root.Children.Add(first);
        root.Children.Add(disabled);
        root.Children.Add(hidden);
        root.Children.Add(clipped);
        root.Children.Add(disabledPanel);
        root.Children.Add(last);
        UiView view = new(root);

        Assert.Same(first, view.FocusedElement);
        Assert.False(view.Focus(disabled));
        Assert.True(view.Key(UiKey.Tab));
        Assert.Same(last, view.FocusedElement);
        last.Enabled = false;
        Assert.Same(first, view.FocusedElement);
        Assert.False(last.IsFocused);
    }

    [Fact]
    public void KeyboardPagesScrollSelectedRowIntoViewAndClampsAtEnds()
    {
        UiList list = new() { Bounds = new UiRect(0, 0, 100, 60), RowHeight = 20, Items = Enumerable.Range(0, 10).Select(index => index.ToString()).ToList() };
        List<int> changes = new();
        list.SelectionChanged = changes.Add;
        UiView view = new(list);

        view.Key(UiKey.PageDown);
        Assert.Equal(3, list.SelectedIndex);
        Assert.Equal(1, list.ScrollOffset);
        view.Key(UiKey.PageDown);
        view.Key(UiKey.PageDown);
        Assert.Equal(9, list.SelectedIndex);
        Assert.Equal(7, list.ScrollOffset);
        view.Key(UiKey.Down);
        Assert.Equal(new[] { 3, 6, 9 }, changes);
        view.Key(UiKey.PageUp);
        view.Key(UiKey.PageUp);
        Assert.Equal(3, list.SelectedIndex);
        Assert.Equal(3, list.ScrollOffset);
        list.Items.Clear();
        Assert.Equal(-1, list.SelectedIndex);
        Assert.Equal(0, list.ScrollOffset);
        Assert.False(list.Key(UiKey.Confirm));
    }

    [Fact]
    public void PointerMapsLetterboxedDesignCoordinatesAndActivatesOncePerPress()
    {
        int count = 0;
        UiPanel root = new() { Bounds = new UiRect(0, 0, 100, 100) };
        UiButton button = new() { Bounds = new UiRect(10, 10, 30, 20), Click = () => count++ };
        root.Children.Add(button);
        UiView view = new(root) { DesignWidth = 100, DesignHeight = 100 };
        view.Resize(400, 200);

        view.Pointer(120, 20, true);
        view.Pointer(121, 21, true);
        Assert.Equal(1, count);
        view.Pointer(120, 20, false);
        view.Pointer(180, 20, true);
        Assert.Equal(1, count);
        view.Pointer(180, 20, false);
        view.Pointer(120, 20, true);
        Assert.Equal(2, count);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Contains(new UiRect(120, 20, 60, 40), canvas.Clips);
    }

    [Fact]
    public void RenderingBalancesIntersectingClipsAndProceduralThemeIsGrayscale()
    {
        UiPanel root = new() { Bounds = new UiRect(0, 0, 100, 100), Caption = "Menu" };
        root.Children.Add(new UiLabel { Bounds = new UiRect(80, 80, 100, 40), Text = "Clipped" });
        RecordingCanvas canvas = new();
        new UiView(root).Draw(canvas, new UiTheme());

        Assert.Equal(0, canvas.ClipDepth);
        Assert.Contains(new UiRect(80, 80, 20, 20), canvas.Clips);
        Assert.Equal(24, canvas.Fills.Count);
        Assert.All(canvas.Fills, fill =>
        {
            Assert.Equal(fill.Color.R, fill.Color.G);
            Assert.Equal(fill.Color.G, fill.Color.B);
        });
        Assert.True(canvas.Fills[0].Color.R > canvas.Fills[^1].Color.R);
        Assert.Equal(8, canvas.Lines);
        Assert.Contains("Menu", canvas.Texts);
    }

    [Fact]
    public void ClippingIsRestoredWhenAChildRendererThrows()
    {
        UiElement root = new() { Bounds = new UiRect(0, 0, 100, 100) };
        root.Children.Add(new ThrowingElement { Bounds = root.Bounds });
        RecordingCanvas canvas = new();
        Assert.Throws<InvalidOperationException>(() => root.Draw(canvas, new UiTheme()));
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void HitTestingUsesTopmostChildAndParentClip()
    {
        UiElement root = new() { Bounds = new UiRect(0, 0, 100, 100) };
        UiButton lower = new() { Bounds = new UiRect(0, 0, 100, 100) };
        UiButton upper = new() { Bounds = new UiRect(50, 50, 100, 100) };
        root.Children.Add(lower);
        root.Children.Add(upper);
        Assert.Same(upper, root.HitTest(50, 50));
        Assert.Null(root.HitTest(110, 60));
        upper.Enabled = false;
        Assert.Same(lower, root.HitTest(50, 50));
    }

    [Fact]
    public void GridAndStackArrangeNestedControlsWithoutNegativeSizes()
    {
        UiGrid grid = new() { Columns = 2, Gap = 10 };
        UiStack stack = new() { Gap = 4 };
        stack.Children.Add(new UiButton());
        stack.Children.Add(new UiButton());
        grid.Children.Add(stack);
        grid.Children.Add(new UiButton());
        grid.Arrange(new UiRect(0, 0, 210, 100));
        Assert.Equal(new UiRect(0, 0, 100, 100), stack.Bounds);
        Assert.Equal(new UiRect(0, 52, 100, 48), stack.Children[1].Bounds);
        grid.Arrange(new UiRect(0, 0, 1, 1));
        Assert.Equal(0, stack.Bounds.Width);
    }

    [Fact]
    public void SliderAndToggleRouteKeyboardAndOnlyNotifyOnInputChanges()
    {
        UiSlider slider = new() { Bounds = new UiRect(0, 0, 100, 40), Max = 100, Step = 25, Value = 50 };
        List<float> values = new();
        slider.ValueChanged = values.Add;
        slider.Value = 75;
        Assert.Empty(values);
        slider.Key(UiKey.Right);
        slider.Key(UiKey.Right);
        slider.Key(UiKey.Left);
        Assert.Equal(new[] { 100f, 75f }, values);
        slider.Pointer(24, 20);
        Assert.Equal(0, slider.Value);
        slider.Enabled = false;
        Assert.False(slider.Key(UiKey.Right));
        slider.Pointer(99, 20);
        Assert.Equal(0, slider.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => slider.Step = 0);

        UiToggle toggle = new() { Bounds = new UiRect(0, 0, 100, 30) };
        List<bool> toggles = new();
        toggle.ValueChanged = toggles.Add;
        toggle.Key(UiKey.Right);
        toggle.Key(UiKey.Right);
        toggle.Key(UiKey.Left);
        toggle.Pointer(10, 10);
        Assert.Equal(new[] { true, false, true }, toggles);
    }

    [Fact]
    public void ProgressBarClampsValuesAndDoesNotJoinFocusNavigation()
    {
        UiProgressBar bar = new() { Bounds = new UiRect(0, 0, 100, 10), Max = 100, Value = 150 };
        RecordingCanvas canvas = new();
        UiView view = new(bar);
        view.Draw(canvas, new UiTheme());
        Assert.Null(view.FocusedElement);
        Assert.Equal(new UiRect(1, 1, 98, 8), canvas.Fills[^1].Bounds);
        bar.Value = -1;
        bar.Draw(canvas, new UiTheme());
        Assert.Equal(0, canvas.Fills[^1].Bounds.Width);
        bar.Value = float.NaN;
        bar.Draw(canvas, new UiTheme());
        Assert.Equal(0, canvas.Fills[^1].Bounds.Width);
    }

    private static UiButton Button(float y)
    {
        return new UiButton { Bounds = new UiRect(0, y, 100, 20) };
    }

    private sealed class ThrowingElement : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            throw new InvalidOperationException("Deliberate test failure.");
        }
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        private readonly Stack<UiRect> _clips = new();
        public int ClipDepth => _clips.Count;
        public int Lines { get; private set; }
        public List<UiRect> Clips { get; } = new();
        public List<(UiRect Bounds, UiColor Color)> Fills { get; } = new();
        public List<string> Texts { get; } = new();

        public void Fill(UiRect bounds, UiColor color)
        {
            Fills.Add((bounds, color));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add(text);
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            Lines++;
        }

        public void PushClip(UiRect bounds)
        {
            UiRect clip = _clips.Count == 0 ? bounds : _clips.Peek().Intersect(bounds);
            _clips.Push(clip);
            Clips.Add(clip);
        }

        public void PopClip()
        {
            _clips.Pop();
        }
    }
}
