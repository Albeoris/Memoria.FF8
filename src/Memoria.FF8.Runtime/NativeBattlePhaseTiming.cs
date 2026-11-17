using System.Diagnostics;

namespace Memoria.FF8.Runtime;

public enum NativeBattleTimedPhase
{
    Setup,
    RequestsAndEffects,
    PacketsContactsAndStatus,
    Models,
    StageAndProducerCommit,
    CameraAndSound,
    OrderingExecution,
    RenderTail,
    SceneReplay,
    Completion
}

public interface INativeBattlePhaseTimingSink
{
    void BeginCallback()
    {
    }

    void Record(NativeBattleTimedPhase phase, long elapsedTicks);
    void CompleteCallback();
}

/// <summary>Optional synchronous wall-time attribution; phases partition one graphics callback and include pauses and scheduling delays.</summary>
public static class NativeBattlePhaseTiming
{
    [ThreadStatic]
    private static INativeBattlePhaseTimingSink? current;

    public static ObservationScope Observe(INativeBattlePhaseTimingSink? sink)
    {
        ObservationScope scope = new(current);
        current = sink;
        return scope;
    }

    internal static CallbackScope BeginCallback()
    {
        if (current is not { } sink)
        {
            return default;
        }
        sink.BeginCallback();
        return new(sink, Stopwatch.GetTimestamp());
    }

    public readonly struct ObservationScope : IDisposable
    {
        private readonly INativeBattlePhaseTimingSink? previous;

        internal ObservationScope(INativeBattlePhaseTimingSink? previous)
        {
            this.previous = previous;
        }

        public void Dispose()
        {
            current = previous;
        }
    }

    internal struct CallbackScope(INativeBattlePhaseTimingSink sink, long started) : IDisposable
    {
        private INativeBattlePhaseTimingSink? sink = sink;
        private long started = started;
        private NativeBattleTimedPhase phase;

        internal void Advance(NativeBattleTimedPhase next)
        {
            if (sink is null)
            {
                return;
            }
            long now = Stopwatch.GetTimestamp();
            sink.Record(phase, now - started);
            started = now;
            phase = next;
        }

        public void Dispose()
        {
            if (sink is null)
            {
                return;
            }
            sink.Record(phase, Stopwatch.GetTimestamp() - started);
            sink.CompleteCallback();
            sink = null;
        }
    }
}
