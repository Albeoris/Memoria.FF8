using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticPresentationPacingTests
{
    [Fact]
    public void Live_progress_is_non_destructive_and_matches_final_retained_sample_totals()
    {
        DiagnosticPresentationPacing collector = new(1000, 3, 100, new(true, 3, 2, 20, 4, 60, 0));
        DiagnosticPacingProgress empty = collector.SnapshotProgress();
        Assert.Equal(0, empty.Samples);
        Assert.Equal(0, empty.Interval.TotalMilliseconds);
        collector.Record(1, 140, 123, 138, new(true, 4, 3, 36, 4, 60, 0));
        DiagnosticPacingProgress first = collector.SnapshotProgress();
        Assert.Equal(first, collector.SnapshotProgress());
        collector.Record(2, 180, 161, 178, new(true, 5, 4, 50, 5, 30, 0));
        DiagnosticPacingProgress progress = collector.SnapshotProgress();
        Assert.Equal(progress, collector.SnapshotProgress());
        Assert.Equal(1, first.Samples);
        Assert.Equal(2, progress.Samples);
        Assert.Equal(40, first.Interval.TotalMilliseconds);
        Assert.Equal(80, progress.Interval.TotalMilliseconds);
        AssertProgressMatchesFinal(progress, collector.Complete());
        collector.Record(3, 220, 201, 218, new(true, 6, 5, 64, 5, 30, 0));
        Assert.Equal(3, collector.SnapshotProgress().Samples);
        AssertProgressMatchesFinal(collector.SnapshotProgress(), collector.Complete());
    }

    [Fact]
    public void Live_progress_reports_invalid_and_overflow_without_including_unretained_durations()
    {
        DiagnosticPresentationPacing collector = new(1000, 1, 100, new(true, 0, 0, 0, 0, 60, 0));
        collector.Record(1, 120, 115, 119, new(true, 1, 1, 10, 0, 60, 0));
        collector.Record(2, 140, 135, 139, new(true, 2, 2, 20, 0, 60, 0));
        collector.Record(3, 140, 141, 140, new(true, 3, 3, 30, 0, 60, 0));
        DiagnosticPacingProgress progress = collector.SnapshotProgress();
        Assert.Equal(3, progress.Observations);
        Assert.Equal(1, progress.Samples);
        Assert.Equal(1, progress.InvalidObservations);
        Assert.Equal(1, progress.OverflowObservations);
        AssertProgressMatchesFinal(progress, collector.Complete());
    }

    private static void AssertProgressMatchesFinal(DiagnosticPacingProgress progress, DiagnosticPacingSummary final)
    {
        Assert.Equal(final.TimestampFrequency, progress.TimestampFrequency);
        Assert.Equal(final.Capacity, progress.Capacity);
        Assert.Equal(final.Observations, progress.Observations);
        Assert.Equal(final.InvalidObservations, progress.InvalidObservations);
        Assert.Equal(final.OverflowObservations, progress.OverflowObservations);
        Assert.Equal(final.Samples, progress.Samples);
        Assert.Equal(final.PacerCalls, progress.PacerCalls);
        Assert.Equal(final.TimerCalls, progress.TimerCalls);
        Assert.Equal(final.ResetCalls, progress.ResetCalls);
        Assert.Equal(final.Interval.TotalMilliseconds, progress.Interval.TotalMilliseconds);
        Assert.Equal(final.Interval.MaximumMilliseconds, progress.Interval.MaximumMilliseconds);
        Assert.Equal(final.TimerWait.TotalMilliseconds, progress.TimerWait.TotalMilliseconds);
        Assert.Equal(final.TimerWait.MaximumMilliseconds, progress.TimerWait.MaximumMilliseconds);
        Assert.Equal(final.Present.TotalMilliseconds, progress.Present.TotalMilliseconds);
        Assert.Equal(final.Present.MaximumMilliseconds, progress.Present.MaximumMilliseconds);
        Assert.Equal(final.Other.TotalMilliseconds, progress.Other.TotalMilliseconds);
        Assert.Equal(final.Other.MaximumMilliseconds, progress.Other.MaximumMilliseconds);
    }

    [Fact]
    public void Successful_present_separates_timer_present_and_remaining_work()
    {
        DiagnosticPresentationPacing collector = new(1000, 2, 100, new(true, 3, 2, 20, 4, 60, 0));
        collector.Record(1, 140, 123, 138, new(true, 4, 3, 36, 4, 60, 0));
        collector.Record(2, 180, 161, 178, new(true, 5, 4, 50, 5, 60, 0));
        DiagnosticPacingSummary result = collector.Complete();
        Assert.Equal(2, result.Samples);
        Assert.Equal(0, result.InvalidObservations);
        Assert.Equal(0, result.OverflowObservations);
        Assert.Equal(2, result.PacerCalls);
        Assert.Equal(2, result.TimerCalls);
        Assert.Equal(1, result.ResetCalls);
        Assert.Equal(60, Assert.Single(result.FrameLimits).FrameLimit);
        Assert.Equal(40, result.Interval.MedianMilliseconds);
        Assert.Equal(30, result.TimerWait.TotalMilliseconds);
        Assert.Equal(32, result.Present.TotalMilliseconds);
        Assert.Equal(18, result.Other.TotalMilliseconds);
    }

    [Fact]
    public void Invalid_boundaries_and_capacity_overflow_are_counted_without_throwing_or_fake_intervals()
    {
        DiagnosticPresentationPacing collector = new(1000, 1, 100, new(true, 0, 0, 0, 0, 60, 0));
        collector.Record(1, 120, 115, 119, new(true, 1, 1, 10, 0, 60, 0));
        collector.Record(2, 140, 135, 139, new(true, 2, 2, 20, 0, 60, 0));
        collector.Record(3, 140, 141, 140, new(true, 3, 3, 30, 0, 60, 0));
        DiagnosticPacingSummary result = collector.Complete();
        Assert.Equal(3, result.Observations);
        Assert.Equal(1, result.Samples);
        Assert.Equal(1, result.InvalidObservations);
        Assert.Equal(1, result.OverflowObservations);
        Assert.Equal(20, result.Interval.TotalMilliseconds);
    }

    [Fact]
    public void Host_phases_explain_other_time_without_counting_nested_work_twice()
    {
        DiagnosticPresentationPacing collector = new(1000, 3, 100, new(true, 0, 0, 0, 0, 60, 0));
        collector.Record(1, 150, 140, 148, new(true, 1, 1, 20, 0, 60, 0), new(2, 1, 10, 3, 5));
        collector.Record(2, 195, 182, 193, new(true, 2, 1, 20, 0, 60, 0), new(25, 15, 10, 4, 5));
        DiagnosticPacingSummary report = collector.Complete();
        DiagnosticHostPhaseSummary phases = Assert.IsType<DiagnosticHostPhaseSummary>(report.HostPhases);
        Assert.Equal(2, report.Samples);
        Assert.Equal(1, phases.Samples);
        Assert.Equal(1, phases.ExcludedSamples);
        Assert.Equal(2, phases.UpdateExcludingTimerWait.TotalMilliseconds);
        Assert.Equal(1, phases.BattlePresentationUpdate.TotalMilliseconds);
        Assert.Equal(10, phases.Draw.TotalMilliseconds);
        Assert.Equal(3, phases.BattleSceneRender.TotalMilliseconds);
        Assert.Equal(5, phases.PostPresent.TotalMilliseconds);
        Assert.Equal(5, phases.Uninstrumented.TotalMilliseconds);
    }

    [Fact]
    public void Ordinary_pacer_path_does_not_record_observations()
    {
        using PresentationPacer pacer = new();
        pacer.WaitForNextFrame(0, TestContext.Current.CancellationToken);
        pacer.Reset();
        Assert.Equal(default(PresentationPacerObservation), pacer.TimingObservation);
        pacer.ObserveTiming(true);
        pacer.WaitForNextFrame(0, TestContext.Current.CancellationToken);
        Assert.Equal(1, pacer.TimingObservation.Calls);
        Assert.Equal(1, pacer.TimingObservation.Resets);
        Assert.Equal(0, pacer.TimingObservation.TimerCalls);
    }

    [Fact]
    public void Slowest_presents_are_bounded_ranked_stably_and_respect_completion_boundary()
    {
        DiagnosticPresentationPacing collector = new(1000, 12, 100, new(true, 0, 0, 0, 0, 60, 0));
        long presented = 100;
        for (int frame = 1; frame <= 12; frame++)
        {
            presented += frame == 12 ? 11 : frame;
            collector.Record(frame, presented, presented - 1, presented, new(true, frame, 0, 0, 0, 60, 0));
        }
        DiagnosticPacingSummary report = collector.Complete();
        Assert.Equal(new long[] { 11, 12, 10, 9, 8, 7, 6, 5, 4, 3 }, report.SlowestPresents.Select(sample => sample.Frame));
        Assert.Equal(11, report.SlowestPresents[0].IntervalMilliseconds);
        Assert.Equal(11, report.SlowestPresents[0].PresentedTicks - report.SlowestPresents[0].IntervalBeginTicks);
        Assert.Equal("Unavailable", report.SlowestPresents[0].HostPhaseStatus);
        Assert.Null(report.SlowestPresents[0].HostPhases);
        DiagnosticPacingSummary earlier = collector.Complete(presented - 11);
        Assert.Equal(new long[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 }, earlier.SlowestPresents.Select(sample => sample.Frame));
        Assert.Equal(12, collector.Complete().Samples);
    }

    [Fact]
    public void Slow_present_host_partition_and_native_subdivision_share_the_exact_interval()
    {
        DiagnosticPresentationPacing collector = new(1000, 2, 100, new(true, 0, 0, 0, 0, 60, 0));
        DiagnosticHostFramePhases phases = new(17, 10, 10, 3, 5)
        {
            Native = new(2, 1, 1, 0, 3, 0, 0, 2, 0, 1, 0)
        };
        collector.Record(1, 150, 140, 148, new(true, 1, 1, 5, 0, 60, 0), phases);
        collector.Record(2, 195, 182, 193, new(true, 2, 1, 5, 0, 60, 0), new(25, 15, 10, 4, 5));
        DiagnosticSlowPresent[] rows = collector.Complete().SlowestPresents;
        DiagnosticExclusiveHostPhases host = Assert.IsType<DiagnosticExclusiveHostPhases>(rows[0].HostPhases);
        Assert.Equal("Aligned", rows[0].HostPhaseStatus);
        Assert.Equal(rows[0].IntervalMilliseconds, rows[0].TimerWaitMilliseconds + rows[0].PresentMilliseconds + host.UpdateOtherMilliseconds + host.BattlePresentationMilliseconds + host.DrawOtherMilliseconds + host.BattleSceneMilliseconds + host.PostPresentMilliseconds + host.UninstrumentedMilliseconds);
        Assert.Equal(7, host.UpdateOtherMilliseconds);
        Assert.Equal(7, host.DrawOtherMilliseconds);
        DiagnosticNativeIntervalPhases native = Assert.IsType<DiagnosticNativeIntervalPhases>(rows[0].NativePhases);
        Assert.Equal(2, native.Callbacks);
        Assert.Equal(8, native.TotalMilliseconds);
        Assert.Equal(3, native.ModelsMilliseconds);
        Assert.Equal("OverlappingBoundary", rows[1].HostPhaseStatus);
        Assert.Null(rows[1].HostPhases);
        Assert.Null(rows[1].NativePhases);
    }

    [Fact]
    public void Native_phase_accumulator_resets_at_each_present()
    {
        DiagnosticHostPhaseTiming collector = new(true);
        Memoria.FF8.Runtime.INativeBattlePhaseTimingSink sink = collector;
        sink.Record(Memoria.FF8.Runtime.NativeBattleTimedPhase.Models, 30);
        sink.Record(Memoria.FF8.Runtime.NativeBattleTimedPhase.Models, 20);
        sink.Record(Memoria.FF8.Runtime.NativeBattleTimedPhase.OrderingExecution, 10);
        sink.CompleteCallback();
        sink.CompleteCallback();
        DiagnosticNativeFramePhases first = collector.CompleteInterval().Native;
        Assert.Equal(2, first.Callbacks);
        Assert.Equal(50, first.Models);
        Assert.Equal(60, first.Total);
        Assert.Equal(default, collector.CompleteInterval().Native);
    }

    [Fact]
    public void Native_cpu_cycles_partition_callbacks_reset_and_report_the_aligned_paired_wall_ticks()
    {
        Queue<ulong> readings = new([100, 110, 145, 200, 207, 225, 300, 304, 310]);
        DiagnosticHostPhaseTiming timing = new(true, true, Read);
        INativeBattlePhaseTimingSink sink = timing;
        Callback(2, 8);
        Callback(3, 7);
        DiagnosticHostFramePhases first = timing.CompleteInterval() with { Update = 25, BattlePresentationUpdate = 20 };
        DiagnosticNativeCpuCycles observed = Assert.IsType<DiagnosticNativeCpuCycles>(first.NativeCpuCycles);
        Assert.True(observed.Available);
        Assert.Equal(0, observed.InvalidObservations);
        Assert.Equal(42u, observed.ThreadId);
        Assert.Equal(2, observed.Cycles!.Value.Callbacks);
        Assert.Equal(17, observed.Cycles.Value.Setup);
        Assert.Equal(53, observed.Cycles.Value.Models);
        Assert.Equal(70, observed.TotalCycles);
        Assert.Equal(first.Native, observed.PairedWallTicks);

        Callback(1, 4);
        DiagnosticHostFramePhases second = timing.CompleteInterval() with { Update = 10, BattlePresentationUpdate = 5 };
        Assert.Equal(10, second.NativeCpuCycles!.TotalCycles);
        Assert.Equal(1, second.NativeCpuCycles.Cycles!.Value.Callbacks);
        Assert.Null(timing.CompleteInterval().NativeCpuCycles!.Cycles);
        Assert.Empty(readings);

        DiagnosticPresentationPacing report = new(1000, 3, 100, new(true, 0, 0, 0, 0, 60, 0));
        report.Record(1, 150, 140, 148, new(true, 1, 0, 0, 0, 60, 0), first);
        report.Record(2, 195, 182, 193, new(true, 2, 0, 0, 0, 60, 0), second);
        report.Record(3, 240, 230, 238, new(true, 3, 0, 0, 0, 60, 0), second with { Update = 50 });
        DiagnosticPacingSummary summary = report.Complete();
        DiagnosticNativeCpuCycles aggregate = Assert.IsType<DiagnosticNativeCpuCycles>(summary.NativeCpuCycles);
        Assert.Equal(3, aggregate.Cycles!.Value.Callbacks);
        Assert.Equal(80, aggregate.TotalCycles);
        Assert.Equal(25, aggregate.TotalPairedWallTicks);
        Assert.Equal(1, aggregate.InvalidObservations);
        Assert.Equal(observed, summary.SlowestPresents[0].NativeCpuCycles);
        Assert.Null(summary.SlowestPresents.Single(row => row.Frame == 3).NativeCpuCycles);

        bool Read(out uint thread, out ulong cycles)
        {
            thread = 42;
            cycles = readings.Dequeue();
            return true;
        }

        void Callback(long setup, long models)
        {
            sink.BeginCallback();
            sink.Record(NativeBattleTimedPhase.Setup, setup);
            sink.Record(NativeBattleTimedPhase.Models, models);
            sink.CompleteCallback();
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Native_cpu_counter_requires_both_diagnostic_options_and_reports_unavailable(bool pacing, bool requested)
    {
        int reads = 0;
        DiagnosticHostPhaseTiming timing = new(pacing, requested, Read);
        INativeBattlePhaseTimingSink sink = timing;
        Assert.Equal(pacing && requested, sink.DetailEnabled);
        sink.BeginCallback();
        sink.BeginDetail(NativeBattleTimedDetail.ModelSequence);
        sink.RecordDetail(NativeBattleTimedDetail.ModelSequence, 1);
        sink.Record(NativeBattleTimedPhase.Setup, 3);
        sink.CompleteCallback();
        DiagnosticNativeCpuCycles? observation = timing.CompleteInterval().NativeCpuCycles;
        if (pacing && requested)
        {
            Assert.NotNull(observation);
            Assert.Equal(1, reads);
            Assert.False(observation.Available);
            Assert.Equal(1, observation.InvalidObservations);
            Assert.Null(observation.ThreadId);
            Assert.Null(observation.TotalCycles);
        }
        else
        {
            Assert.Null(observation);
            Assert.Equal(0, reads);
        }

        bool Read(out uint thread, out ulong cycles)
        {
            reads++;
            thread = 0;
            cycles = 0;
            return false;
        }
    }

    [Fact]
    public void Native_cpu_details_preserve_nested_measurements_and_aggregate_only_aligned_intervals()
    {
        ulong stamp = 100;
        DiagnosticHostPhaseTiming timing = new(true, true, Read);
        INativeBattlePhaseTimingSink sink = timing;

        DiagnosticHostFramePhases first = MeasureInterval();
        DiagnosticHostFramePhases second = MeasureInterval();
        Assert.Null(timing.CompleteInterval().NativeCpuCycles!.Details);
        DiagnosticNativeCpuCycles native = Assert.IsType<DiagnosticNativeCpuCycles>(first.NativeCpuCycles);
        Assert.Equal(15, native.TotalCycles);
        Assert.Equal(15, native.Cycles!.Value.Models);
        Assert.Equal(9, native.TotalPairedWallTicks);
        var firstDetails = native.Details!;
        Assert.Equal(new DiagnosticNativeTimingDetail(1, 10, 7), firstDetails[NativeBattleTimedDetail.ModelSubmission]);
        Assert.Equal(new DiagnosticNativeTimingDetail(1, 3, 2), firstDetails[NativeBattleTimedDetail.ModelPacketPlacement]);
        Assert.Equal(new DiagnosticNativeTimingDetail(1, 0, 0), firstDetails[NativeBattleTimedDetail.ModelSequence]);
        Assert.False(firstDetails.ContainsKey(NativeBattleTimedDetail.OrderingPass2));

        DiagnosticPresentationPacing report = new(1000, 3, 100, new(true, 0, 0, 0, 0, 60, 0));
        report.Record(1, 150, 140, 148, new(true, 1, 0, 0, 0, 60, 0), first);
        report.Record(2, 195, 182, 193, new(true, 2, 0, 0, 0, 60, 0), second);
        report.Record(3, 240, 230, 238, new(true, 3, 0, 0, 0, 60, 0), second with { Update = 50 });
        DiagnosticPacingSummary summary = report.Complete();
        DiagnosticNativeCpuCycles aggregate = Assert.IsType<DiagnosticNativeCpuCycles>(summary.NativeCpuCycles);
        Assert.Equal(30, aggregate.TotalCycles);
        Assert.Equal(1, aggregate.InvalidObservations);
        var aggregateDetails = aggregate.Details!;
        Assert.Equal(new DiagnosticNativeTimingDetail(2, 20, 14), aggregateDetails[NativeBattleTimedDetail.ModelSubmission]);
        Assert.Equal(new DiagnosticNativeTimingDetail(2, 6, 4), aggregateDetails[NativeBattleTimedDetail.ModelPacketPlacement]);
        Assert.Same(native, summary.SlowestPresents[0].NativeCpuCycles);

        DiagnosticHostFramePhases MeasureInterval()
        {
            sink.BeginCallback();
            sink.BeginDetail(NativeBattleTimedDetail.ModelSequence);
            sink.RecordDetail(NativeBattleTimedDetail.ModelSequence, 0);
            stamp += 2;
            sink.BeginDetail(NativeBattleTimedDetail.ModelSubmission);
            stamp += 3;
            sink.BeginDetail(NativeBattleTimedDetail.ModelPacketPlacement);
            stamp += 3;
            sink.RecordDetail(NativeBattleTimedDetail.ModelPacketPlacement, 2);
            stamp += 4;
            sink.RecordDetail(NativeBattleTimedDetail.ModelSubmission, 7);
            stamp += 3;
            sink.Record(NativeBattleTimedPhase.Models, 9);
            sink.CompleteCallback();
            return timing.CompleteInterval() with { Update = 15, BattlePresentationUpdate = 12 };
        }

        bool Read(out uint thread, out ulong cycles)
        {
            thread = 42;
            cycles = stamp;
            return true;
        }
    }

    [Theory]
    [InlineData("unbalanced")]
    [InlineData("out of order")]
    [InlineData("reentrant")]
    [InlineData("read failure")]
    [InlineData("thread change")]
    [InlineData("backwards")]
    [InlineData("overflow")]
    public void Invalid_native_cpu_detail_discards_its_callback_and_preserves_the_next_observation(string fault)
    {
        ulong stamp = 100;
        uint thread = 42;
        bool readSucceeds = true;
        DiagnosticHostPhaseTiming timing = new(true, true, Read);
        INativeBattlePhaseTimingSink sink = timing;

        sink.BeginCallback();
        sink.BeginDetail(NativeBattleTimedDetail.ModelSubmission);
        stamp += 10;
        switch (fault)
        {
            case "unbalanced":
                break;
            case "out of order":
                sink.BeginDetail(NativeBattleTimedDetail.ModelPacketPlacement);
                sink.RecordDetail(NativeBattleTimedDetail.ModelSubmission, 1);
                break;
            case "reentrant":
                sink.BeginDetail(NativeBattleTimedDetail.ModelSubmission);
                break;
            default:
                stamp = fault == "backwards" ? 99 : fault == "overflow" ? ulong.MaxValue : stamp;
                thread = fault == "thread change" ? 43u : 42u;
                readSucceeds = fault != "read failure";
                sink.RecordDetail(NativeBattleTimedDetail.ModelSubmission, 1);
                break;
        }
        sink.Record(NativeBattleTimedPhase.Models, 8);
        sink.CompleteCallback();

        stamp = 200;
        thread = 42;
        readSucceeds = true;
        sink.BeginCallback();
        sink.BeginDetail(NativeBattleTimedDetail.OrderingTraversal);
        stamp += 5;
        sink.RecordDetail(NativeBattleTimedDetail.OrderingTraversal, 4);
        stamp += 2;
        sink.Record(NativeBattleTimedPhase.OrderingExecution, 6);
        sink.CompleteCallback();

        DiagnosticHostFramePhases frame = timing.CompleteInterval();
        DiagnosticNativeCpuCycles received = Assert.IsType<DiagnosticNativeCpuCycles>(frame.NativeCpuCycles);
        Assert.Equal(1, received.InvalidObservations);
        Assert.Equal(1, received.Cycles!.Value.Callbacks);
        Assert.Equal(7, received.TotalCycles);
        Assert.Equal(14, frame.Native.Total);
        var detail = Assert.Single(received.Details!);
        Assert.Equal(NativeBattleTimedDetail.OrderingTraversal, detail.Key);
        Assert.Equal(new DiagnosticNativeTimingDetail(1, 5, 4), detail.Value);

        bool Read(out uint threadId, out ulong cycles)
        {
            threadId = thread;
            cycles = stamp;
            return readSucceeds;
        }
    }

    [Theory]
    [InlineData(true, 42u, 109ul)]
    [InlineData(true, 43u, 115ul)]
    [InlineData(true, 42u, ulong.MaxValue)]
    [InlineData(false, 42u, 115ul)]
    public void Unusable_cpu_readings_discard_the_whole_callback_and_the_next_baseline_recovers(bool success, uint thread, ulong cycles)
    {
        Queue<(bool Success, uint Thread, ulong Cycles)> readings = new([(true, 42, 100), (true, 42, 110), (success, thread, cycles), (true, 42, 200), (true, 42, 202), (true, 42, 205)]);
        DiagnosticHostPhaseTiming timing = new(true, true, Read);
        INativeBattlePhaseTimingSink sink = timing;
        for (int callback = 0; callback < 2; callback++)
        {
            sink.BeginCallback();
            sink.Record(NativeBattleTimedPhase.Setup, 2);
            sink.Record(NativeBattleTimedPhase.OrderingExecution, 8);
            sink.CompleteCallback();
        }
        DiagnosticHostFramePhases window = timing.CompleteInterval();
        DiagnosticNativeCpuCycles observation = Assert.IsType<DiagnosticNativeCpuCycles>(window.NativeCpuCycles);
        Assert.True(observation.Available);
        Assert.Equal(42u, observation.ThreadId);
        Assert.Equal(1, observation.InvalidObservations);
        Assert.Equal(1, observation.Cycles!.Value.Callbacks);
        Assert.Equal(5, observation.TotalCycles);
        Assert.Equal(10, observation.TotalPairedWallTicks);
        Assert.Equal(20, window.Native.Total);

        bool Read(out uint threadId, out ulong count)
        {
            var reading = readings.Dequeue();
            threadId = reading.Thread;
            count = reading.Cycles;
            return reading.Success;
        }
    }

    [Fact]
    public void Gc_annotation_uses_cohort_baseline_and_retained_observation_boundaries_without_changing_phase_totals()
    {
        DiagnosticPresentationPacing collector = new(1000, 2, 100, new(true, 0, 0, 0, 0, 60, 0), new(105, TimeSpan.TicksPerMillisecond * 10, 3));
        collector.Record(1, 150, 140, 148, new(true, 1, 1, 5, 0, 60, 0), new(22, 10, 10, 3, 5), new(151, TimeSpan.TicksPerMillisecond * 70, 4));
        collector.Record(2, 190, 180, 188, new(true, 2, 1, 5, 0, 60, 0), gcObservation: new(192, TimeSpan.TicksPerMillisecond * 80, 4));
        DiagnosticPacingSummary report = collector.Complete();
        DiagnosticGcPauseAnnotation first = Assert.IsType<DiagnosticGcPauseAnnotation>(report.SlowestPresents[0].GcPauseAnnotation);
        Assert.Equal(105, first.ObservationBeginTicks);
        Assert.Equal(151, first.ObservationEndTicks);
        Assert.Equal(60, first.ReportedProcessGcPauseMilliseconds);
        Assert.Equal(1, first.Gen2Collections);
        Assert.Equal(50, report.SlowestPresents[0].IntervalMilliseconds);
        Assert.Equal(10, report.SlowestPresents[0].HostPhases!.BattlePresentationMilliseconds);
        DiagnosticGcPauseAnnotation second = Assert.IsType<DiagnosticGcPauseAnnotation>(report.SlowestPresents[1].GcPauseAnnotation);
        Assert.Equal(151, second.ObservationBeginTicks);
        Assert.Equal(192, second.ObservationEndTicks);
        Assert.Equal(10, second.ReportedProcessGcPauseMilliseconds);
        Assert.Equal(0, second.Gen2Collections);
        Assert.Equal(first, Assert.Single(collector.Complete(150).SlowestPresents).GcPauseAnnotation);
        Assert.Empty(collector.Complete(149).SlowestPresents);
    }

    [Fact]
    public void Invalid_present_reseeds_gc_correlation_and_overflow_does_not_modify_retained_rows()
    {
        DiagnosticPresentationPacing collector = new(1000, 1, 100, new(true, 0, 0, 0, 0, 60, 0), new(100, 0, 0));
        collector.Record(1, 100, 101, 100, new(true, 1, 0, 0, 0, 60, 0), gcObservation: new(110, TimeSpan.TicksPerMillisecond * 20, 1));
        collector.Record(2, 150, 140, 150, new(true, 2, 0, 0, 0, 60, 0), gcObservation: new(151, TimeSpan.TicksPerMillisecond * 25, 1));
        collector.Record(3, 200, 190, 200, new(true, 3, 0, 0, 0, 60, 0), gcObservation: new(201, TimeSpan.TicksPerMillisecond * 90, 2));
        DiagnosticPacingSummary report = collector.Complete();
        Assert.Equal(1, report.InvalidObservations);
        Assert.Equal(1, report.OverflowObservations);
        DiagnosticGcPauseAnnotation annotation = Assert.IsType<DiagnosticGcPauseAnnotation>(Assert.Single(report.SlowestPresents).GcPauseAnnotation);
        Assert.Equal(110, annotation.ObservationBeginTicks);
        Assert.Equal(151, annotation.ObservationEndTicks);
        Assert.Equal(5, annotation.ReportedProcessGcPauseMilliseconds);
        Assert.Equal(0, annotation.Gen2Collections);
    }

    [Fact]
    public void Regressing_gc_counters_omit_only_the_optional_annotation_and_recover_from_the_new_baseline()
    {
        DiagnosticPresentationPacing collector = new(1000, 2, 100, new(true, 0, 0, 0, 0, 60, 0), new(100, TimeSpan.TicksPerMillisecond * 20, 2));
        collector.Record(1, 150, 140, 150, new(true, 1, 0, 0, 0, 60, 0), gcObservation: new(151, TimeSpan.TicksPerMillisecond * 10, 1));
        collector.Record(2, 190, 180, 190, new(true, 2, 0, 0, 0, 60, 0), gcObservation: new(191, TimeSpan.TicksPerMillisecond * 12, 1));
        DiagnosticPacingSummary report = collector.Complete();
        Assert.Equal(0, report.InvalidObservations);
        Assert.Equal(2, report.Samples);
        Assert.Null(report.SlowestPresents[0].GcPauseAnnotation);
        Assert.DoesNotContain("GcPauseAnnotation", System.Text.Json.JsonSerializer.Serialize(report.SlowestPresents[0]), StringComparison.Ordinal);
        Assert.Equal(2, report.SlowestPresents[1].GcPauseAnnotation!.ReportedProcessGcPauseMilliseconds);
    }
}
