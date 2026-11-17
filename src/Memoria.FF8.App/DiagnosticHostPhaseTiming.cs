using System.Diagnostics;

using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum DiagnosticHostPhase
{
    Update,
    BattlePresentationUpdate,
    Draw,
    BattleSceneRender,
    PostPresent
}

// Whole-host Update and Draw include their respective battle subscopes.
internal readonly record struct DiagnosticHostFramePhases(long Update, long BattlePresentationUpdate, long Draw, long BattleSceneRender, long PostPresent)
{
    internal DiagnosticNativeFramePhases Native { get; init; }

    internal bool FitsInterval(long timerWait, long nonPresent)
    {
        long work = nonPresent - timerWait;
        return BattlePresentationUpdate <= Update && BattleSceneRender <= Draw &&
            Update <= work && Draw <= work - Update && PostPresent <= work - Update - Draw;
    }
}

internal readonly record struct DiagnosticNativeFramePhases(long Callbacks, long Setup, long RequestsAndEffects, long PacketsContactsAndStatus, long Models, long StageAndProducerCommit, long CameraAndSound, long OrderingExecution, long RenderTail, long SceneReplay, long Completion)
{
    internal long Total => Setup + RequestsAndEffects + PacketsContactsAndStatus + Models + StageAndProducerCommit + CameraAndSound + OrderingExecution + RenderTail + SceneReplay + Completion;

    internal DiagnosticNativeFramePhases Add(NativeBattleTimedPhase phase, long elapsed)
    {
        return phase switch
        {
            NativeBattleTimedPhase.Setup => this with { Setup = Setup + elapsed },
            NativeBattleTimedPhase.RequestsAndEffects => this with { RequestsAndEffects = RequestsAndEffects + elapsed },
            NativeBattleTimedPhase.PacketsContactsAndStatus => this with { PacketsContactsAndStatus = PacketsContactsAndStatus + elapsed },
            NativeBattleTimedPhase.Models => this with { Models = Models + elapsed },
            NativeBattleTimedPhase.StageAndProducerCommit => this with { StageAndProducerCommit = StageAndProducerCommit + elapsed },
            NativeBattleTimedPhase.CameraAndSound => this with { CameraAndSound = CameraAndSound + elapsed },
            NativeBattleTimedPhase.OrderingExecution => this with { OrderingExecution = OrderingExecution + elapsed },
            NativeBattleTimedPhase.RenderTail => this with { RenderTail = RenderTail + elapsed },
            NativeBattleTimedPhase.SceneReplay => this with { SceneReplay = SceneReplay + elapsed },
            NativeBattleTimedPhase.Completion => this with { Completion = Completion + elapsed },
            _ => throw new ArgumentOutOfRangeException(nameof(phase))
        };
    }
}

internal sealed class DiagnosticHostPhaseTiming(bool enabled) : INativeBattlePhaseTimingSink
{
    private DiagnosticHostFramePhases current;

    internal bool Enabled { get; } = enabled;

    internal Scope Measure(DiagnosticHostPhase phase)
    {
        return Enabled ? new(this, phase, Stopwatch.GetTimestamp()) : default;
    }

    internal DiagnosticHostFramePhases CompleteInterval()
    {
        DiagnosticHostFramePhases phases = current;
        current = default;
        return phases;
    }

    private void Record(DiagnosticHostPhase phase, long elapsed)
    {
        current = phase switch
        {
            DiagnosticHostPhase.Update => current with { Update = current.Update + elapsed },
            DiagnosticHostPhase.BattlePresentationUpdate => current with { BattlePresentationUpdate = current.BattlePresentationUpdate + elapsed },
            DiagnosticHostPhase.Draw => current with { Draw = current.Draw + elapsed },
            DiagnosticHostPhase.BattleSceneRender => current with { BattleSceneRender = current.BattleSceneRender + elapsed },
            DiagnosticHostPhase.PostPresent => current with { PostPresent = current.PostPresent + elapsed },
            _ => throw new ArgumentOutOfRangeException(nameof(phase))
        };
    }

    void INativeBattlePhaseTimingSink.Record(NativeBattleTimedPhase phase, long elapsedTicks)
    {
        current = current with { Native = current.Native.Add(phase, elapsedTicks) };
    }

    void INativeBattlePhaseTimingSink.CompleteCallback()
    {
        current = current with { Native = current.Native with { Callbacks = current.Native.Callbacks + 1 } };
    }

    internal readonly struct Scope(DiagnosticHostPhaseTiming? owner, DiagnosticHostPhase phase, long started) : IDisposable
    {
        public void Dispose()
        {
            if (owner is not null)
            {
                owner.Record(phase, Stopwatch.GetTimestamp() - started);
            }
        }
    }
}
