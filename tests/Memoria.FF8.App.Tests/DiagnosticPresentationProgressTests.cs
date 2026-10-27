namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticPresentationProgressTests
{
    [Fact]
    public void Counts_only_whole_active_intervals_with_strict_exact_thresholds()
    {
        DiagnosticPresentationProgress progress = new(3000);
        progress.Reset(1, 0);
        progress.Presented(100, 10, 0);
        progress.Presented(200, 10, 0);
        progress.Presented(350, 10, 0);
        progress.Presented(650, 10, 0);
        progress.Presented(951, 10, 0);
        DiagnosticPresentationProgressSnapshot snapshot = progress.Snapshot(7, true, false);
        Assert.Equal(7, snapshot.Sequence);
        Assert.Equal(1, snapshot.Execution);
        Assert.True(snapshot.EffectStarted);
        Assert.False(snapshot.EffectCompleted);
        Assert.Equal(5, snapshot.ObservedIntervals);
        Assert.Equal(4, snapshot.ActiveEffectIntervals);
        Assert.Equal(1, snapshot.ExcludedIntervals);
        Assert.Equal(0, snapshot.InvalidTimestampCount);
        Assert.Equal(3, snapshot.OverThirtyFpsBudgetIntervals);
        Assert.Equal(2, snapshot.Over50MsIntervals);
        Assert.Equal(1, snapshot.Over100MsIntervals);
        Assert.Equal(301 * 1000d / 3000, snapshot.MaximumFrameMs);
    }

    [Fact]
    public void Completion_accepts_exact_endpoint_and_excludes_crossing_and_tail_without_clipping()
    {
        DiagnosticPresentationProgress progress = new(1000);
        progress.Reset(1, 100);
        progress.Presented(200, 100, 200);
        progress.Presented(400, 100, 350);
        progress.Presented(600, 100, 350);
        DiagnosticPresentationProgressSnapshot snapshot = progress.Snapshot(1, true, true);
        Assert.Equal(1, snapshot.ActiveEffectIntervals);
        Assert.Equal(2, snapshot.ExcludedIntervals);
        Assert.Equal(0, snapshot.Over100MsIntervals);
        Assert.Equal(100, snapshot.MaximumFrameMs);
    }

    [Fact]
    public void Ten_slow_presented_intervals_are_cumulative_and_reset_for_the_next_execution()
    {
        DiagnosticPresentationProgress progress = new(1000);
        progress.Reset(1, 0);
        progress.Presented(1, 1, 0);
        for (int interval = 1; interval <= 10; interval++)
        {
            progress.Presented(1 + interval * 101, 1, 0);
        }
        DiagnosticPresentationProgressSnapshot first = progress.Snapshot(3, true, false);
        Assert.Equal(10, first.ActiveEffectIntervals);
        Assert.Equal(10, first.Over100MsIntervals);
        Assert.Equal(101, first.MaximumFrameMs);
        progress.Reset(2, 2000);
        progress.Presented(2200, 0, 0);
        DiagnosticPresentationProgressSnapshot second = progress.Snapshot(4, false, false);
        Assert.Equal(2, second.Execution);
        Assert.Equal(1, second.ObservedIntervals);
        Assert.Equal(1, second.ExcludedIntervals);
        Assert.Equal(0, second.ActiveEffectIntervals);
        Assert.Equal(0, second.Over100MsIntervals);
        Assert.Equal(0, second.MaximumFrameMs);
        Assert.Equal(10, first.Over100MsIntervals);
    }

    [Fact]
    public void Duplicate_and_rewound_timestamps_are_reported_without_fabricating_intervals_or_throwing()
    {
        DiagnosticPresentationProgress progress = new(1000);
        progress.Reset(1, 100);
        progress.Presented(100, 100, 0);
        progress.Presented(99, 100, 0);
        progress.Presented(201, 100, 0);
        progress.Presented(302, 100, 0);
        DiagnosticPresentationProgressSnapshot snapshot = progress.Snapshot(1, true, false);
        Assert.Equal(4, snapshot.ObservedIntervals);
        Assert.Equal(2, snapshot.InvalidTimestampCount);
        Assert.Equal(3, snapshot.ExcludedIntervals);
        Assert.Equal(1, snapshot.ActiveEffectIntervals);
        Assert.Equal(1, snapshot.Over100MsIntervals);
        Assert.Equal(101, snapshot.MaximumFrameMs);
    }
}
