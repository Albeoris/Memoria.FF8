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
        Assert.Equal(NativeBattleTimedPhase.Setup, Assert.Single(sink.Phases));
    }

    private sealed class Sink : INativeBattlePhaseTimingSink
    {
        internal List<NativeBattleTimedPhase> Phases { get; } = [];
        internal List<long> Ticks { get; } = [];
        internal int Callbacks { get; private set; }

        public void Record(NativeBattleTimedPhase phase, long elapsedTicks)
        {
            Phases.Add(phase);
            Ticks.Add(elapsedTicks);
        }

        public void CompleteCallback()
        {
            Callbacks++;
        }
    }
}
