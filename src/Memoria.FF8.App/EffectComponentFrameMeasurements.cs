namespace Memoria.FF8;

internal sealed record EffectComponentFrameReport(string Scope, int ObservedPresentedFrameCount, int PresentedFrameCount, int IntervalCount, double ElapsedMilliseconds, double IntervalMilliseconds, double EffectiveFps, double MedianIntervalMilliseconds, double P95IntervalMilliseconds, double MaximumIntervalMilliseconds, int IntervalsBelow50Fps, int IntervalsBelowTargetFps, int TargetFps, string BoundaryPolicy);

internal sealed class EffectComponentFrameMeasurements
{
    internal const int MaximumTimestampCount = 65_536;
    private const int TargetFps = 60;
    private const string Scope = "installed-source-controlled-component-gpu-presentation";
    private const string BoundaryPolicy = "Only whole consecutive successful-present intervals with both endpoints inside dispatch through first drain are included. The first in-window presentation seeds the interval boundary; admission and trailing boundary intervals are excluded without clipping. Retrospectively observed tail presentations are excluded.";
    private readonly double timestampFrequency;
    private readonly long[] timestamps;
    private long started;
    private int count;
    private bool invalid;

    internal EffectComponentFrameMeasurements(double timestampFrequency, int maximumTimestamps = MaximumTimestampCount)
    {
        if (!double.IsFinite(timestampFrequency) || timestampFrequency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timestampFrequency), "Presentation timestamps require a finite positive frequency.");
        }
        if (maximumTimestamps is < 2 or > MaximumTimestampCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTimestamps), "Presentation timestamp storage must remain within the supported finite budget.");
        }
        this.timestampFrequency = timestampFrequency;
        timestamps = new long[maximumTimestamps];
    }

    internal bool IsActive { get; private set; }

    internal void Begin(long timestamp)
    {
        if (IsActive)
        {
            throw new InvalidOperationException("A component presentation measurement is already active.");
        }
        if (timestamp < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timestamp));
        }
        started = timestamp;
        count = 0;
        invalid = false;
        IsActive = true;
    }

    internal void Presented(long timestamp)
    {
        if (!IsActive)
        {
            return;
        }
        if (timestamp < started || count > 0 && timestamp <= timestamps[count - 1])
        {
            invalid = true;
            throw new InvalidDataException("Successful presentation timestamps must increase monotonically within the active component window.");
        }
        if (count == timestamps.Length)
        {
            invalid = true;
            throw new InvalidDataException("Component presentation timestamp collection exceeded its finite budget.");
        }
        if (count > 0)
        {
            double milliseconds = (timestamp - timestamps[count - 1]) * (1000d / timestampFrequency);
            if (!double.IsFinite(milliseconds) || milliseconds <= 0)
            {
                invalid = true;
                throw new InvalidDataException("Component presentation duration is not finite and positive.");
            }
        }
        timestamps[count++] = timestamp;
    }

    internal EffectComponentFrameReport Complete(long? timestamp = null)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("No component presentation measurement is active.");
        }
        if (invalid)
        {
            throw new InvalidDataException("Invalid or overflowing presentation samples cannot produce a valid component FPS report.");
        }
        long completed = timestamp ?? (count == 0 ? started : timestamps[count - 1]);
        if (completed < started)
        {
            throw new InvalidDataException("The component completion boundary precedes dispatch.");
        }
        int included = 0;
        while (included < count && timestamps[included] <= completed)
        {
            included++;
        }
        if (included < 2)
        {
            throw new InvalidDataException("The component has no whole successful-present interval inside its active window.");
        }
        double[] intervals = new double[included - 1];
        int below50 = 0;
        int belowTarget = 0;
        for (int index = 0; index < intervals.Length; index++)
        {
            double milliseconds = Milliseconds(timestamps[index + 1] - timestamps[index]);
            intervals[index] = milliseconds;
            below50 += milliseconds > 20 ? 1 : 0;
            belowTarget += milliseconds > 1000d / TargetFps ? 1 : 0;
        }
        double duration = Milliseconds(timestamps[included - 1] - timestamps[0]);
        double elapsed = Milliseconds(completed - started);
        double effectiveFps = intervals.Length * 1000d / duration;
        if (!double.IsFinite(effectiveFps) || effectiveFps <= 0)
        {
            throw new InvalidDataException("Component presentation FPS is not finite and positive.");
        }
        Array.Sort(intervals);
        int middle = intervals.Length / 2;
        double median = intervals.Length % 2 == 0 ? intervals[middle - 1] + (intervals[middle] - intervals[middle - 1]) / 2 : intervals[middle];
        double p95 = intervals[checked((int)Math.Ceiling(intervals.Length * 0.95)) - 1];
        EffectComponentFrameReport report = new(Scope, count, included, intervals.Length, elapsed, duration, effectiveFps, median, p95, intervals[^1], below50, belowTarget, TargetFps, BoundaryPolicy);
        IsActive = false;
        return report;
    }

    private double Milliseconds(long ticks)
    {
        double milliseconds = ticks * (1000d / timestampFrequency);
        if (!double.IsFinite(milliseconds) || milliseconds <= 0)
        {
            throw new InvalidDataException("Component presentation duration is not finite and positive.");
        }
        return milliseconds;
    }
}
