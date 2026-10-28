using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticVisualChangesTests
{
    [Fact]
    public void Pixel_receipts_join_their_original_frames_before_and_after_present_and_execution_completion()
    {
        DiagnosticVisualChanges collector = new(1000, 4, 100, 0);
        Assert.True(collector.Resolve(2, true));
        collector.Presented(1, 110, true, Submission(1));
        collector.Presented(2, 120, true, Submission(2));
        collector.Presented(3, 130, true, Submission(3));
        Assert.True(collector.Resolve(1, false));
        DiagnosticVisualChangesSummary summary = collector.Complete(100, 130);
        Assert.Equal(2, summary.KnownBattleImageComparisons);
        Assert.Equal(1, summary.ChangedBattleImageSamples);
        Assert.False(summary.Samples[2].ImageResultResolved);
        Assert.True(collector.Resolve(3, true));
        Assert.True(summary.Samples[2].ImageResultResolved);
        Assert.Equal(3, summary.KnownBattleImageComparisons);
        Assert.Equal(2, summary.ChangedBattleImageSamples);
        Assert.Equal(0, summary.UnknownBattleImageComparisons);
        Assert.False(collector.Resolve(4, true));
        collector.Resolve(3, null, true);
        Assert.Equal(1, summary.GpuDroppedComparisons);
        Assert.Equal(1, summary.UnknownBattleImageComparisons);
    }

    [Fact]
    public void Only_whole_presented_intervals_inside_the_effect_contribute_to_the_separate_image_rate()
    {
        DiagnosticVisualChanges collector = new(1000, 4, 100, 0);
        for (int frame = 1; frame <= 4; frame++)
        {
            collector.Presented(frame, 100 + frame * 10, true, Submission(frame));
            collector.Resolve(frame, true);
        }
        DiagnosticVisualChangesSummary summary = collector.Complete(115, 135);
        Assert.Equal(1, summary.EffectSamples);
        Assert.Equal(1, summary.KnownBattleImageComparisons);
        Assert.Equal(100, summary.BattleImageChangesPerSecond);
        Assert.Equal(1, summary.ChangedWorldSamples);
        Assert.Contains("not establish FX-only", summary.ImageScope);
    }

    [Fact]
    public void Baselines_unknown_results_and_capacity_loss_are_reported_without_fabricating_pixel_changes()
    {
        DiagnosticVisualChanges collector = new(1000, 2, 100, 0);
        for (int frame = 1; frame <= 9; frame++)
        {
            collector.Resolve(frame, null);
        }
        collector.Presented(1, 110, true, Submission(1));
        collector.Presented(2, 120, true, Submission(2));
        collector.Presented(3, 130, true, Submission(3));
        DiagnosticVisualChangesSummary summary = collector.Complete(100, 130);
        Assert.Equal(2, summary.Samples.Length);
        Assert.Equal(1, summary.DroppedSamples);
        Assert.Equal(1, summary.DroppedComparisons);
        Assert.Equal(0, summary.KnownBattleImageComparisons);
        Assert.Equal(0, summary.ChangedBattleImageSamples);
        Assert.Null(summary.BattleImageChangesPerSecond);
        Assert.All(summary.Samples, sample => Assert.True(sample.ImageResultResolved));
    }

    private static NativeBattlePilotSubmission Submission(int sample) => new(sample, sample + 1, 1, 2, 0.5f, (ulong)sample, (ulong)sample, 1, 3, 3, 3, 0, null, 0);
}
