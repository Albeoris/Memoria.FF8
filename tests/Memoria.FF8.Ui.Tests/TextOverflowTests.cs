namespace Memoria.FF8.Ui.Tests;

public sealed class TextOverflowTests
{
    [Fact]
    public void ScrollPausesAtBothEndsAndRestartsAfterShowingTheLastCharacter()
    {
        Assert.Equal(0, UiTextOverflow.Offset(0, 120, 20));
        Assert.Equal(0, UiTextOverflow.Offset(1.5, 120, 20));
        Assert.Equal(60, UiTextOverflow.Offset(3.5, 120, 20));
        Assert.Equal(120, UiTextOverflow.Offset(5.5, 120, 20));
        Assert.Equal(120, UiTextOverflow.Offset(7.49, 120, 20));
        Assert.Equal(0, UiTextOverflow.Offset(7.5, 120, 20));
    }

    [Fact]
    public void WrappedTextThatFitsRemainsStillAndKeepsWrapping()
    {
        UiTextOverflow overflow = new();
        UiRect bounds = new(0, 0, 100, 50);
        overflow.BeginFrame(TimeSpan.Zero);
        UiTextViewport layout = overflow.Layout("Two fitting lines", 20, bounds, null, width => new UiSize(90, 40));
        overflow.BeginFrame(TimeSpan.FromSeconds(20));
        Assert.Equal(layout, overflow.Layout("Two fitting lines", 20, bounds, null, _ => throw new InvalidOperationException("Remeasured unchanged text")));
        Assert.Equal(100, layout.WrapWidth);
        Assert.Equal(0, layout.OffsetX);
        Assert.Equal(0, layout.OffsetY);
    }

    [Fact]
    public void FixedHeightLineScrollsHorizontallyAndResetsWhenContentOrFontChanges()
    {
        UiTextOverflow overflow = new();
        UiRect bounds = new(0, 0, 100, 25);
        UiSize Measure(float width) => new(float.IsPositiveInfinity(width) ? 220 : 100, float.IsPositiveInfinity(width) ? 20 : 60);
        overflow.BeginFrame(TimeSpan.Zero);
        UiTextViewport first = overflow.Layout("A long help line", 20, bounds, "Font", Measure);
        overflow.BeginFrame(TimeSpan.FromSeconds(3.5));
        UiTextViewport moving = overflow.Layout("A long help line", 20, bounds, "Font", Measure);
        Assert.True(float.IsPositiveInfinity(first.WrapWidth));
        Assert.Equal(0, first.OffsetX);
        Assert.Equal(60, moving.OffsetX);
        Assert.Equal(0, moving.OffsetY);
        Assert.Equal(0, overflow.Layout("Another help line", 20, bounds, "Font", Measure).OffsetX);
        overflow.BeginFrame(TimeSpan.FromSeconds(7));
        Assert.True(overflow.Layout("Another help line", 20, bounds, "Font", Measure).OffsetX > 0);
        Assert.Equal(0, overflow.Layout("Another help line", 20, bounds, "Other font", Measure).OffsetX);
    }

    [Fact]
    public void ExplicitParagraphsScrollVerticallyWithoutRemovingLineBreaks()
    {
        UiTextOverflow overflow = new();
        UiRect bounds = new(0, 0, 100, 40);
        overflow.BeginFrame(TimeSpan.Zero);
        overflow.Layout("Instruction\nLong details", 20, bounds, null, _ => new UiSize(100, 100));
        overflow.BeginFrame(TimeSpan.FromSeconds(3.5));
        UiTextViewport moving = overflow.Layout("Instruction\nLong details", 20, bounds, null, _ => new UiSize(100, 100));
        Assert.Equal(100, moving.WrapWidth);
        Assert.Equal(0, moving.OffsetX);
        Assert.Equal(60, moving.OffsetY);
    }

    [Fact]
    public void HiddenAndResizedTextRestartsAtTheBeginning()
    {
        UiTextOverflow overflow = new();
        UiRect bounds = new(0, 0, 100, 25);
        UiSize Measure(float width) => new(float.IsPositiveInfinity(width) ? 220 : 100, float.IsPositiveInfinity(width) ? 20 : 60);
        overflow.BeginFrame(TimeSpan.Zero);
        overflow.Layout("Help", 20, bounds, null, Measure);
        overflow.BeginFrame(TimeSpan.FromSeconds(1));
        overflow.BeginFrame(TimeSpan.FromSeconds(4));
        Assert.Equal(0, overflow.Layout("Help", 20, bounds, null, Measure).OffsetX);
        overflow.BeginFrame(TimeSpan.FromSeconds(7));
        Assert.Equal(0, overflow.Layout("Help", 20, new UiRect(0, 0, 120, 25), null, Measure).OffsetX);
    }

    [Fact]
    public void SpeedIsIndependentOfFrameRateAndScalesWithFontSize()
    {
        Assert.Equal(UiTextOverflow.Offset(3, 100, 20) * 2, UiTextOverflow.Offset(3, 200, 40));
        Assert.Equal(0, UiTextOverflow.Offset(100, 0, 20));
    }
}
