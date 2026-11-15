using System.Diagnostics;

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
    internal bool FitsInterval(long timerWait, long nonPresent)
    {
        return timerWait <= Update && BattlePresentationUpdate <= Update - timerWait && BattleSceneRender <= Draw &&
            Update <= nonPresent && Draw <= nonPresent - Update && PostPresent <= nonPresent - Update - Draw;
    }
}

internal sealed class DiagnosticHostPhaseTiming(bool enabled)
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
