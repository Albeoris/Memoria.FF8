namespace Memoria.FF8.App.Tests;

public sealed class PresentationFrameRateTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0.5)]
    public void TwoSecondPresentationStallCountsItsEntireElapsedInterval(bool changed, double visualFps)
    {
        PresentationFrameRate counter = new();
        counter.Presented(TimeSpan.Zero, false);
        counter.Presented(TimeSpan.FromSeconds(2), changed);
        Assert.Equal(0.5, counter.PresentationsPerSecond!.Value);
        Assert.Equal(visualFps, counter.FramesPerSecond!.Value);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void CountsCompletedIntervalsAtActualWallClockCadence(int fps)
    {
        PresentationFrameRate counter = new();
        Assert.Null(counter.FramesPerSecond);
        Assert.Null(counter.PresentationsPerSecond);
        counter.Presented(TimeSpan.FromSeconds(10), true);
        Assert.Null(counter.FramesPerSecond);
        Assert.Null(counter.PresentationsPerSecond);
        for (int frame = 1; frame <= fps * 3; frame++)
        {
            counter.Presented(TimeSpan.FromSeconds(10 + frame / (double)fps), true);
        }
        Assert.Equal(fps, counter.FramesPerSecond!.Value, 3);
        Assert.Equal(fps, counter.PresentationsPerSecond!.Value, 3);
    }

    [Fact]
    public void LongFrameIncludesTheEntireStallAndThenRecovers()
    {
        PresentationFrameRate counter = new();
        for (int frame = 0; frame <= 60; frame++)
        {
            counter.Presented(TimeSpan.FromSeconds(frame / 60d), true);
        }
        counter.Presented(TimeSpan.FromSeconds(4), true);
        Assert.Equal(1d / 3, counter.FramesPerSecond!.Value, 6);
        Assert.Equal(1d / 3, counter.PresentationsPerSecond!.Value, 6);
        for (int frame = 1; frame <= 60; frame++)
        {
            counter.Presented(TimeSpan.FromSeconds(4 + frame / 60d), true);
        }
        Assert.Equal(60, counter.FramesPerSecond!.Value, 3);
        Assert.Equal(60, counter.PresentationsPerSecond!.Value, 3);
    }

    [Fact]
    public void RollingWindowFollowsChangedAndUnevenPresentationCadence()
    {
        PresentationFrameRate counter = new();
        for (int frame = 0; frame <= 120; frame++)
        {
            counter.Presented(TimeSpan.FromSeconds(frame / 60d), true);
        }
        for (int frame = 1; frame <= 60; frame++)
        {
            counter.Presented(TimeSpan.FromSeconds(2 + frame / 30d), true);
        }
        Assert.Equal(30, counter.FramesPerSecond!.Value, 3);
        counter.Presented(TimeSpan.FromSeconds(4.1), true);
        counter.Presented(TimeSpan.FromSeconds(4.4), true);
        counter.Presented(TimeSpan.FromSeconds(4.9), true);
        counter.Presented(TimeSpan.FromSeconds(5), true);
        Assert.Equal(4, counter.FramesPerSecond!.Value, 6);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(15, 60)]
    [InlineData(30, 60)]
    [InlineData(15, 120)]
    [InlineData(30, 120)]
    [InlineData(60, 120)]
    public void RepeatedPresentationsDoNotCountAsVisualFrames(int visualFps, int presentationFps)
    {
        PresentationFrameRate counter = new();
        for (int frame = 0; frame <= presentationFps * 3; frame++)
        {
            bool changed = visualFps > 0 && frame % (presentationFps / visualFps) == 0;
            counter.Presented(TimeSpan.FromSeconds(frame / (double)presentationFps), changed);
        }
        Assert.Equal(visualFps, counter.FramesPerSecond!.Value, 3);
        Assert.Equal(presentationFps, counter.PresentationsPerSecond!.Value, 3);
    }

    [Fact]
    public void StillOutputAgesEarlierChangesOutOfTheWindow()
    {
        PresentationFrameRate counter = new();
        for (int frame = 0; frame <= 120; frame++)
        {
            counter.Presented(TimeSpan.FromSeconds(frame / 60d), frame <= 60);
        }
        Assert.Equal(0, counter.FramesPerSecond!.Value);
        Assert.Equal(60, counter.PresentationsPerSecond!.Value, 3);
        counter.Presented(TimeSpan.FromSeconds(2.5), true);
        Assert.InRange(counter.FramesPerSecond!.Value, 0.9, 1);
    }

    [Fact]
    public void ResetRequiresANewPresentationBaseline()
    {
        PresentationFrameRate counter = new();
        counter.Presented(TimeSpan.Zero, true);
        counter.Presented(TimeSpan.FromSeconds(1), true);
        Assert.Equal(1, counter.FramesPerSecond!.Value);
        Assert.Equal(1, counter.PresentationsPerSecond!.Value);
        counter.Reset();
        Assert.Null(counter.FramesPerSecond);
        Assert.Null(counter.PresentationsPerSecond);
        counter.Presented(TimeSpan.FromSeconds(20), true);
        Assert.Null(counter.FramesPerSecond);
        Assert.Null(counter.PresentationsPerSecond);
        counter.Presented(TimeSpan.FromSeconds(20.5), false);
        Assert.Equal(0, counter.FramesPerSecond!.Value);
        Assert.Equal(2, counter.PresentationsPerSecond!.Value);
    }
}
