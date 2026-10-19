namespace Memoria.FF8.App.Tests;

public sealed class PresentationFrameRateTests
{
    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void CountsCompletedIntervalsAtActualWallClockCadence(int fps)
    {
        PresentationFrameRate counter = new();
        Assert.Null(counter.FramesPerSecond);
        counter.Presented(TimeSpan.FromSeconds(10));
        Assert.Null(counter.FramesPerSecond);
        for (int frame = 1; frame <= fps * 3; frame++)
        {
            counter.Presented(TimeSpan.FromSeconds(10 + frame / (double)fps));
        }
        Assert.Equal(fps, counter.FramesPerSecond!.Value, 3);
    }

    [Fact]
    public void LongFrameIncludesTheEntireStallAndThenRecovers()
    {
        PresentationFrameRate counter = new();
        for (int frame = 0; frame <= 60; frame++)
        {
            counter.Presented(TimeSpan.FromSeconds(frame / 60d));
        }
        counter.Presented(TimeSpan.FromSeconds(4));
        Assert.Equal(1d / 3, counter.FramesPerSecond!.Value, 6);
        for (int frame = 1; frame <= 60; frame++)
        {
            counter.Presented(TimeSpan.FromSeconds(4 + frame / 60d));
        }
        Assert.Equal(60, counter.FramesPerSecond!.Value, 3);
    }

    [Fact]
    public void RollingWindowFollowsChangedAndUnevenPresentationCadence()
    {
        PresentationFrameRate counter = new();
        for (int frame = 0; frame <= 120; frame++)
        {
            counter.Presented(TimeSpan.FromSeconds(frame / 60d));
        }
        for (int frame = 1; frame <= 60; frame++)
        {
            counter.Presented(TimeSpan.FromSeconds(2 + frame / 30d));
        }
        Assert.Equal(30, counter.FramesPerSecond!.Value, 3);
        counter.Presented(TimeSpan.FromSeconds(4.1));
        counter.Presented(TimeSpan.FromSeconds(4.4));
        counter.Presented(TimeSpan.FromSeconds(4.9));
        counter.Presented(TimeSpan.FromSeconds(5));
        Assert.Equal(4, counter.FramesPerSecond!.Value, 6);
    }
}
