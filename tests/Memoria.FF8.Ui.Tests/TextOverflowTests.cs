namespace Memoria.FF8.Ui.Tests;

public sealed class TextOverflowTests
{
    [Fact]
    public void TextLayersSharingBoundsKeepIndependentAnimationAndMeasurements()
    {
        UiTextOverflow overflow = new();
        UiRect bounds = new(0, 0, 100, 30);
        overflow.BeginFrame(TimeSpan.Zero);
        overflow.Layout("First layer", 20, bounds, "Font A", _ => new UiSize(220, 20), UiTextFlow.SingleLine);
        overflow.Layout("Second layer", 20, bounds, "Font B", _ => new UiSize(300, 20), UiTextFlow.SingleLine);
        overflow.BeginFrame(TimeSpan.FromSeconds(3.5));

        UiTextViewport first = overflow.Layout("First layer", 20, bounds, "Font A", _ => throw new InvalidOperationException(), UiTextFlow.SingleLine);
        UiTextViewport second = overflow.Layout("Second layer", 20, bounds, "Font B", _ => throw new InvalidOperationException(), UiTextFlow.SingleLine);

        Assert.Equal(60, first.OffsetX);
        Assert.Equal(60, second.OffsetX);
        Assert.Equal(220, first.Content.Width);
        Assert.Equal(300, second.Content.Width);
    }

    [Theory]
    [InlineData(72, 24, 90, 19.2f)]
    [InlineData(24, 48, 30, 24)]
    [InlineData(6, 8, 7.5f, 6)]
    [InlineData(48, 0, 60, 0)]
    public void HeightFittingPreservesSizeWhenPossibleAndScalesProportionallyWhenConstrained(float size, float height, float lineHeight, float expected)
    {
        Assert.Equal(expected, UiTextOverflow.FitFontSize(size, height, lineHeight), 3);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(72)]
    public void WrappedProseWithoutExplicitBreaksScrollsVerticallyAtTheRequestedFontSize(float size)
    {
        UiTextOverflow overflow = new();
        UiRect bounds = new(0, 0, size * 5, size * 2);
        UiSize Measure(float width)
        {
            Assert.Equal(bounds.Width, width);
            return new UiSize(width, size * 5);
        }
        overflow.BeginFrame(TimeSpan.Zero);
        UiTextViewport first = overflow.Layout("A complete paragraph with several wrapped lines", size, bounds, null, Measure, UiTextFlow.Wrap);
        overflow.BeginFrame(TimeSpan.FromSeconds(3));
        UiTextViewport moving = overflow.Layout("A complete paragraph with several wrapped lines", size, bounds, null, Measure, UiTextFlow.Wrap);
        Assert.Equal(0, first.OffsetY);
        Assert.Equal(0, moving.OffsetX);
        Assert.True(moving.OffsetY > 0);
        Assert.Equal(bounds.Width, moving.WrapWidth);
    }

    [Fact]
    public void SingleLineRowsNeverWrapEvenWhenTwoLinesWouldFit()
    {
        UiTextOverflow overflow = new();
        UiRect bounds = new(0, 0, 100, 60);
        overflow.BeginFrame(TimeSpan.Zero);
        UiTextViewport layout = overflow.Layout("Long row label", 20, bounds, null, width =>
        {
            Assert.True(float.IsPositiveInfinity(width));
            return new UiSize(220, 20);
        }, UiTextFlow.SingleLine);
        overflow.BeginFrame(TimeSpan.FromSeconds(3.5));
        UiTextViewport moving = overflow.Layout("Long row label", 20, bounds, null, _ => throw new InvalidOperationException(), UiTextFlow.SingleLine);
        Assert.Equal(0, layout.OffsetX);
        Assert.Equal(60, moving.OffsetX);
        Assert.Equal(0, moving.OffsetY);
    }

    [Theory]
    [InlineData(UiTextFlow.Wrap)]
    [InlineData(UiTextFlow.SingleLine)]
    public void ExplicitFlowKeepsFittingTextStationary(UiTextFlow flow)
    {
        UiTextOverflow overflow = new();
        UiRect bounds = new(0, 0, 100, 40);
        overflow.BeginFrame(TimeSpan.Zero);
        overflow.Layout("Fits", 20, bounds, null, _ => new UiSize(60, 20), flow);
        overflow.BeginFrame(TimeSpan.FromSeconds(60));
        UiTextViewport layout = overflow.Layout("Fits", 20, bounds, null, _ => throw new InvalidOperationException(), flow);
        Assert.Equal(0, layout.OffsetX);
        Assert.Equal(0, layout.OffsetY);
    }

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
