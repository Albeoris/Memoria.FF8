using System.Numerics;

namespace Memoria.FF8;

internal readonly record struct DiagnosticPresentationProgressSnapshot(int Sequence, int Execution, bool EffectStarted, bool EffectCompleted, long ObservedIntervals, long ActiveEffectIntervals, long ExcludedIntervals, long InvalidTimestampCount, long OverThirtyFpsBudgetIntervals, long Over50MsIntervals, long Over100MsIntervals, double MaximumFrameMs, int MaximumOver50MsIn60Intervals = 0);

/// <summary>Whole consecutive present intervals inside one observed effect, without retaining individual samples.</summary>
internal sealed class DiagnosticPresentationProgress(long frequency)
{
    private readonly long frequency = frequency > 0 ? frequency : throw new ArgumentOutOfRangeException(nameof(frequency));
    private long previousTimestamp;
    private bool boundaryKnown;
    private int execution;
    private long observed;
    private long included;
    private long overThirtyFpsBudget;
    private long over50Ms;
    private long over100Ms;
    private long maximumTicks;
    private long invalidTimestamps;
    private ulong over50MsWindow;
    private int maximumOver50MsIn60Intervals;

    public void Reset(int execution, long precedingTimestamp)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(execution);
        ArgumentOutOfRangeException.ThrowIfNegative(precedingTimestamp);
        this.execution = execution;
        previousTimestamp = precedingTimestamp;
        boundaryKnown = true;
        observed = included = overThirtyFpsBudget = over50Ms = over100Ms = maximumTicks = invalidTimestamps = 0;
        over50MsWindow = 0;
        maximumOver50MsIn60Intervals = 0;
    }

    public void Presented(long timestamp, long effectStartedTicks, long effectCompletedTicks)
    {
        observed++;
        if (execution == 0 || timestamp <= previousTimestamp)
        {
            invalidTimestamps++;
            previousTimestamp = Math.Max(previousTimestamp, timestamp);
            boundaryKnown = false;
            return;
        }
        long intervalStart = previousTimestamp;
        long ticks = timestamp - intervalStart;
        previousTimestamp = timestamp;
        if (!boundaryKnown)
        {
            boundaryKnown = true;
            return;
        }
        if (effectStartedTicks <= 0 || intervalStart < effectStartedTicks || effectCompletedTicks > 0 && timestamp > effectCompletedTicks)
        {
            return;
        }
        included++;
        if (ticks > frequency / 30d)
        {
            overThirtyFpsBudget++;
        }
        bool over50 = ticks > frequency / 20d;
        over50MsWindow = ((over50MsWindow << 1) | (over50 ? 1UL : 0UL)) & ((1UL << 60) - 1);
        maximumOver50MsIn60Intervals = Math.Max(maximumOver50MsIn60Intervals, BitOperations.PopCount(over50MsWindow));
        if (over50)
        {
            over50Ms++;
        }
        if (ticks > frequency / 10d)
        {
            over100Ms++;
        }
        maximumTicks = Math.Max(maximumTicks, ticks);
    }

    public DiagnosticPresentationProgressSnapshot Snapshot(int sequence, bool effectStarted, bool effectCompleted)
        => new(sequence, execution, effectStarted, effectCompleted, observed, included, observed - included, invalidTimestamps, overThirtyFpsBudget, over50Ms, over100Ms, maximumTicks * 1000d / frequency, maximumOver50MsIn60Intervals);
}
