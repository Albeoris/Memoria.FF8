using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal readonly record struct DiagnosticVisualChangeSample(long Frame, long IntervalStartTicks, long PresentedTicks, bool Rendered, NativeBattlePilotSubmission Fx, bool? WorldChanged, bool? ProjectedChanged, bool? MaterialChanged, bool ImageResultResolved = false, bool? BattleImageChanged = null, bool ImageComparisonDropped = false);

internal sealed record DiagnosticVisualChangesSummary(long TimestampFrequency, long EffectStartedTicks, long EffectCompletedTicks, long DroppedSamples, long DroppedComparisons, DiagnosticVisualChangeSample[] Samples)
{
    public string ImageScope => "Presented battle-renderer target, including UI inside that target; window UI is excluded. Pixel changes do not establish FX-only motion.";
    public string FxScope => "Queued triangle world/projected geometry and UV, diffuse, TPAGE/CLUT and draw-state signatures for effects 2, 3 and 144; stage/actor geometry (effect 0) is excluded. Signatures do not establish pixel visibility; texture contents are measured by battle image comparisons.";
    public int EffectSamples => Samples.Count(Admitted);
    public int KnownBattleImageComparisons => Samples.Count(sample => Admitted(sample) && sample.BattleImageChanged.HasValue);
    public int UnknownBattleImageComparisons => EffectSamples - KnownBattleImageComparisons;
    public int GpuDroppedComparisons => Samples.Count(sample => sample.ImageComparisonDropped);
    public int ChangedBattleImageSamples => Samples.Count(sample => Admitted(sample) && sample.BattleImageChanged == true);
    public int ChangedWorldSamples => Samples.Count(sample => Admitted(sample) && sample.WorldChanged == true);
    public int ChangedProjectedSamples => Samples.Count(sample => Admitted(sample) && sample.ProjectedChanged == true);
    public int ChangedMaterialSamples => Samples.Count(sample => Admitted(sample) && sample.MaterialChanged == true);
    public int CoveredContinuousSamples => Samples.Count(sample => Admitted(sample) && sample.Fx.CoveredContinuousCorners > 0);
    public double? BattleImageChangesPerSecond
    {
        get
        {
            long duration = 0;
            foreach (DiagnosticVisualChangeSample sample in Samples)
            {
                if (Admitted(sample) && sample.BattleImageChanged.HasValue)
                {
                    duration += sample.PresentedTicks - sample.IntervalStartTicks;
                }
            }
            return duration > 0 ? ChangedBattleImageSamples * (double)TimestampFrequency / duration : null;
        }
    }

    private bool Admitted(DiagnosticVisualChangeSample sample) => EffectStartedTicks > 0 && EffectCompletedTicks > EffectStartedTicks && sample.IntervalStartTicks >= EffectStartedTicks && sample.PresentedTicks <= EffectCompletedTicks;
}

/// <summary>Fixed-capacity present receipts with asynchronous pixel results joined by their original frame ordinal.</summary>
internal sealed class DiagnosticVisualChanges
{
    private readonly DiagnosticVisualChangeSample[] samples;
    private readonly (long Frame, bool? Changed, bool Dropped)[] pending = new (long, bool?, bool)[8];
    private readonly long frequency;
    private readonly long firstFrame;
    private long preceding;
    private int count;
    private int pendingCount;
    private long dropped;
    private long droppedComparisons;
    private DiagnosticVisualChangesSummary? completed;

    internal DiagnosticVisualChanges(long frequency, int capacity, long preceding, long precedingFrame)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frequency, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);
        this.frequency = frequency;
        this.preceding = preceding;
        firstFrame = precedingFrame + 1;
        samples = new DiagnosticVisualChangeSample[capacity];
    }

    internal void Presented(long frame, long timestamp, bool rendered, NativeBattlePilotSubmission fx)
    {
        if (completed is not null || timestamp < preceding)
        {
            throw new InvalidOperationException("Visual present receipts must remain ordered inside their execution.");
        }
        if (count == samples.Length)
        {
            dropped++;
            preceding = timestamp;
            return;
        }
        DiagnosticVisualChangeSample? previous = count > 0 ? samples[count - 1] : null;
        bool compare = rendered && previous is { Rendered: true };
        DiagnosticVisualChangeSample sample = new(frame, preceding, timestamp, rendered, fx,
            compare ? fx.WorldSignature != previous!.Value.Fx.WorldSignature : null,
            compare ? fx.ProjectedSignature != previous!.Value.Fx.ProjectedSignature : null,
            compare ? fx.MaterialSignature != previous!.Value.Fx.MaterialSignature : null);
        for (int index = 0; index < pendingCount; index++)
        {
            if (pending[index].Frame == frame)
            {
                sample = sample with { ImageResultResolved = true, BattleImageChanged = pending[index].Changed, ImageComparisonDropped = pending[index].Dropped };
                pending[index] = pending[--pendingCount];
                break;
            }
        }
        samples[count++] = sample;
        preceding = timestamp;
    }

    internal bool Resolve(long frame, bool? changed, bool droppedComparison = false)
    {
        for (int index = count - 1; index >= 0 && samples[index].Frame >= frame; index--)
        {
            if (samples[index].Frame == frame)
            {
                samples[index] = samples[index] with { ImageResultResolved = true, BattleImageChanged = changed, ImageComparisonDropped = droppedComparison };
                if (completed is not null)
                {
                    completed.Samples[index] = samples[index];
                }
                return true;
            }
        }
        if (completed is null && frame >= firstFrame && (count == 0 || frame > samples[count - 1].Frame))
        {
            if (pendingCount < pending.Length)
            {
                pending[pendingCount++] = (frame, changed, droppedComparison);
            }
            else
            {
                droppedComparisons++;
            }
            return true;
        }
        return false;
    }

    internal DiagnosticVisualChangesSummary Complete(long effectStartedTicks, long effectCompletedTicks)
    {
        return completed ??= new(frequency, effectStartedTicks, effectCompletedTicks, dropped, droppedComparisons, samples.AsSpan(0, count).ToArray());
    }
}
