namespace Memoria.FF8.BattleEffects;

public readonly record struct TelemetryFrame(long TimestampTicks, long FrameIndex, string Phase, long NativeCallbacks, long UniqueRenders, long DrawCalls, long TextureUploads);
public readonly record struct TelemetryFrameInterval(TelemetryFrame End, double DurationMs);
public sealed record TelemetryFrameSummary(int FrameCount, double DurationMs, double MinimumFps, double MaximumFps, double MedianFps, double MinimumFrameMs, double MedianFrameMs, double P95FrameMs, double P99FrameMs, double MaximumFrameMs, int SlowIntervalCount, double SlowIntervalDurationMs, TelemetryFrameInterval? WorstFrame, long DroppedSamples, bool Valid, string PercentileConvention, IReadOnlyList<TelemetryFrameInterval> Intervals);

/// <summary>Bounded successful-present observations. The preceding present includes admission and first-use stalls. Serialize only after collection.</summary>
public sealed class TelemetryFrameCollector
{
    private readonly TelemetryFrameInterval[] intervals;
    private readonly long frequency;
    private long previousTicks;
    private long previousFrame = -1;
    private int count;
    private long dropped;

    public TelemetryFrameCollector(long precedingPresentationTicks, long timestampFrequency, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(precedingPresentationTicks);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timestampFrequency);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        previousTicks = precedingPresentationTicks;
        frequency = timestampFrequency;
        intervals = new TelemetryFrameInterval[capacity];
    }

    public void RecordPresentation(TelemetryFrame frame)
    {
        if (frame.TimestampTicks <= previousTicks || frame.FrameIndex <= previousFrame)
        {
            throw new InvalidDataException("Successful presentation timestamps and frame indices must increase.");
        }
        double duration = (frame.TimestampTicks - previousTicks) * 1000.0 / frequency;
        previousTicks = frame.TimestampTicks;
        previousFrame = frame.FrameIndex;
        if (count == intervals.Length)
        {
            dropped++;
            return;
        }
        intervals[count++] = new(frame, duration);
    }

    public TelemetryFrameSummary Complete()
    {
        return Recompute(intervals.AsSpan(0, count).ToArray(), dropped);
    }

    public static TelemetryFrameSummary Combine(params TelemetryFrameSummary[] executions)
    {
        return Recompute(executions.SelectMany(execution => execution.Intervals).ToArray(), executions.Sum(execution => execution.DroppedSamples));
    }

    public static TelemetryFrameSummary Recompute(TelemetryFrameInterval[] values, long dropped)
    {
        if (dropped < 0 || values.Any(value => !double.IsFinite(value.DurationMs) || value.DurationMs <= 0))
        {
            throw new InvalidDataException("Frame intervals must be finite and positive, and loss cannot be negative.");
        }
        const string convention = "Nearest rank: sorted[ceil(p*N)-1]; medians average the two central samples; interval ending at each included successful present includes its preceding boundary.";
        if (values.Length == 0)
        {
            return new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, dropped, false, convention, values);
        }
        double[] times = values.Select(value => value.DurationMs).Order().ToArray();
        double[] fps = values.Select(value => 1000.0 / value.DurationMs).Order().ToArray();
        TelemetryFrameInterval[] slow = values.Where(value => value.DurationMs > 1000.0 / 30).ToArray();
        return new(values.Length, times.Sum(), fps[0], fps[^1], Median(fps), times[0], Median(times), times[(int)Math.Ceiling(0.95 * times.Length) - 1], times[(int)Math.Ceiling(0.99 * times.Length) - 1], times[^1], slow.Length, slow.Sum(value => value.DurationMs), values.MaxBy(value => value.DurationMs), dropped, dropped == 0, convention, values);
    }

    private static double Median(double[] sorted)
    {
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }
}

/// <summary>Bounded semantic stream; preserve individual hits, original/actual recipients and transient statuses rather than polling a status tail.</summary>
public sealed class TelemetryEventCollector
{
    private readonly TelemetryCombatEvent[] events;
    private int count;
    public long DroppedSamples { get; private set; }

    public TelemetryEventCollector(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        events = new TelemetryCombatEvent[capacity];
    }

    public void Record(TelemetryCombatEvent value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value.ResolvedAmount);
        ArgumentOutOfRangeException.ThrowIfNegative(value.ActualAmount);
        if (count == events.Length)
        {
            DroppedSamples++;
            return;
        }
        events[count++] = value;
    }

    public IReadOnlyList<TelemetryCombatEvent> Complete()
    {
        return events.AsSpan(0, count).ToArray();
    }
}

public readonly record struct TelemetryCombatEvent(long TimestampTicks, long Invocation, string Kind, string OriginalRecipient, string ActualRecipient, long ResolvedAmount, long ActualAmount, string Result, string? Status, string HpOwner);
