namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticPresentationPacingTests
{
    [Fact]
    public void Successful_present_separates_timer_present_and_remaining_work_without_raw_report_samples()
    {
        DiagnosticPresentationPacing collector = new(1000, 2, 100, new(true, 3, 2, 20, 4, 60, 0));
        collector.Record(1, 140, 123, 138, new(true, 4, 3, 36, 4, 60, 0));
        collector.Record(2, 180, 161, 178, new(true, 5, 4, 50, 5, 60, 0));
        DiagnosticPacingSummary result = collector.Complete();
        Assert.Equal(2, result.Samples);
        Assert.Equal(0, result.InvalidObservations);
        Assert.Equal(0, result.OverflowObservations);
        Assert.Equal(2, result.PacerCalls);
        Assert.Equal(2, result.TimerCalls);
        Assert.Equal(1, result.ResetCalls);
        Assert.Equal(60, Assert.Single(result.FrameLimits).FrameLimit);
        Assert.Equal(40, result.Interval.MedianMilliseconds);
        Assert.Equal(30, result.TimerWait.TotalMilliseconds);
        Assert.Equal(32, result.Present.TotalMilliseconds);
        Assert.Equal(18, result.Other.TotalMilliseconds);
    }

    [Fact]
    public void Invalid_boundaries_and_capacity_overflow_are_counted_without_throwing_or_fake_intervals()
    {
        DiagnosticPresentationPacing collector = new(1000, 1, 100, new(true, 0, 0, 0, 0, 60, 0));
        collector.Record(1, 120, 115, 119, new(true, 1, 1, 10, 0, 60, 0));
        collector.Record(2, 140, 135, 139, new(true, 2, 2, 20, 0, 60, 0));
        collector.Record(3, 140, 141, 140, new(true, 3, 3, 30, 0, 60, 0));
        DiagnosticPacingSummary result = collector.Complete();
        Assert.Equal(3, result.Observations);
        Assert.Equal(1, result.Samples);
        Assert.Equal(1, result.InvalidObservations);
        Assert.Equal(1, result.OverflowObservations);
        Assert.Equal(20, result.Interval.TotalMilliseconds);
    }

    [Fact]
    public void Ordinary_pacer_path_does_not_record_observations()
    {
        using PresentationPacer pacer = new();
        pacer.WaitForNextFrame(0, TestContext.Current.CancellationToken);
        pacer.Reset();
        Assert.Equal(default(PresentationPacerObservation), pacer.TimingObservation);
        pacer.ObserveTiming(true);
        pacer.WaitForNextFrame(0, TestContext.Current.CancellationToken);
        Assert.Equal(1, pacer.TimingObservation.Calls);
        Assert.Equal(1, pacer.TimingObservation.Resets);
        Assert.Equal(0, pacer.TimingObservation.TimerCalls);
    }
}
