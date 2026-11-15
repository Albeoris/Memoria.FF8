using System.Text.Json.Serialization;

namespace Memoria.FF8;

internal readonly record struct PresentationPacerObservation(bool Enabled, long Calls, long TimerCalls, long TimerTicks, long Resets, int FrameLimit, long Invalid);
internal readonly record struct DiagnosticPacingSample(long Frame, long PresentedTicks, long IntervalTicks, long PresentBeginTicks, long PresentEndTicks, long TimerWaitTicks, long PacerCalls, long TimerCalls, long ResetGeneration, int FrameLimit, DiagnosticHostFramePhases? HostPhases);
internal sealed record DiagnosticPacingDuration(long Count, double TotalMilliseconds, double MinimumMilliseconds, double MedianMilliseconds, double P95Milliseconds, double MaximumMilliseconds);
internal sealed record DiagnosticPacingLimit(int FrameLimit, int Samples);
internal sealed record DiagnosticHostPhaseSummary(int Samples, int ExcludedSamples, DiagnosticPacingDuration UpdateExcludingTimerWait, DiagnosticPacingDuration BattlePresentationUpdate, DiagnosticPacingDuration Draw, DiagnosticPacingDuration BattleSceneRender, DiagnosticPacingDuration PostPresent, DiagnosticPacingDuration Uninstrumented);
internal sealed record DiagnosticPacingSummary(long TimestampFrequency, int Capacity, long Observations, long InvalidObservations, long OverflowObservations, int Samples, long PacerCalls, long TimerCalls, long ResetCalls, DiagnosticPacingLimit[] FrameLimits, DiagnosticPacingDuration Interval, DiagnosticPacingDuration TimerWait, DiagnosticPacingDuration Present, DiagnosticPacingDuration Other)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DiagnosticHostPhaseSummary? HostPhases { get; init; }
}
internal readonly record struct DiagnosticPacingProgressDuration(double TotalMilliseconds, double MaximumMilliseconds);
internal sealed record DiagnosticPacingProgress(long TimestampFrequency, int Capacity, long Observations, long InvalidObservations, long OverflowObservations, int Samples, long PacerCalls, long TimerCalls, long ResetCalls, DiagnosticPacingProgressDuration Interval, DiagnosticPacingProgressDuration TimerWait, DiagnosticPacingProgressDuration Present, DiagnosticPacingProgressDuration Other);

/// <summary>Optional bounded wall-time observations; no state capture or per-present materialization.</summary>
internal sealed class DiagnosticPresentationPacing
{
    private readonly DiagnosticPacingSample[] samples;
    private readonly long frequency;
    private PresentationPacerObservation previous;
    private readonly long initialResets;
    private long preceding;
    private int count;
    private long observations;
    private long invalid;
    private long overflow;

    internal DiagnosticPresentationPacing(long frequency, int capacity, long preceding, PresentationPacerObservation baseline)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frequency);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        this.frequency = frequency;
        samples = new DiagnosticPacingSample[capacity];
        this.preceding = preceding;
        previous = baseline;
        initialResets = baseline.Resets;
    }

    internal void Record(long frame, long presented, long presentBegin, long presentEnd, PresentationPacerObservation observation, DiagnosticHostFramePhases? hostPhases = null)
    {
        observations++;
        bool valid = observation.Enabled && presented > preceding && presentBegin >= preceding && presentEnd >= presentBegin && presentEnd <= presented && observation.Calls >= previous.Calls && observation.TimerCalls >= previous.TimerCalls && observation.TimerTicks >= previous.TimerTicks && observation.Resets >= previous.Resets && observation.Invalid == previous.Invalid && observation.TimerTicks - previous.TimerTicks <= presented - preceding - (presentEnd - presentBegin);
        if (!valid)
        {
            invalid++;
        }
        else if (count == samples.Length)
        {
            overflow++;
        }
        else
        {
            samples[count++] = new(frame, presented, presented - preceding, presentBegin, presentEnd, observation.TimerTicks - previous.TimerTicks, observation.Calls - previous.Calls, observation.TimerCalls - previous.TimerCalls, observation.Resets, observation.FrameLimit, hostPhases);
        }
        preceding = presented;
        previous = observation;
    }

    internal DiagnosticPacingProgress SnapshotProgress()
    {
        double interval = 0;
        double timer = 0;
        double present = 0;
        double other = 0;
        long maximumInterval = 0;
        long maximumTimer = 0;
        long maximumPresent = 0;
        long maximumOther = 0;
        long pacerCalls = 0;
        long timerCalls = 0;
        foreach (DiagnosticPacingSample sample in samples.AsSpan(0, count))
        {
            long presentTicks = sample.PresentEndTicks - sample.PresentBeginTicks;
            long otherTicks = sample.IntervalTicks - sample.TimerWaitTicks - presentTicks;
            interval += sample.IntervalTicks;
            timer += sample.TimerWaitTicks;
            present += presentTicks;
            other += otherTicks;
            maximumInterval = Math.Max(maximumInterval, sample.IntervalTicks);
            maximumTimer = Math.Max(maximumTimer, sample.TimerWaitTicks);
            maximumPresent = Math.Max(maximumPresent, presentTicks);
            maximumOther = Math.Max(maximumOther, otherTicks);
            pacerCalls += sample.PacerCalls;
            timerCalls += sample.TimerCalls;
        }
        double milliseconds = 1000d / frequency;
        return new(frequency, samples.Length, observations, invalid, overflow, count, pacerCalls, timerCalls, previous.Resets - initialResets, new(interval * milliseconds, maximumInterval * milliseconds), new(timer * milliseconds, maximumTimer * milliseconds), new(present * milliseconds, maximumPresent * milliseconds), new(other * milliseconds, maximumOther * milliseconds));
    }

    internal DiagnosticPacingSummary Complete(long? throughTimestamp = null)
    {
        int included = count;
        if (throughTimestamp is long through)
        {
            while (included > 0 && samples[included - 1].PresentedTicks > through)
            {
                included--;
            }
        }
        DiagnosticPacingSample[] observed = samples.AsSpan(0, included).ToArray();
        long finalResets = throughTimestamp.HasValue ? observed.LastOrDefault().ResetGeneration : previous.Resets;
        long resets = observed.Length == 0 && throughTimestamp.HasValue ? 0 : finalResets - initialResets;
        return new(frequency, samples.Length, observations, invalid, overflow, included, observed.Sum(sample => sample.PacerCalls), observed.Sum(sample => sample.TimerCalls), resets, observed.GroupBy(sample => sample.FrameLimit).Select(group => new DiagnosticPacingLimit(group.Key, group.Count())).ToArray(), Summarize(observed.Select(sample => sample.IntervalTicks)), Summarize(observed.Select(sample => sample.TimerWaitTicks)), Summarize(observed.Select(sample => sample.PresentEndTicks - sample.PresentBeginTicks)), Summarize(observed.Select(sample => sample.IntervalTicks - sample.TimerWaitTicks - (sample.PresentEndTicks - sample.PresentBeginTicks))))
        {
            HostPhases = SummarizeHostPhases(observed)
        };
    }

    private DiagnosticHostPhaseSummary? SummarizeHostPhases(DiagnosticPacingSample[] observed)
    {
        if (observed.All(sample => sample.HostPhases is null))
        {
            return null;
        }
        // The driver can start after a frame has already been presented. An overlapping first
        // interval can still contribute pacing evidence, but its phase split is not comparable.
        DiagnosticPacingSample[] valid = observed.Where(sample => sample.HostPhases is { } phases && phases.FitsInterval(sample.TimerWaitTicks, NonPresentTicks(sample))).ToArray();
        return new(valid.Length, observed.Length - valid.Length,
            Summarize(valid.Select(sample => sample.HostPhases!.Value.Update - sample.TimerWaitTicks)),
            Summarize(valid.Select(sample => sample.HostPhases!.Value.BattlePresentationUpdate)),
            Summarize(valid.Select(sample => sample.HostPhases!.Value.Draw)),
            Summarize(valid.Select(sample => sample.HostPhases!.Value.BattleSceneRender)),
            Summarize(valid.Select(sample => sample.HostPhases!.Value.PostPresent)),
            Summarize(valid.Select(sample =>
            {
                DiagnosticHostFramePhases phases = sample.HostPhases!.Value;
                return NonPresentTicks(sample) - phases.Update - phases.Draw - phases.PostPresent;
            })));
    }

    private static long NonPresentTicks(DiagnosticPacingSample sample)
    {
        return sample.IntervalTicks - sample.PresentEndTicks + sample.PresentBeginTicks;
    }

    private DiagnosticPacingDuration Summarize(IEnumerable<long> ticks)
    {
        long[] sorted = ticks.Order().ToArray();
        if (sorted.Length == 0)
        {
            return new(0, 0, 0, 0, 0, 0);
        }
        double milliseconds = 1000d / frequency;
        int middle = sorted.Length / 2;
        double median = (sorted.Length & 1) != 0 ? sorted[middle] : sorted[middle - 1] / 2d + sorted[middle] / 2d;
        return new(sorted.Length, sorted.Sum(value => (double)value) * milliseconds, sorted[0] * milliseconds, median * milliseconds, sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1] * milliseconds, sorted[^1] * milliseconds);
    }
}
