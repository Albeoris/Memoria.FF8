using Memoria.FF8.Ui;

namespace Memoria.FF8.Ui.Tests;

public sealed class UiTests
{
    [Fact]
    public void Images_follow_anchor_bounds_and_letterbox_scaling_without_changing_identity()
    {
        UiImageSource source = new(UiImageKind.CharacterPortrait, 3);
        UiAnchorPanel root = new();
        root.Children.Add(new UiImage { Source = source, Anchors = new UiAnchors(0.25f, 0, 0.75f, 1) });
        UiView view = new(root) { DesignWidth = 100, DesignHeight = 100 };
        view.Resize(300, 200);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        Assert.Equal((source, new UiRect(100, 0, 100, 200)), Assert.Single(canvas.Images));
        Assert.Equal(new UiRect(100, 25, 100, 150), UiImage.Fit(canvas.Images[0].Bounds, 64, 96));
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void Scrolled_list_images_keep_their_row_identity_and_text_does_not_overlap()
    {
        UiImageSource first = new(UiImageKind.Icon, 1);
        UiImageSource second = new(UiImageKind.Icon, 2);
        UiList list = new() { Items = ["first", "second", "third"], Images = [first, second, null], RowHeight = 30 };
        UiView view = new(list);
        view.Resize(120, 30);
        list.SelectedIndex = 1;
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        var image = Assert.Single(canvas.Images);
        Assert.Equal(second, image.Source);
        Assert.True(image.Bounds.Right < Assert.Single(canvas.AlignedTexts).Bounds.X);
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void Trailing_markers_reserve_space_after_the_label()
    {
        UiImageSource marker = new(UiImageKind.Icon, 192);
        UiList list = new() { Items = ["Fire  100"], TrailingImages = [marker], RowHeight = 30 };
        UiView view = new(list);
        view.Resize(180, 30);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        var image = Assert.Single(canvas.Images);
        Assert.Equal(marker, image.Source);
        Assert.True(Assert.Single(canvas.AlignedTexts).Bounds.Right < image.Bounds.X);
    }

    [Fact]
    public void Selected_rows_forward_centered_text_and_the_native_pointer_through_view_scaling()
    {
        UiList list = new() { Items = ["Refresh"], RowHeight = 40, FontSize = 16 };
        UiView view = new(list) { DesignWidth = 100, DesignHeight = 100 };
        view.Resize(200, 200);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        var text = Assert.Single(canvas.AlignedTexts);
        Assert.Equal(UiAlignment.Center, text.Alignment);
        Assert.Equal(new UiRect(46, 0, 146, 80), text.Bounds);
        UiRect pointer = Assert.Single(canvas.Pointers);
        Assert.Equal(new UiRect(10, 30, 30, 20), pointer);
        Assert.Equal(text.Bounds.Y + text.Bounds.Height / 2, pointer.Y + pointer.Height / 2);
        Assert.Equal(0, canvas.Lines);
    }

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
        UiPanel root = At(new UiPanel(), new UiRect(0, 0, 100, 100));
        UiButton first = Button(0);
        UiButton disabled = Button(20);
        disabled.Enabled = false;
        UiButton hidden = Button(40);
        hidden.Visible = false;
        UiButton clipped = Button(200);
        UiPanel disabledPanel = At(new UiPanel { Enabled = false }, new UiRect(0, 0, 100, 100));
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
        UiList list = At(new UiList { RowHeight = 20, Items = Enumerable.Range(0, 10).Select(index => index.ToString()).ToList() }, new UiRect(0, 0, 100, 60));
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
        UiAnchorPanel root = new();
        UiButton button = new() { Anchors = new UiAnchors(0.1f, 0.1f, 0.4f, 0.3f), Click = () => count++ };
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
        Assert.Contains(canvas.Clips, clip => clip.X == 120 && clip.Y == 20 && clip.Width == 60 && Math.Abs(clip.Height - 40) < 0.001f);
    }

    [Fact]
    public void RenderingBalancesIntersectingClipsAndProceduralThemeIsGrayscale()
    {
        UiPanel root = At(new UiPanel { Caption = "Menu" }, new UiRect(0, 0, 100, 100));
        root.Children.Add(At(new UiLabel { Text = "Clipped" }, new UiRect(80, 80, 100, 40)));
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
        UiElement root = At(new UiElement(), new UiRect(0, 0, 100, 100));
        root.Children.Add(At(new ThrowingElement(), root.Bounds));
        RecordingCanvas canvas = new();
        Assert.Throws<InvalidOperationException>(() => root.Draw(canvas, new UiTheme()));
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void HitTestingUsesTopmostChildAndParentClip()
    {
        UiElement root = At(new UiElement(), new UiRect(0, 0, 100, 100));
        UiButton lower = At(new UiButton(), new UiRect(0, 0, 100, 100));
        UiButton upper = At(new UiButton(), new UiRect(50, 50, 100, 100));
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
        UiSlider slider = At(new UiSlider { Max = 100, Step = 25, Value = 50 }, new UiRect(0, 0, 100, 40));
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

        UiToggle toggle = At(new UiToggle(), new UiRect(0, 0, 100, 30));
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
        UiProgressBar bar = At(new UiProgressBar { Max = 100, Value = 150 }, new UiRect(0, 0, 100, 10));
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

    [Fact]
    public void WeightedGridResizesWithoutTemplateCoordinatesAndRecursivelyArrangesPanelContent()
    {
        UiGrid root = new() { Gap = 0 };
        root.ColumnDefinitions.Add(UiGridLength.Star(2));
        root.ColumnDefinitions.Add(UiGridLength.Star());
        root.RowDefinitions.Add(UiGridLength.Star());
        root.RowDefinitions.Add(UiGridLength.Star(2));
        UiPanel panel = new() { Caption = "Party", CaptionHeight = 20, Padding = 5, GridRowSpan = 2, Margin = new UiThickness(5, 10) };
        UiLabel label = new() { Text = "Squall", Margin = 2 };
        panel.Children.Add(label);
        UiButton button = new() { GridColumn = 1, GridRow = 1 };
        root.Children.Add(panel);
        root.Children.Add(button);
        UiView view = new(root);
        view.Resize(300, 150);
        Assert.Equal(new UiRect(5, 10, 190, 130), panel.Bounds);
        Assert.Equal(new UiRect(12, 37, 176, 96), label.Bounds);
        Assert.Equal(new UiRect(200, 50, 100, 100), button.Bounds);
        view.Resize(600, 300);
        Assert.Equal(new UiRect(5, 10, 390, 280), panel.Bounds);
        Assert.Equal(new UiRect(400, 100, 200, 200), button.Bounds);
        Assert.Same(button, root.HitTest(400, 100));
        Assert.Null(root.HitTest(600, 100));
    }

    [Fact]
    public void GridCombinesPixelsAutoAndStarWithSpanAndAlignedFixedDimensions()
    {
        UiGrid root = new() { Gap = 5 };
        root.ColumnDefinitions.Add(UiGridLength.Pixel(40));
        root.ColumnDefinitions.Add(UiGridLength.Auto);
        root.ColumnDefinitions.Add(UiGridLength.Star());
        root.RowDefinitions.Add(UiGridLength.Star());
        UiElement auto = new() { GridColumn = 1, Width = 60, Height = 20, VerticalAlignment = UiAlignment.Center };
        UiElement star = new() { GridColumn = 2, Width = 30, Height = 10, HorizontalAlignment = UiAlignment.End, VerticalAlignment = UiAlignment.End, Margin = 5 };
        root.Children.Add(auto);
        root.Children.Add(star);
        root.Arrange(new UiRect(0, 0, 300, 100));
        Assert.Equal(new UiRect(45, 40, 60, 20), auto.Bounds);
        Assert.Equal(new UiRect(265, 85, 30, 10), star.Bounds);
        UiElement span = new() { GridColumnSpan = 3, Height = 15 };
        root.Children.Add(span);
        root.Arrange(new UiRect(0, 0, 300, 100));
        Assert.Equal(new UiRect(0, 0, 300, 15), span.Bounds);
    }

    [Fact]
    public void NormalizedAnchorsResizeAndAlignOverlaysInsidePadding()
    {
        UiAnchorPanel root = new() { Padding = 10 };
        UiElement overlay = new() { Anchors = new UiAnchors(0.5f, 0.5f, 1, 1), Width = 40, Height = 20, HorizontalAlignment = UiAlignment.End, VerticalAlignment = UiAlignment.Center, Margin = 5 };
        root.Children.Add(overlay);
        root.Arrange(new UiRect(0, 0, 220, 120));
        Assert.Equal(new UiRect(165, 75, 40, 20), overlay.Bounds);
        root.Arrange(new UiRect(0, 0, 420, 220));
        Assert.Equal(new UiRect(365, 150, 40, 20), overlay.Bounds);
        overlay.Anchors = new UiAnchors(0.8f, 0, 0.2f, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => root.Arrange(new UiRect(0, 0, 100, 100)));
    }

    [Fact]
    public void ExplicitGridRejectsInvalidSpansAndSmallViewportsKeepTrackSizesNonnegative()
    {
        UiGrid grid = new() { Gap = 0 };
        grid.ColumnDefinitions.Add(UiGridLength.Pixel(100));
        grid.ColumnDefinitions.Add(UiGridLength.Auto);
        grid.RowDefinitions.Add(UiGridLength.Star());
        UiElement first = new();
        UiElement second = new() { GridColumn = 1, Width = 100 };
        grid.Children.Add(first);
        grid.Children.Add(second);
        grid.Arrange(new UiRect(0, 0, 50, 20));
        Assert.True(first.Bounds.Width >= 0);
        Assert.True(second.Bounds.Width >= 0);
        Assert.True(second.Bounds.Right <= 50.001f);
        second.GridColumnSpan = 2;
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Arrange(new UiRect(0, 0, 100, 100)));
    }

    [Fact]
    public void ReadOnlyListCanScrollWithoutDrawingSelectionHighlight()
    {
        UiList list = new() { ShowSelection = false, Items = Enumerable.Range(0, 10).Select(index => index.ToString()).ToList(), RowHeight = 20 };
        UiView view = new(list);
        view.Resize(100, 40);
        view.Key(UiKey.PageDown);
        Assert.Equal(1, list.ScrollOffset);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Single(canvas.Fills);
        Assert.Equal(2, canvas.Fills[0].Bounds.Width);
        Assert.Equal(0, canvas.Lines);
    }

    [Fact]
    public void ViewCachesLayoutUntilViewportChangesOrLayoutIsInvalidated()
    {
        CountingElement root = new();
        UiView view = new(root);
        view.Resize(100, 100);
        view.Resize(100, 100);
        view.Draw(new RecordingCanvas(), new UiTheme());
        Assert.Equal(1, root.Arrangements);
        view.InvalidateLayout();
        view.Draw(new RecordingCanvas(), new UiTheme());
        Assert.Equal(2, root.Arrangements);
        view.Resize(200, 100);
        Assert.Equal(3, root.Arrangements);
        view.DesignWidth = 300;
        view.Draw(new RecordingCanvas(), new UiTheme());
        Assert.Equal(4, root.Arrangements);
        Assert.Equal(300, root.Bounds.Width);
    }

    private static UiButton Button(float y)
    {
        return At(new UiButton(), new UiRect(0, y, 100, 20));
    }

    private static T At<T>(T element, UiRect bounds) where T : UiElement
    {
        element.Arrange(bounds);
        return element;
    }

    private sealed class ThrowingElement : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            throw new InvalidOperationException("Deliberate test failure.");
        }
    }

    [Fact]
    public void Offscreen_parent_relative_anchors_preserve_native_positions_and_parent_clipping()
    {
        var root = new UiAnchorPanel();
        var child = new UiLabel { Text = "Timer", Anchors = new UiAnchors(0.25f, 1.05f, 0.75f, 1.2f) };
        root.Children.Add(child);
        root.Arrange(new UiRect(0, 0, 400, 200));
        Assert.Equal(210f, child.Bounds.Y, 3);
        Assert.Null(root.HitTest(110, 220));
        var canvas = new RecordingCanvas();
        root.Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Clips, clip => clip.IsEmpty);
        Assert.Equal(0, canvas.ClipDepth);
    }

    private sealed class CountingElement : UiElement
    {
        public int Arrangements { get; private set; }

        public override void Arrange(UiRect bounds)
        {
            Arrangements++;
            base.Arrange(bounds);
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
        public List<(UiRect Bounds, UiAlignment Alignment)> AlignedTexts { get; } = new();
        public List<UiRect> Pointers { get; } = new();
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = new();

        public void Fill(UiRect bounds, UiColor color)
        {
            Fills.Add((bounds, color));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add(text);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            AlignedTexts.Add((bounds, verticalAlignment));
            Text(text, fontSize, bounds, color);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add((source, bounds));
        }

        public void MenuPointer(UiRect bounds)
        {
            Pointers.Add(bounds);
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
