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
    internal DiagnosticNativeCpuCycles? NativeCpuCycles { get; init; }

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

    internal DiagnosticNativeFramePhases Add(in DiagnosticNativeFramePhases other)
    {
        return new(Callbacks + other.Callbacks, Setup + other.Setup, RequestsAndEffects + other.RequestsAndEffects,
            PacketsContactsAndStatus + other.PacketsContactsAndStatus, Models + other.Models, StageAndProducerCommit + other.StageAndProducerCommit,
            CameraAndSound + other.CameraAndSound, OrderingExecution + other.OrderingExecution, RenderTail + other.RenderTail, SceneReplay + other.SceneReplay,
            Completion + other.Completion);
    }

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

// Cycle counts include user and kernel execution for the observed thread. They have no elapsed-time conversion.
internal sealed record DiagnosticNativeCpuCycles(bool Available, long InvalidObservations, uint? ThreadId, DiagnosticNativeFramePhases? Cycles, DiagnosticNativeFramePhases? PairedWallTicks)
{
    public long? TotalCycles => Cycles?.Total;
    public long? TotalPairedWallTicks => PairedWallTicks?.Total;
}

internal sealed class DiagnosticHostPhaseTiming(bool enabled, bool nativeCpuCycles = false, DiagnosticThreadCycleCounter.Reader? readThreadCycles = null) : INativeBattlePhaseTimingSink
{
    private readonly DiagnosticThreadCycleCounter.Reader? readCycles = enabled && nativeCpuCycles ? readThreadCycles ?? DiagnosticThreadCycleCounter.TryRead : null;
    private DiagnosticHostFramePhases current;
    private uint? cycleThread;
    private bool intervalCyclesAvailable;
    private bool intervalThreadObserved;
    private long invalidCycleCallbacks;
    private DiagnosticNativeFramePhases intervalCycles;
    private DiagnosticNativeFramePhases intervalCycleWallTicks;
    private int callbackDepth;
    private bool callbackCyclesValid;
    private ulong lastCycles;
    private long callbackCycleTotal;
    private DiagnosticNativeFramePhases callbackCycles;
    private DiagnosticNativeFramePhases callbackCycleWallTicks;

    internal bool Enabled { get; } = enabled;

    internal Scope Measure(DiagnosticHostPhase phase)
    {
        return Enabled ? new(this, phase, Stopwatch.GetTimestamp()) : default;
    }

    internal DiagnosticHostFramePhases CompleteInterval()
    {
        DiagnosticHostFramePhases phases = current;
        current = default;
        if (readCycles is not null)
        {
            phases = phases with
            {
                NativeCpuCycles = new(intervalCyclesAvailable, invalidCycleCallbacks, intervalThreadObserved ? cycleThread : null,
                    intervalCycles.Callbacks > 0 ? intervalCycles : null, intervalCycles.Callbacks > 0 ? intervalCycleWallTicks : null)
            };
            intervalCyclesAvailable = false;
            intervalThreadObserved = false;
            invalidCycleCallbacks = 0;
            intervalCycles = default;
            intervalCycleWallTicks = default;
        }
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

    void INativeBattlePhaseTimingSink.BeginCallback()
    {
        if (readCycles is null)
        {
            return;
        }
        callbackDepth++;
        callbackCyclesValid = false;
        if (callbackDepth != 1)
        {
            return;
        }
        callbackCycles = default;
        callbackCycleWallTicks = default;
        callbackCycleTotal = 0;
        if (ReadCycles(out lastCycles))
        {
            callbackCyclesValid = true;
        }
    }

    private bool ReadCycles(out ulong cycles)
    {
        if (!readCycles!(out uint thread, out cycles) || thread == 0)
        {
            return false;
        }
        intervalCyclesAvailable = true;
        cycleThread ??= thread;
        if (thread != cycleThread)
        {
            return false;
        }
        intervalThreadObserved = true;
        return true;
    }

    void INativeBattlePhaseTimingSink.Record(NativeBattleTimedPhase phase, long elapsedTicks)
    {
        current = current with { Native = current.Native.Add(phase, elapsedTicks) };
        if (!callbackCyclesValid)
        {
            return;
        }
        if (!ReadCycles(out ulong observed) || observed < lastCycles || observed - lastCycles > (ulong)(long.MaxValue - callbackCycleTotal) ||
            elapsedTicks < 0 || elapsedTicks > long.MaxValue - callbackCycleWallTicks.Total)
        {
            callbackCyclesValid = false;
            return;
        }
        long delta = (long)(observed - lastCycles);
        lastCycles = observed;
        callbackCycleTotal += delta;
        callbackCycles = callbackCycles.Add(phase, delta);
        callbackCycleWallTicks = callbackCycleWallTicks.Add(phase, elapsedTicks);
    }

    void INativeBattlePhaseTimingSink.CompleteCallback()
    {
        current = current with { Native = current.Native with { Callbacks = current.Native.Callbacks + 1 } };
        if (readCycles is null)
        {
            return;
        }
        if (callbackDepth > 0)
        {
            callbackDepth--;
        }
        if (callbackCyclesValid && callbackCycleTotal <= long.MaxValue - intervalCycles.Total &&
            callbackCycleWallTicks.Total <= long.MaxValue - intervalCycleWallTicks.Total)
        {
            intervalCycles = intervalCycles.Add(callbackCycles with { Callbacks = 1 });
            intervalCycleWallTicks = intervalCycleWallTicks.Add(callbackCycleWallTicks with { Callbacks = 1 });
        }
        else
        {
            invalidCycleCallbacks++;
        }
        callbackCyclesValid = false;
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
