using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePhaseTimingTests
{
    [Fact]
    public void Callback_phases_are_sequential_and_nested_observers_restore_the_previous_sink()
    {
        Sink outer = new();
        Sink inner = new();
        using (NativeBattlePhaseTiming.Observe(outer))
        {
            NativeBattlePhaseTiming.CallbackScope callback = NativeBattlePhaseTiming.BeginCallback();
            callback.Advance(NativeBattleTimedPhase.Models);
            using (NativeBattlePhaseTiming.Observe(inner))
            {
                NativeBattlePhaseTiming.CallbackScope nested = NativeBattlePhaseTiming.BeginCallback();
                nested.Advance(NativeBattleTimedPhase.OrderingExecution);
                nested.Dispose();
                nested.Dispose();
            }
            callback.Advance(NativeBattleTimedPhase.RenderTail);
            callback.Dispose();
            NativeBattlePhaseTiming.CallbackScope restored = NativeBattlePhaseTiming.BeginCallback();
            restored.Dispose();
        }
        NativeBattlePhaseTiming.CallbackScope disabled = NativeBattlePhaseTiming.BeginCallback();
        disabled.Advance(NativeBattleTimedPhase.Models);
        disabled.Dispose();
        Assert.Equal(2, outer.Callbacks);
        Assert.Equal(1, inner.Callbacks);
        Assert.Equal(new[] { "Begin", "Setup", "Models", "RenderTail", "Complete", "Begin", "Setup", "Complete" }, outer.Notifications);
        Assert.Equal(new[] { "Begin", "Setup", "OrderingExecution", "Complete" }, inner.Notifications);
        Assert.Equal(new[] { NativeBattleTimedPhase.Setup, NativeBattleTimedPhase.Models, NativeBattleTimedPhase.RenderTail, NativeBattleTimedPhase.Setup }, outer.Phases);
        Assert.Equal(new[] { NativeBattleTimedPhase.Setup, NativeBattleTimedPhase.OrderingExecution }, inner.Phases);
        Assert.All(outer.Ticks, ticks => Assert.True(ticks >= 0));
    }

    [Fact]
    public void Disabling_an_observer_temporarily_does_not_leak_callback_observations()
    {
        Sink sink = new();
        using (NativeBattlePhaseTiming.Observe(sink))
        {
            using (NativeBattlePhaseTiming.Observe(null))
            {
                NativeBattlePhaseTiming.CallbackScope disabled = NativeBattlePhaseTiming.BeginCallback();
                disabled.Dispose();
            }
            NativeBattlePhaseTiming.CallbackScope callback = NativeBattlePhaseTiming.BeginCallback();
            try
            {
                throw new InvalidOperationException("Diagnostic failure");
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                callback.Dispose();
            }
        }
        Assert.Equal(1, sink.Callbacks);
        Assert.Equal(new[] { "Begin", "Setup", "Complete" }, sink.Notifications);
        Assert.Equal(NativeBattleTimedPhase.Setup, Assert.Single(sink.Phases));
    }

    [Fact]
    public void Disabled_details_do_not_notify_or_allocate_with_or_without_a_phase_observer()
    {
        Sink phaseOnly = new();
        DetailSink disabled = new(false);
        MeasureManyDetails();
        long before = GC.GetAllocatedBytesForCurrentThread();
        MeasureManyDetails();
        using (NativeBattlePhaseTiming.Observe(disabled))
        {
            MeasureManyDetails();
        }
        using (NativeBattlePhaseTiming.Observe(phaseOnly))
        {
            MeasureManyDetails();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocated);
        Assert.Empty(disabled.Details);
        Assert.Empty(phaseOnly.Notifications);

        static void MeasureManyDetails()
        {
            for (int index = 0; index < 1000; index++)
            {
                using NativeBattlePhaseTiming.DetailScope measured = NativeBattlePhaseTiming.MeasureDetail(NativeBattleTimedDetail.ModelSequence);
            }
        }
    }

    [Fact]
    public void Nested_details_and_observers_close_in_order_when_the_measured_work_throws()
    {
        DetailSink outer = new(true);
        DetailSink inner = new(true);
        using (NativeBattlePhaseTiming.Observe(outer))
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using NativeBattlePhaseTiming.DetailScope submission = NativeBattlePhaseTiming.MeasureDetail(NativeBattleTimedDetail.ModelSubmission);
                using (NativeBattlePhaseTiming.Observe(inner))
                {
                    using NativeBattlePhaseTiming.DetailScope placement = NativeBattlePhaseTiming.MeasureDetail(NativeBattleTimedDetail.ModelPacketPlacement);
                    throw new InvalidOperationException("Diagnostic failure");
                }
            });
            using NativeBattlePhaseTiming.DetailScope sequence = NativeBattlePhaseTiming.MeasureDetail(NativeBattleTimedDetail.ModelSequence);
        }

        Assert.Equal(new[] { "Begin:ModelSubmission", "Record:ModelSubmission", "Begin:ModelSequence", "Record:ModelSequence" }, outer.Details);
        Assert.Equal(new[] { "Begin:ModelPacketPlacement", "Record:ModelPacketPlacement" }, inner.Details);
        Assert.All(outer.Ticks.Concat(inner.Ticks), ticks => Assert.True(ticks >= 0));
        Assert.True(outer.Ticks[0] >= Assert.Single(inner.Ticks));
    }

    [Fact]
    public void Ordering_details_cover_traversal_owner_checks_passes_and_publication()
    {
        DetailSink sink = new(true);
        ImmutableArray<byte> lengths = ImmutableArray.CreateRange(new byte[256]).SetItem(0x20, 4);
        ImmutableArray<uint> handlers = ImmutableArray.CreateRange(new uint[256]).SetItem(0x20, 1);
        NativeBattlePrimitiveTables tables = new(lengths, ImmutableArray.CreateRange(new byte[256]), handlers, handlers, handlers);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear().Prepend(17, new("packets", 0, 0), [0x04000000u, 0x20102030u, 0, 8, 8 << 16], 0, []);
        using (NativeBattlePhaseTiming.Observe(sink))
        {
            var result = NativeBattleLogicalPasses.Execute(ordering, tables, new(0), 0u);
            Assert.Equal(3, result.Commands.Length);
        }

        NativeBattleTimedDetail[] expected = [NativeBattleTimedDetail.OrderingTraversal, NativeBattleTimedDetail.OrderingOwnerValidation, NativeBattleTimedDetail.OrderingPass0, NativeBattleTimedDetail.OrderingPass1, NativeBattleTimedDetail.OrderingPass2, NativeBattleTimedDetail.OrderingPublication];
        Assert.Equal(expected.SelectMany(detail => new[] { $"Begin:{detail}", $"Record:{detail}" }), sink.Details);
        Assert.All(sink.Ticks, ticks => Assert.True(ticks >= 0));
    }

    private sealed class DetailSink(bool enabled) : INativeBattlePhaseTimingSink
    {
        internal List<string> Details { get; } = [];
        internal List<long> Ticks { get; } = [];
        public bool DetailEnabled => enabled;

        public void BeginDetail(NativeBattleTimedDetail detail)
        {
            Details.Add($"Begin:{detail}");
        }

        public void RecordDetail(NativeBattleTimedDetail detail, long elapsedTicks)
        {
            Details.Add($"Record:{detail}");
            Ticks.Add(elapsedTicks);
        }

        public void Record(NativeBattleTimedPhase phase, long elapsedTicks)
        {
        }

        public void CompleteCallback()
        {
        }
    }

    private sealed class Sink : INativeBattlePhaseTimingSink
    {
        internal List<NativeBattleTimedPhase> Phases { get; } = [];
        internal List<long> Ticks { get; } = [];
        internal List<string> Notifications { get; } = [];
        internal int Callbacks { get; private set; }

        public void BeginCallback()
        {
            Notifications.Add("Begin");
        }

        public void Record(NativeBattleTimedPhase phase, long elapsedTicks)
        {
            Phases.Add(phase);
            Ticks.Add(elapsedTicks);
            Notifications.Add(phase.ToString());
        }

        public void CompleteCallback()
        {
            Callbacks++;
            Notifications.Add("Complete");
        }
    }
}
