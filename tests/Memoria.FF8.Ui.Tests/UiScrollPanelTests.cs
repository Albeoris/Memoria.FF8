namespace Memoria.FF8.Ui.Tests;

public sealed class UiScrollPanelTests
{
    [Fact]
    public void OffsetClampsAndTranslatedContentUsesTheSameHitBounds()
    {
        (UiScrollPanel scroll, UiButton first, UiButton last) = Create();
        Assert.Same(first, scroll.HitTest(20, 20));
        Assert.Null(scroll.HitTest(20, 230));
        scroll.Offset = 1000;
        Assert.Equal(200, scroll.Offset);
        Assert.Equal(-190, first.Bounds.Y);
        Assert.Equal(10, last.Bounds.Y);
        Assert.Same(last, scroll.HitTest(20, 20));
        Assert.Same(scroll, scroll.HitTest(105, 20));
        scroll.Offset = -100;
        Assert.Equal(0, scroll.Offset);
        Assert.Equal(10, first.Bounds.Y);
    }

    [Fact]
    public void FocusedDescendantIsRevealedAndUnrelatedElementsDoNotScroll()
    {
        (UiScrollPanel scroll, UiButton first, UiButton last) = Create();
        Assert.True(scroll.EnsureVisible(last));
        Assert.Equal(200, scroll.Offset);
        Assert.False(scroll.EnsureVisible(last));
        Assert.False(scroll.EnsureVisible(new UiButton()));
        Assert.True(scroll.EnsureVisible(first));
        Assert.Equal(0, scroll.Offset);
    }

    [Fact]
    public void ResizeAndContentChangesClampOffsetAndNotifyOwner()
    {
        (UiScrollPanel scroll, _, _) = Create();
        List<float> offsets = [];
        scroll.OffsetChanged = offsets.Add;
        scroll.ScrollBy(80);
        scroll.ContentHeight = 120;
        Assert.Equal(20, scroll.Offset);
        scroll.Arrange(new(10, 10, 100, 140));
        Assert.Equal(0, scroll.Offset);
        Assert.Equal(new[] { 80f, 20f, 0f }, offsets);
        Assert.Throws<ArgumentOutOfRangeException>(() => scroll.ContentHeight = float.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => scroll.Offset = float.PositiveInfinity);
    }

    [Fact]
    public void ScrollbarClicksAndPagingReachTheContentWithoutChangingAssignments()
    {
        (UiScrollPanel scroll, _, _) = Create();
        scroll.Pointer(105, 109);
        Assert.Equal(200, scroll.Offset);
        Assert.True(scroll.Key(UiKey.PageUp));
        Assert.Equal(120, scroll.Offset);
        Assert.False(scroll.Key(UiKey.Confirm));
    }

    [Fact]
    public void DrawClipsChildrenAndLeavesAVisibleScrollbar()
    {
        (UiScrollPanel scroll, _, _) = Create();
        ClipCanvas canvas = new();
        scroll.Draw(canvas, new UiTheme());
        Assert.Empty(canvas.Clips);
        Assert.Contains(canvas.Fills, fill => fill.Bounds.X >= 100 && fill.Bounds.Height == 100);
        Assert.All(canvas.TextClips.Where(clip => !clip.IsEmpty), clip => Assert.Equal(clip, clip.Intersect(new(10, 10, 100, 100))));
    }

    [Fact]
    public void ViewCanFocusHiddenControlsAndRevealsThemBeforeFocusNotification()
    {
        (UiScrollPanel scroll, UiButton first, UiButton last) = Create();
        UiView view = new(scroll);
        view.Resize(100, 100);
        bool visibleOnNotification = false;
        last.FocusChanged = focused => visibleOnNotification = focused && last.Bounds.Y >= scroll.Bounds.Y && last.Bounds.Bottom <= scroll.Bounds.Bottom;
        Assert.True(view.Focus(last));
        Assert.True(visibleOnNotification);
        Assert.Equal(200, scroll.Offset);
        Assert.Same(last, view.FocusedElement);
        Assert.True(view.Focus(first));
        Assert.Equal(0, scroll.Offset);
        view.Key(UiKey.Down);
        Assert.Equal(100, scroll.Offset);
        view.Key(UiKey.Down);
        Assert.Same(last, view.FocusedElement);
        Assert.Equal(200, scroll.Offset);
    }

    private static (UiScrollPanel Scroll, UiButton First, UiButton Last) Create()
    {
        UiButton first = new() { Text = "First" };
        UiButton middle = new() { Text = "Middle" };
        UiButton last = new() { Text = "Last" };
        UiStack content = new() { Gap = 0 };
        content.Children.Add(first);
        content.Children.Add(middle);
        content.Children.Add(last);
        UiScrollPanel scroll = new() { ContentHeight = 300 };
        scroll.Children.Add(content);
        scroll.Arrange(new(10, 10, 100, 100));
        return (scroll, first, last);
    }

    private sealed class ClipCanvas : IUiCanvas
    {
        public Stack<UiRect> Clips { get; } = [];
        public List<(UiRect Bounds, UiColor Color)> Fills { get; } = [];
        public List<UiRect> TextClips { get; } = [];
        public void Fill(UiRect bounds, UiColor color) => Fills.Add((bounds, color));
        public void Text(string text, float fontSize, UiRect bounds, UiColor color) => TextClips.Add(Clips.Peek());
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment) => Text(text, fontSize, bounds, color);
        public void MenuPointer(UiRect bounds)
        {
        }
        public void Image(UiImageSource source, UiRect bounds)
        {
        }
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
        public void PushClip(UiRect bounds) => Clips.Push(Clips.Count == 0 ? bounds : Clips.Peek().Intersect(bounds));
        public void PopClip() => Clips.Pop();
    }
}
