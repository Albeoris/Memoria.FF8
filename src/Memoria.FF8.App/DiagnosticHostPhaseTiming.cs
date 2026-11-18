using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json.Serialization;

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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<NativeBattleTimedDetail, DiagnosticNativeTimingDetail>? Details { get; init; }

    public long? TotalCycles => Cycles?.Total;
    public long? TotalPairedWallTicks => PairedWallTicks?.Total;
}

// Detail scopes can nest. Each row measures its own calls.
internal readonly record struct DiagnosticNativeTimingDetail(long Calls, long CpuCycles, long PairedWallTicks)
{
    internal const int Count = (int)NativeBattleTimedDetail.EffectCinematicMesh + 1;

    internal bool CanAdd(DiagnosticNativeTimingDetail other)
    {
        return other.Calls <= long.MaxValue - Calls && other.CpuCycles <= long.MaxValue - CpuCycles && other.PairedWallTicks <= long.MaxValue - PairedWallTicks;
    }

    internal DiagnosticNativeTimingDetail Add(DiagnosticNativeTimingDetail other)
    {
        return new(Calls + other.Calls, CpuCycles + other.CpuCycles, PairedWallTicks + other.PairedWallTicks);
    }

    internal static ImmutableDictionary<NativeBattleTimedDetail, DiagnosticNativeTimingDetail>? ToReport(ReadOnlySpan<DiagnosticNativeTimingDetail> details)
    {
        ImmutableDictionary<NativeBattleTimedDetail, DiagnosticNativeTimingDetail>.Builder? report = null;
        for (int index = 0; index < details.Length; index++)
        {
            if (details[index].Calls != 0)
            {
                report ??= ImmutableDictionary.CreateBuilder<NativeBattleTimedDetail, DiagnosticNativeTimingDetail>();
                report.Add((NativeBattleTimedDetail)index, details[index]);
            }
        }
        return report?.ToImmutable();
    }
}

internal sealed class DiagnosticHostPhaseTiming(bool enabled, bool nativeCpuCycles = false, DiagnosticThreadCycleCounter.Reader? readThreadCycles = null) : INativeBattlePhaseTimingSink
{
    private readonly DiagnosticThreadCycleCounter.Reader? readCycles = enabled && nativeCpuCycles ? readThreadCycles ?? DiagnosticThreadCycleCounter.TryRead : null;
    private readonly (ulong Cycles, int Parent)?[]? detailStarts = enabled && nativeCpuCycles ? new (ulong, int)?[DiagnosticNativeTimingDetail.Count] : null;
    private readonly DiagnosticNativeTimingDetail[]? callbackDetails = enabled && nativeCpuCycles ? new DiagnosticNativeTimingDetail[DiagnosticNativeTimingDetail.Count] : null;
    private readonly DiagnosticNativeTimingDetail[]? intervalDetails = enabled && nativeCpuCycles ? new DiagnosticNativeTimingDetail[DiagnosticNativeTimingDetail.Count] : null;
    private DiagnosticHostFramePhases current;
    private uint? cycleThread;
    private bool intervalCyclesAvailable;
    private bool intervalThreadObserved;
    private long invalidCycleCallbacks;
    private DiagnosticNativeFramePhases intervalCycles;
    private DiagnosticNativeFramePhases intervalCycleWallTicks;
    private int callbackDepth;
    private int activeDetail = -1;
    private bool callbackCyclesValid;
    private ulong lastCycles;
    private ulong lastObservedCycles;
    private long callbackCycleTotal;
    private DiagnosticNativeFramePhases callbackCycles;
    private DiagnosticNativeFramePhases callbackCycleWallTicks;

    internal bool Enabled { get; } = enabled;
    bool INativeBattlePhaseTimingSink.DetailEnabled => readCycles is not null;

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
                {
                    Details = DiagnosticNativeTimingDetail.ToReport(intervalDetails)
                }
            };
            intervalCyclesAvailable = false;
            intervalThreadObserved = false;
            invalidCycleCallbacks = 0;
            intervalCycles = default;
            intervalCycleWallTicks = default;
            Array.Clear(intervalDetails!);
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
        activeDetail = -1;
        Array.Clear(detailStarts!);
        Array.Clear(callbackDetails!);
        if (ReadCycles(out lastCycles))
        {
            lastObservedCycles = lastCycles;
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

    private bool ReadCallbackCycles(out ulong observed)
    {
        if (!ReadCycles(out observed) || observed < lastObservedCycles)
        {
            return false;
        }
        lastObservedCycles = observed;
        return true;
    }

    void INativeBattlePhaseTimingSink.BeginDetail(NativeBattleTimedDetail detail)
    {
        if (!callbackCyclesValid)
        {
            return;
        }
        int index = (int)detail;
        (ulong Cycles, int Parent)?[] starts = detailStarts!;
        if ((uint)index >= starts.Length || starts[index] is not null || !ReadCallbackCycles(out ulong started))
        {
            callbackCyclesValid = false;
            return;
        }
        starts[index] = (started, activeDetail);
        activeDetail = index;
    }

    void INativeBattlePhaseTimingSink.RecordDetail(NativeBattleTimedDetail detail, long elapsedTicks)
    {
        if (!callbackCyclesValid)
        {
            return;
        }
        int index = (int)detail;
        (ulong Cycles, int Parent)?[] starts = detailStarts!;
        if (index != activeDetail || (uint)index >= starts.Length || starts[index] is not { } start || elapsedTicks < 0 ||
            !ReadCallbackCycles(out ulong observed) || observed - start.Cycles > long.MaxValue)
        {
            callbackCyclesValid = false;
            return;
        }
        starts[index] = null;
        activeDetail = start.Parent;
        DiagnosticNativeTimingDetail measured = new(1, (long)(observed - start.Cycles), elapsedTicks);
        ref DiagnosticNativeTimingDetail accumulated = ref callbackDetails![index];
        if (!accumulated.CanAdd(measured))
        {
            callbackCyclesValid = false;
            return;
        }
        accumulated = accumulated.Add(measured);
    }

    void INativeBattlePhaseTimingSink.Record(NativeBattleTimedPhase phase, long elapsedTicks)
    {
        current = current with { Native = current.Native.Add(phase, elapsedTicks) };
        if (!callbackCyclesValid)
        {
            return;
        }
        if (!ReadCallbackCycles(out ulong observed) || observed - lastCycles > (ulong)(long.MaxValue - callbackCycleTotal) ||
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
        if (callbackCyclesValid && activeDetail < 0 && callbackCycleTotal <= long.MaxValue - intervalCycles.Total &&
            callbackCycleWallTicks.Total <= long.MaxValue - intervalCycleWallTicks.Total && TryAccumulateDetails())
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

    private bool TryAccumulateDetails()
    {
        DiagnosticNativeTimingDetail[] interval = intervalDetails!;
        DiagnosticNativeTimingDetail[] callback = callbackDetails!;
        for (int index = 0; index < interval.Length; index++)
        {
            if (!interval[index].CanAdd(callback[index]))
            {
                return false;
            }
        }
        for (int index = 0; index < interval.Length; index++)
        {
            interval[index] = interval[index].Add(callback[index]);
        }
        return true;
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
