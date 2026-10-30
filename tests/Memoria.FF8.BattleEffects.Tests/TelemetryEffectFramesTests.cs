using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class TelemetryEffectFramesTests
{
    [Fact]
    public void Admission_and_tail_cannot_dilute_the_effect_median()
    {
        TelemetryFrameSummary invocation = Frames(10, 20, 30, 130, 230, 240, 250, 260);

        TelemetryEffectFrameSummary effect = TelemetryEffectFrames.Summarize(invocation, 0, 30, 230, 1000);

        Assert.Equal(100, invocation.MedianFps);
        Assert.True(effect.Available);
        Assert.Equal(10, effect.MedianFps);
        Assert.Equal(2, effect.Frames.FrameCount);
        Assert.Equal(200, effect.Frames.DurationMs);
        Assert.Empty(effect.BoundaryIntervals);
    }

    [Fact]
    public void Exact_marker_boundaries_admit_whole_intervals_and_leave_adjacent_intervals_outside()
    {
        TelemetryFrameSummary invocation = Frames(10, 30, 50, 60);

        TelemetryEffectFrameSummary effect = TelemetryEffectFrames.Summarize(invocation, 0, 10, 50, 1000);

        Assert.Equal(invocation.Intervals.Skip(1).Take(2), effect.Frames.Intervals);
        Assert.Equal(50, effect.MedianFps);
        Assert.Equal(0, effect.BoundaryIntervalCount);
        Assert.Equal(10, effect.StartedTicks);
        Assert.Equal(50, effect.CompletedTicks);
    }

    [Fact]
    public void Partial_edges_are_retained_separately_with_original_durations_and_without_mutating_input()
    {
        TelemetryFrameSummary invocation = Frames(10, 30, 130, 150, 160);
        TelemetryFrameInterval[] original = invocation.Intervals.ToArray();

        TelemetryEffectFrameSummary effect = TelemetryEffectFrames.Summarize(invocation, 0, 20, 140, 1000);

        Assert.Equal(original, invocation.Intervals);
        Assert.Equal(original[2], Assert.Single(effect.Frames.Intervals));
        Assert.Equal(new[] { original[1], original[3] }, effect.BoundaryIntervals);
        Assert.Equal(2, effect.BoundaryIntervalCount);
        Assert.Equal(100, effect.Frames.DurationMs);
        Assert.Equal(120, effect.CompletedTicks - effect.StartedTicks);
        Assert.Equal(10, effect.MedianFps);
        Assert.Equal(new double[] { 20, 20 }, effect.BoundaryIntervals.Select(interval => interval.DurationMs));
        Assert.Contains("without clipping", effect.Policy);
    }

    [Fact]
    public void Effect_inside_one_present_interval_has_no_available_fps_and_counts_the_boundary_once()
    {
        TelemetryFrameSummary invocation = Frames(100, 200);

        TelemetryEffectFrameSummary effect = TelemetryEffectFrames.Summarize(invocation, 0, 20, 30, 1000);

        Assert.False(effect.Available);
        Assert.Null(effect.MedianFps);
        Assert.Empty(effect.Frames.Intervals);
        Assert.Equal(0, effect.Frames.DurationMs);
        Assert.Equal(invocation.Intervals[0], Assert.Single(effect.BoundaryIntervals));
        Assert.Equal(1, effect.BoundaryIntervalCount);
    }

    [Theory]
    [InlineData(-1, 10, 20, 1000)]
    [InlineData(0, -1, 20, 1000)]
    [InlineData(0, 20, 20, 1000)]
    [InlineData(0, 20, 10, 1000)]
    [InlineData(0, 20, 201, 1000)]
    [InlineData(0, 20, 30, 0)]
    [InlineData(0, 20, 30, -1)]
    public void Invalid_markers_frequency_or_uncovered_markers_are_rejected(long preceding, long start, long end, long frequency)
    {
        Assert.Throws<InvalidDataException>(() => TelemetryEffectFrames.Summarize(Frames(100, 200), preceding, start, end, frequency));
    }

    [Fact]
    public void Invalid_original_timestamps_or_durations_are_rejected_even_outside_the_effect()
    {
        TelemetryFrameSummary invocation = Frames(100, 200, 300);
        TelemetryFrameInterval[] intervals = invocation.Intervals.ToArray();
        intervals[2] = intervals[2] with { End = intervals[2].End with { TimestampTicks = 199 } };
        Assert.Throws<InvalidDataException>(() => TelemetryEffectFrames.Summarize(invocation with { Intervals = intervals }, 0, 100, 200, 1000));
        foreach (double duration in new[] { 0, -1, double.NaN, double.PositiveInfinity, 99 })
        {
            intervals = invocation.Intervals.ToArray();
            intervals[0] = intervals[0] with { DurationMs = duration };
            Assert.Throws<InvalidDataException>(() => TelemetryEffectFrames.Summarize(invocation with { Intervals = intervals }, 0, 100, 200, 1000));
        }
    }

    [Fact]
    public void Timestamp_frequency_is_used_to_validate_original_durations()
    {
        TelemetryFrameCollector collector = new(100, 3000, 2);
        collector.RecordPresentation(new(400, 1, "effect", 1, 1, 1, 0));
        collector.RecordPresentation(new(700, 2, "effect", 2, 2, 2, 0));

        TelemetryEffectFrameSummary effect = TelemetryEffectFrames.Summarize(collector.Complete(), 100, 100, 700, 3000);

        Assert.Equal(10, effect.MedianFps);
        Assert.Equal(200, effect.Frames.DurationMs);
        Assert.Throws<InvalidDataException>(() => TelemetryEffectFrames.Summarize(collector.Complete(), 100, 100, 700, 1000));
    }

    [Fact]
    public void Dropped_samples_keep_effect_fps_unavailable_even_with_whole_intervals()
    {
        TelemetryFrameSummary invocation = Frames(100, 200) with { DroppedSamples = 1, Valid = false };

        TelemetryEffectFrameSummary effect = TelemetryEffectFrames.Summarize(invocation, 0, 0, 200, 1000);

        Assert.False(effect.Available);
        Assert.Null(effect.MedianFps);
        Assert.Equal(2, effect.Frames.FrameCount);
        Assert.Equal(1, effect.Frames.DroppedSamples);
    }

    [Fact]
    public void Native_release_between_real_presentations_retains_walltime_without_inventing_an_interval()
    {
        TelemetryFrameCollector collector = new(100, 1000, 2);
        collector.RecordPresentation(new(105, 1, "effect", 1, 1, 1, 0));
        collector.RecordPresentation(new(110, 2, "effect", 2, 2, 1, 0));
        TelemetryFrameSummary invocation = collector.Complete();
        TelemetryFrameInterval[] original = invocation.Intervals.ToArray();
        TelemetryEffectFrameSummary effect = TelemetryEffectFrames.SummarizeNativeRelease(invocation, 100, 102, 112, 115, 1000);
        Assert.Equal(112, effect.CompletedTicks);
        Assert.Equal(10, effect.CompletedTicks - effect.StartedTicks);
        Assert.Equal(original, invocation.Intervals);
        Assert.Equal(original[1], Assert.Single(effect.Frames.Intervals));
        Assert.Equal(original[0], Assert.Single(effect.BoundaryIntervals));
        Assert.Equal(5, effect.Frames.DurationMs);
        Assert.Equal(200, effect.MedianFps);
        Assert.Contains("Unobserved completion time contributes no presentation interval", effect.Policy);
        Assert.Throws<InvalidDataException>(() => TelemetryEffectFrames.Summarize(invocation, 100, 102, 112, 1000));
    }

    [Fact]
    public void Native_release_equal_to_last_real_present_remains_valid_without_adding_a_tail_frame()
    {
        TelemetryFrameSummary invocation = Frames(100, 200);
        var effect = TelemetryEffectFrames.SummarizeNativeRelease(invocation, 0, 100, 200, 210, 1000);
        Assert.Equal(200, effect.CompletedTicks);
        Assert.Equal(invocation.Intervals[1], Assert.Single(effect.Frames.Intervals));
        Assert.Equal(100, effect.Frames.DurationMs);
    }

    [Theory]
    [InlineData(199, 210, 1000)]
    [InlineData(201, 201, 1000)]
    [InlineData(202, 201, 1000)]
    [InlineData(201, 0, 1000)]
    [InlineData(201, 210, 0)]
    public void Native_release_requires_known_upper_bound_after_release_and_valid_frequency(long release, long firstTail, long frequency)
    {
        Assert.Throws<InvalidDataException>(() => TelemetryEffectFrames.SummarizeNativeRelease(Frames(100, 200), 0, 150, release, firstTail, frequency));
    }

    [Fact]
    public void Native_release_cannot_hide_empty_dropped_or_corrupt_original_intervals()
    {
        TelemetryFrameSummary invocation = Frames(100, 200);
        TelemetryFrameSummary empty = new TelemetryFrameCollector(0, 1000, 1).Complete();
        Assert.Throws<InvalidDataException>(() => TelemetryEffectFrames.SummarizeNativeRelease(empty, 0, 150, 201, 210, 1000));
        Assert.Throws<InvalidDataException>(() => TelemetryEffectFrames.SummarizeNativeRelease(invocation with { DroppedSamples = 1 }, 0, 150, 201, 210, 1000));
        TelemetryFrameInterval[] corrupt = invocation.Intervals.ToArray();
        corrupt[0] = corrupt[0] with { DurationMs = 99 };
        Assert.Throws<InvalidDataException>(() => TelemetryEffectFrames.SummarizeNativeRelease(invocation with { Intervals = corrupt }, 0, 150, 201, 210, 1000));
        corrupt = invocation.Intervals.ToArray();
        corrupt[1] = corrupt[1] with { End = corrupt[1].End with { FrameIndex = 1 } };
        Assert.Throws<InvalidDataException>(() => TelemetryEffectFrames.SummarizeNativeRelease(invocation with { Intervals = corrupt }, 0, 150, 201, 210, 1000));
    }

    private static TelemetryFrameSummary Frames(params long[] ticks)
    {
        TelemetryFrameCollector collector = new(0, 1000, ticks.Length);
        for (int index = 0; index < ticks.Length; index++)
        {
            collector.RecordPresentation(new(ticks[index], index + 1, "effect", index + 1, index + 1, index + 1, 0));
        }
        return collector.Complete();
    }
}
