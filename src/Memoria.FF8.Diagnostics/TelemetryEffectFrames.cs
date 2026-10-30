namespace Memoria.FF8.BattleEffects;

public sealed record TelemetryEffectFrameSummary(TelemetryFrameSummary Frames, long StartedTicks, long CompletedTicks, int BoundaryIntervalCount, TelemetryFrameInterval[] BoundaryIntervals, string Policy)
{
    public bool Available => Frames.Valid;
    public double? MedianFps => Available ? Frames.MedianFps : null;
}

/// <summary>Reports whole observed presentation intervals contained by the effect markers without clipping boundary intervals.</summary>
public static class TelemetryEffectFrames
{
    public const string Policy = "Only whole original presentation intervals with start >= effect start and end <= effect completion; overlapping boundary intervals are retained separately without clipping. Frame duration is the sum of admitted intervals, not effect wall time.";

    public static TelemetryEffectFrameSummary Summarize(TelemetryFrameSummary invocation, long precedingTicks, long effectStart, long effectEnd, long frequency)
    {
        return SummarizeCore(invocation, precedingTicks, effectStart, effectEnd, frequency, null);
    }

    public static TelemetryEffectFrameSummary SummarizeNativeRelease(TelemetryFrameSummary invocation, long precedingTicks, long effectStart, long effectEnd, long firstTailTicks, long frequency)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        if (invocation.Intervals.Count == 0 || invocation.DroppedSamples != 0 || firstTailTicks <= effectEnd)
        {
            throw new InvalidDataException("Native release requires nonempty loss-free real presentation intervals and its independently observed first tail strictly after release.");
        }
        return SummarizeCore(invocation, precedingTicks, effectStart, effectEnd, frequency, firstTailTicks);
    }

    private static TelemetryEffectFrameSummary SummarizeCore(TelemetryFrameSummary invocation, long precedingTicks, long effectStart, long effectEnd, long frequency, long? firstTailTicks)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        if (frequency <= 0 || precedingTicks < 0 || effectStart < precedingTicks || effectEnd <= effectStart)
        {
            throw new InvalidDataException("Effect markers must increase within the presentation window, and timestamp frequency must be positive.");
        }
        List<TelemetryFrameInterval> included = [];
        List<TelemetryFrameInterval> boundaries = [];
        long previousTicks = precedingTicks;
        long? previousFrame = null;
        foreach (TelemetryFrameInterval interval in invocation.Intervals)
        {
            long endTicks = interval.End.TimestampTicks;
            if (endTicks <= previousTicks || previousFrame.HasValue && interval.End.FrameIndex <= previousFrame.Value || !double.IsFinite(interval.DurationMs) || interval.DurationMs <= 0)
            {
                throw new InvalidDataException("Effect frame intervals must retain increasing presentation timestamps and frame indices with their original measured durations.");
            }
            double measuredDuration = (endTicks - previousTicks) * 1000.0 / frequency;
            if (Math.Abs(interval.DurationMs - measuredDuration) > 0.0000001)
            {
                throw new InvalidDataException("Effect frame intervals must retain their original measured durations.");
            }
            if (previousTicks >= effectStart && endTicks <= effectEnd)
            {
                included.Add(interval);
            }
            else if (previousTicks < effectEnd && endTicks > effectStart)
            {
                boundaries.Add(interval);
            }
            previousTicks = endTicks;
            previousFrame = interval.End.FrameIndex;
        }
        if (firstTailTicks is not null && effectEnd < previousTicks)
        {
            throw new InvalidDataException("Native release cannot precede its last retained focal presentation.");
        }
        if (firstTailTicks is null && effectEnd > previousTicks)
        {
            throw new InvalidDataException("Effect markers must be covered by retained presentation intervals.");
        }
        TelemetryFrameSummary frames = TelemetryFrameCollector.Recompute(included.ToArray(), invocation.DroppedSamples);
        string policy = firstTailTicks is null ? Policy : Policy + " Native release may follow the last focal presentation and must precede the independently observed first tail. Unobserved completion time contributes no presentation interval.";
        return new(frames, effectStart, effectEnd, boundaries.Count, boundaries.ToArray(), policy);
    }
}
