using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class TelemetryTests
{
    private static readonly TelemetryCoverageManifest SyntheticCoverage = new("synthetic-v1", [new(10, "synthetic", "numeric test observer", true, true, null)]);
    private static readonly TelemetryOwnerState Owners = new("battle", 1, "memory", "renderer", 1, 0, 0, 10, 20);

    [Fact]
    public async Task Cached_site_lookup_keeps_concurrent_counters_exact_and_overflow_atomic()
    {
        TelemetryCoverageManifest coverage = new("synthetic-many-sites", Enumerable.Range(1, 200).Select(site => new TelemetryCoverageSite(site, "synthetic", "counter", true, true, null)).ToArray());
        TelemetryTrafficCollector collector = new(coverage);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (int operation = 0; operation < 1000; operation++)
            {
                collector.Record(200, TelemetryTrafficKind.CpuCopyPayload, 3);
            }
        })));
        TelemetryTrafficAggregate result = collector.Complete().Sites.Single(site => site.SiteId == 200 && site.Kind == TelemetryTrafficKind.CpuCopyPayload);
        Assert.Equal(24000, result.Bytes);
        Assert.Equal(8000, result.Operations);
        Assert.Equal(3, result.LargestOperationBytes);
        Assert.Throws<InvalidOperationException>(() => collector.Record(999, TelemetryTrafficKind.CpuCopyPayload, 1));
        Assert.Throws<InvalidOperationException>(() => collector.Record(200, (TelemetryTrafficKind)99, 1));
        collector.Record(1, TelemetryTrafficKind.CpuCopyPayload, long.MaxValue);
        Assert.Throws<OverflowException>(() => collector.Record(1, TelemetryTrafficKind.CpuCopyPayload, 1));
        TelemetryTrafficAggregate after = collector.Complete().Sites.Single(site => site.SiteId == 1 && site.Kind == TelemetryTrafficKind.CpuCopyPayload);
        Assert.Equal(long.MaxValue, after.Bytes);
        Assert.Equal(1, after.Operations);
    }

    [Fact]
    public void Frame_summary_includes_first_frame_stall_and_computes_even_median_fps_directly()
    {
        TelemetryFrameCollector collector = new(100, 1000, 8);
        collector.RecordPresentation(new(140, 1, "admission", 1, 1, 1, 0));
        collector.RecordPresentation(new(150, 2, "cleanup", 2, 2, 2, 0));
        TelemetryFrameSummary result = collector.Complete();
        Assert.Equal(50, result.DurationMs);
        Assert.Equal(25, result.MinimumFps);
        Assert.Equal(100, result.MaximumFps);
        Assert.Equal(62.5, result.MedianFps);
        Assert.Equal(25, result.MedianFrameMs);
        Assert.Equal(40, result.P95FrameMs);
        Assert.Equal(1, result.SlowIntervalCount);
        Assert.Equal("admission", result.WorstFrame!.Value.End.Phase);
    }

    [Fact]
    public void Strict_threshold_accepts_exactly_thirty_fps_and_rejects_next_tick()
    {
        TelemetryFrameCollector collector = new(0, 3000, 4);
        collector.RecordPresentation(new(100, 1, "effect", 1, 1, 1, 0));
        Assert.Equal(0, collector.Complete().SlowIntervalCount);
        collector.RecordPresentation(new(201, 2, "effect", 2, 2, 2, 0));
        Assert.Equal(1, collector.Complete().SlowIntervalCount);
        Assert.Equal(1, TelemetryFrameCollector.Combine(collector.Complete()).SlowIntervalCount);
    }

    [Fact]
    public void Overflow_and_empty_frames_cannot_be_valid_and_timestamps_cannot_rewind()
    {
        TelemetryFrameCollector collector = new(0, 1000, 1);
        Assert.False(collector.Complete().Valid);
        collector.RecordPresentation(new(10, 1, "effect", 1, 1, 1, 0));
        collector.RecordPresentation(new(20, 2, "effect", 2, 2, 2, 0));
        Assert.False(collector.Complete().Valid);
        Assert.Equal(1, collector.Complete().DroppedSamples);
        Assert.Throws<InvalidDataException>(() => collector.RecordPresentation(new(19, 3, "effect", 3, 3, 3, 0)));
    }

    [Fact]
    public void Numeric_counters_distinguish_actual_copy_overlap_logical_copy_and_fill_beyond_four_gib()
    {
        TelemetryTrafficCollector collector = new(SyntheticCoverage);
        collector.Record(10, TelemetryTrafficKind.CpuCopyPayload, 32);
        collector.Record(10, TelemetryTrafficKind.CpuCopyPayload, 16);
        collector.Record(10, TelemetryTrafficKind.LogicalMemoryCopy, 16);
        collector.Record(10, TelemetryTrafficKind.ZeroFill, 128);
        collector.Record(10, TelemetryTrafficKind.Fill, 64);
        for (int i = 0; i < 5; i++)
        {
            collector.Record(10, TelemetryTrafficKind.CpuCopyPayload, 1024L * 1024 * 1024);
        }
        TelemetryTrafficSnapshot result = collector.Complete();
        Assert.Equal(5L * 1024 * 1024 * 1024 + 48, result.CpuCopyPayloadBytes);
        Assert.Equal(7, result.Sites.Single(site => site.Kind == TelemetryTrafficKind.CpuCopyPayload).Operations);
        Assert.Equal(128, result.Sites.Single(site => site.Kind == TelemetryTrafficKind.ZeroFill).Bytes);
        Assert.Equal(16, result.Sites.Single(site => site.Kind == TelemetryTrafficKind.LogicalMemoryCopy).Bytes);
        Assert.True(result.RequiresLargeCopyReview);
    }

    [Fact]
    public void Synthetic_copy_and_overlapping_copy_count_payload_once_and_clear_separately()
    {
        TelemetryTrafficCollector collector = new(SyntheticCoverage);
        byte[] source = [1, 2, 3, 4, 5, 6];
        byte[] target = new byte[6];
        source.CopyTo(target, 0);
        collector.Record(10, TelemetryTrafficKind.CpuCopyPayload, source.Length);
        target.AsSpan(0, 4).CopyTo(target.AsSpan(2));
        collector.Record(10, TelemetryTrafficKind.CpuCopyPayload, 4);
        collector.Record(10, TelemetryTrafficKind.LogicalMemoryCopy, 4);
        Assert.Equal(new byte[] { 1, 2, 1, 2, 3, 4 }, target);
        target.AsSpan().Clear();
        collector.Record(10, TelemetryTrafficKind.ZeroFill, target.Length);
        Assert.All(target, value => Assert.Equal(0, value));
        TelemetryTrafficSnapshot result = collector.Complete();
        Assert.Equal(10, result.CpuCopyPayloadBytes);
        Assert.Equal(2, result.Sites.Single(site => site.Kind == TelemetryTrafficKind.CpuCopyPayload).Operations);
        Assert.Equal(6, result.Sites.Single(site => site.Kind == TelemetryTrafficKind.ZeroFill).Bytes);
    }

    [Fact]
    public void Uninstrumented_is_unknown_while_enabled_zero_is_zero()
    {
        TelemetryTrafficSnapshot missing = new TelemetryTrafficCollector(TelemetryCoverageManifest.PreparationOnly).Complete();
        Assert.False(missing.Available);
        Assert.Null(missing.CpuCopyPayloadBytes);
        Assert.Contains("TELEMETRY_UNAVAILABLE", missing.Error!);
        Assert.Equal(0, new TelemetryTrafficCollector(SyntheticCoverage).Complete().CpuCopyPayloadBytes);
    }

    [Fact]
    public void Event_stream_preserves_individual_reflected_hits_healing_and_transient_statuses()
    {
        TelemetryEventCollector collector = new(64);
        for (int i = 0; i < 40; i++)
        {
            collector.Record(new(i, 1, "damage", "target", "reflected_target", 100, 80, "reflected", null, "character"));
        }
        collector.Record(new(40, 1, "healing", "target", "target", 90, 50, "hit", null, "gf"));
        collector.Record(new(41, 1, "status", "target", "target", 0, 0, "added", "sleep", "character"));
        collector.Record(new(42, 1, "status", "target", "target", 0, 0, "removed", "sleep", "character"));
        Assert.Equal(43, collector.Complete().Count);
        Assert.Equal("reflected_target", collector.Complete()[0].ActualRecipient);
        Assert.Equal("gf", collector.Complete()[40].HpOwner);
        Assert.Equal("added", collector.Complete()[41].Result);
        Assert.Equal("removed", collector.Complete()[42].Result);
        Assert.Equal(0, collector.DroppedSamples);
        TelemetryEventCollector bounded = new(1);
        bounded.Record(collector.Complete()[0]);
        bounded.Record(collector.Complete()[1]);
        Assert.Equal(1, bounded.DroppedSamples);
    }

    [Fact]
    public void Repeat_guard_allows_immutable_updates_but_rejects_reconstruction_restore_reset_and_rewind()
    {
        TelemetryContinuityGuard guard = new(Owners);
        guard.Observe(Owners with { NativeCallback = 11, FrameIndex = 21 });
        Assert.Throws<InvalidDataException>(() => guard.Observe(Owners with { LifecycleGeneration = 2 }));
        Assert.Throws<InvalidDataException>(() => guard.Observe(Owners with { RestoreCount = 1 }));
        Assert.Throws<InvalidDataException>(() => guard.Observe(Owners with { ResetCount = 1 }));
        Assert.Throws<InvalidDataException>(() => guard.Observe(Owners with { ConstructorCount = 2 }));
        Assert.Throws<InvalidDataException>(() => guard.Observe(Owners with { RetainedMemoryOwnerId = "replacement" }));
        Assert.Throws<InvalidDataException>(() => guard.Observe(Owners));
    }

    [Fact]
    public void Independent_validator_rejects_missing_owner_second_cast_fallback_noop_loss_and_forged_fps_summary()
    {
        TelemetryExecutionEvidence first = Evidence();
        TelemetryExecutionEvidence second = Evidence(1);
        Assert.Equal("PASS", Validate(first, second).Verdict);
        Assert.Contains("MISSING_SECOND_EXECUTION", Validate(first).Errors);
        Assert.Contains("INITIALIZATION_UNVERIFIED", TelemetryEvidenceValidator.Validate([first, second], false, true, true, SyntheticCoverage).Errors);
        Assert.Contains("DISPATCH_UNVERIFIED", Validate(first, second with { ActualBinding = "fallback" }).Errors);
        Assert.Contains("DISPATCH_UNVERIFIED", Validate(first, second with { Started = false }).Errors);
        Assert.Contains("TELEMETRY_LOSS", Validate(first, second with { DroppedEvents = 1 }).Errors);
        Assert.Contains("CONTINUITY_BROKEN", Validate(first, second with { StartOwners = Owners with { ConstructorCount = 2 } }).Errors);
        Assert.Contains("TELEMETRY_UNAVAILABLE", TelemetryEvidenceValidator.Validate([first, second], true, true, true).Errors);
        TelemetryFrameSummary forged = first.Frames with { SlowIntervalCount = 0, Intervals = [new(new(100, 1, "admission", 1, 1, 1, 0), 100)] };
        Assert.Contains("PRESENTATION_BELOW_30_FPS", Validate(first, second with { Frames = forged }).Errors);
    }

    private static TelemetryEvidenceVerdict Validate(params TelemetryExecutionEvidence[] executions)
    {
        return TelemetryEvidenceValidator.Validate(executions, true, true, true, SyntheticCoverage);
    }

    private static TelemetryExecutionEvidence Evidence(int offset = 0)
    {
        TelemetryFrameCollector frames = new(0, 1000, 4);
        frames.RecordPresentation(new(10, 1, "effect", 1, 1, 1, 0));
        return new("action", "action", "binding", "binding", true, true, true, 2, true, true, true, true, 1, 1, 0, frames.Complete(), new TelemetryTrafficCollector(SyntheticCoverage).Complete(), Owners with { NativeCallback = 10 + offset, FrameIndex = 20 + offset }, Owners with { NativeCallback = 11 + offset, FrameIndex = 21 + offset });
    }
}
