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
        Assert.Equal(2, snapshot.MaximumOver50MsIn60Intervals);
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
        Assert.Equal(1, snapshot.MaximumOver50MsIn60Intervals);
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
        Assert.Equal(10, first.MaximumOver50MsIn60Intervals);
        progress.Reset(2, 2000);
        progress.Presented(2200, 0, 0);
        DiagnosticPresentationProgressSnapshot second = progress.Snapshot(4, false, false);
        Assert.Equal(2, second.Execution);
        Assert.Equal(1, second.ObservedIntervals);
        Assert.Equal(1, second.ExcludedIntervals);
        Assert.Equal(0, second.ActiveEffectIntervals);
        Assert.Equal(0, second.Over100MsIntervals);
        Assert.Equal(0, second.MaximumFrameMs);
        Assert.Equal(0, second.MaximumOver50MsIn60Intervals);
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

    [Fact]
    public void Peak_window_survives_later_fast_presentations_until_the_next_heartbeat()
    {
        DiagnosticPresentationProgress progress = new(1000);
        progress.Reset(1, 1);
        long timestamp = 1;
        for (int interval = 0; interval < 180; interval++)
        {
            timestamp += interval < 13 ? 51 : 1;
            progress.Presented(timestamp, 1, 0);
        }
        DiagnosticPresentationProgressSnapshot snapshot = progress.Snapshot(1, true, false);
        Assert.Equal(180, snapshot.ActiveEffectIntervals);
        Assert.Equal(13, snapshot.Over50MsIntervals);
        Assert.Equal(13, snapshot.MaximumOver50MsIn60Intervals);
        System.Text.Json.JsonElement json = System.Text.Json.JsonSerializer.SerializeToElement(snapshot, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower });
        Assert.Equal(13, json.GetProperty("maximum_over50_ms_in60_intervals").GetInt32());
    }

    [Fact]
    public void Sliding_window_expires_the_sixtieth_prior_interval_instead_of_using_cumulative_bad_frames()
    {
        DiagnosticPresentationProgress progress = new(1000);
        progress.Reset(1, 1);
        long timestamp = 1;
        for (int interval = 0; interval < 69; interval++)
        {
            timestamp += interval < 9 || interval >= 60 ? 51 : 50;
            progress.Presented(timestamp, 1, 0);
        }
        DiagnosticPresentationProgressSnapshot snapshot = progress.Snapshot(1, true, false);
        Assert.Equal(69, snapshot.ActiveEffectIntervals);
        Assert.Equal(18, snapshot.Over50MsIntervals);
        Assert.Equal(9, snapshot.MaximumOver50MsIn60Intervals);
    }

    [Fact]
    public void Exact_active_start_and_completion_endpoints_bound_the_peak_without_counting_tail_intervals()
    {
        DiagnosticPresentationProgress progress = new(1000);
        progress.Reset(1, 1);
        progress.Presented(100, 100, 0);
        progress.Presented(151, 100, 151);
        progress.Presented(202, 100, 151);
        DiagnosticPresentationProgressSnapshot snapshot = progress.Snapshot(1, true, true);
        Assert.Equal(1, snapshot.ActiveEffectIntervals);
        Assert.Equal(2, snapshot.ExcludedIntervals);
        Assert.Equal(1, snapshot.MaximumOver50MsIn60Intervals);
    }

    [Fact]
    public void Updating_rolling_density_does_not_allocate_per_presentation()
    {
        DiagnosticPresentationProgress progress = new(1000);
        progress.Reset(1, 1);
        progress.Presented(52, 1, 0);
        long timestamp = 52;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int interval = 0; interval < 180; interval++)
        {
            timestamp += interval % 2 == 0 ? 51 : 1;
            progress.Presented(timestamp, 1, 0);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(31, progress.Snapshot(1, true, false).MaximumOver50MsIn60Intervals);
    }
}
