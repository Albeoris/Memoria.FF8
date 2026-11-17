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

/// <summary>Optional detail measurements within a phase. Model submission includes packet placement.</summary>
public enum NativeBattleTimedDetail
{
    ModelSequence,
    ModelSubmission,
    ModelPacketPlacement,
    OrderingTraversal,
    OrderingOwnerValidation,
    OrderingPass0,
    OrderingPass1,
    OrderingPass2,
    OrderingPublication
}

public interface INativeBattlePhaseTimingSink
{
    bool DetailEnabled => false;

    void BeginDetail(NativeBattleTimedDetail detail)
    {
    }

    void RecordDetail(NativeBattleTimedDetail detail, long elapsedTicks)
    {
    }

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

    internal static DetailScope MeasureDetail(NativeBattleTimedDetail detail)
    {
        if (current is not { DetailEnabled: true } sink)
        {
            return default;
        }
        sink.BeginDetail(detail);
        return new(sink, detail, Stopwatch.GetTimestamp());
    }

    internal struct DetailScope(INativeBattlePhaseTimingSink sink, NativeBattleTimedDetail detail, long started) : IDisposable
    {
        private INativeBattlePhaseTimingSink? sink = sink;

        public void Dispose()
        {
            if (sink is not { } observer)
            {
                return;
            }
            sink = null;
            observer.RecordDetail(detail, Stopwatch.GetTimestamp() - started);
        }
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
