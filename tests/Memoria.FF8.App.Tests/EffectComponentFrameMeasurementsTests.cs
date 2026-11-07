namespace Memoria.FF8.Tests;

public sealed class EffectComponentFrameMeasurementsTests
{
    [Fact]
    public void Sixty_fps_uses_only_whole_present_intervals_and_keeps_dispatch_elapsed_separate()
    {
        EffectComponentFrameMeasurements measurement = new(60_000);
        measurement.Begin(0);
        measurement.Presented(1000);
        measurement.Presented(2000);
        measurement.Presented(3000);

        EffectComponentFrameReport report = measurement.Complete(3500);

        Assert.Equal(3, report.PresentedFrameCount);
        Assert.Equal(2, report.IntervalCount);
        Assert.Equal(60, report.EffectiveFps, 8);
        Assert.Equal(1000d / 60, report.MedianIntervalMilliseconds, 8);
        Assert.Equal(1000d / 60, report.P95IntervalMilliseconds, 8);
        Assert.Equal(3500d / 60, report.ElapsedMilliseconds, 8);
        Assert.Equal(2000d / 60, report.IntervalMilliseconds, 8);
        Assert.Equal(0, report.IntervalsBelow50Fps);
        Assert.Equal(60, report.TargetFps);
        Assert.False(measurement.IsActive);
        Assert.Equal("installed-source-controlled-component-gpu-presentation", report.Scope);
    }

    [Fact]
    public void Retrospective_drain_excludes_crossing_and_tail_intervals_without_clipping()
    {
        EffectComponentFrameMeasurements measurement = new(1000);
        measurement.Begin(5);
        foreach (long timestamp in new long[] { 10, 20, 40, 70, 90 })
        {
            measurement.Presented(timestamp);
        }

        EffectComponentFrameReport report = measurement.Complete(65);

        Assert.Equal(5, report.ObservedPresentedFrameCount);
        Assert.Equal(3, report.PresentedFrameCount);
        Assert.Equal(2, report.IntervalCount);
        Assert.Equal(30, report.IntervalMilliseconds);
        Assert.Equal(60, report.ElapsedMilliseconds);
        Assert.Equal(2000d / 30, report.EffectiveFps, 8);
        Assert.Equal(15, report.MedianIntervalMilliseconds);
        Assert.Equal(20, report.P95IntervalMilliseconds);
        Assert.Equal(20, report.MaximumIntervalMilliseconds);
        Assert.Equal(0, report.IntervalsBelow50Fps);
        Assert.Equal(1, report.IntervalsBelowTargetFps);
    }

    [Fact]
    public void Strict_fifty_fps_threshold_and_nearest_rank_p95_use_individual_intervals()
    {
        EffectComponentFrameMeasurements measurement = new(1000);
        measurement.Begin(0);
        foreach (long timestamp in new long[] { 0, 10, 30, 51, 151 })
        {
            measurement.Presented(timestamp);
        }

        EffectComponentFrameReport report = measurement.Complete();

        Assert.Equal(4000d / 151, report.EffectiveFps, 8);
        Assert.Equal(20.5, report.MedianIntervalMilliseconds);
        Assert.Equal(100, report.P95IntervalMilliseconds);
        Assert.Equal(100, report.MaximumIntervalMilliseconds);
        Assert.Equal(2, report.IntervalsBelow50Fps);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(20)]
    public void Regressing_or_repeated_present_timestamps_are_rejected(long timestamp)
    {
        EffectComponentFrameMeasurements measurement = new(1000);
        measurement.Begin(10);
        measurement.Presented(20);

        Assert.Throws<InvalidDataException>(() => measurement.Presented(timestamp));
        Assert.Throws<InvalidDataException>(() => measurement.Presented(9));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_timestamp_frequency_is_rejected(double frequency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EffectComponentFrameMeasurements(frequency));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Missing_whole_interval_cannot_be_reported_as_valid_fps(int presentations)
    {
        EffectComponentFrameMeasurements measurement = new(1000);
        measurement.Begin(0);
        for (int index = 0; index < presentations; index++)
        {
            measurement.Presented(index + 1);
        }

        Assert.Throws<InvalidDataException>(() => measurement.Complete(100));
    }

    [Fact]
    public void Timestamp_budget_overflow_and_nonfinite_duration_fail_explicitly()
    {
        EffectComponentFrameMeasurements bounded = new(1000, 2);
        bounded.Begin(0);
        bounded.Presented(1);
        bounded.Presented(2);
        Assert.Throws<InvalidDataException>(() => bounded.Presented(3));
        Assert.Throws<InvalidDataException>(() => bounded.Complete());
        EffectComponentFrameMeasurements overflow = new(double.Epsilon);
        overflow.Begin(0);
        overflow.Presented(1);
        Assert.Throws<InvalidDataException>(() => overflow.Presented(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EffectComponentFrameMeasurements(1000, 65_537));
    }

    [Fact]
    public void Separate_components_do_not_inherit_inactive_presentations_or_previous_intervals()
    {
        EffectComponentFrameMeasurements measurement = new(1000);
        measurement.Presented(1);
        measurement.Begin(10);
        Assert.Throws<InvalidOperationException>(() => measurement.Begin(11));
        measurement.Presented(10);
        measurement.Presented(30);
        Assert.Equal(50, measurement.Complete().EffectiveFps);
        measurement.Presented(31);
        measurement.Begin(100);
        measurement.Presented(110);
        measurement.Presented(120);
        Assert.Throws<InvalidDataException>(() => measurement.Complete(99));
        Assert.Equal(100, measurement.Complete().EffectiveFps);
    }
}
