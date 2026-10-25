using System.Diagnostics;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record DiagnosticLiveExecution(int Index, bool Completed, bool CleanupCompleted, int TailPresentedFrames, long StartCallback, long EndCallback, long StartFrame, long EndFrame, long RendererFrames, long PrecedingPresentationTicks, TelemetryOwnerState StartOwners, TelemetryOwnerState EndOwners, BattleDiagnosticEffectDispatch[] Dispatches, TelemetryFrameSummary Frames, TelemetryTrafficSnapshot Traffic, BattleEvent[] Events, BattleDiagnosticResolution[] Resolutions, BattleDiagnosticCombatant[] Before, BattleDiagnosticCombatant[] After, long DroppedEvents, string? IntegrityError, long AllocatedBytes, int[] Collections, NativeBattlePoolObservation PoolBefore, NativeBattlePoolObservation PoolAfter, long EffectStartedTicks, long EffectCompletedTicks, double EffectDurationMs);

/// <summary>Observes one live session. Scored windows include ordinary admission, first-use work, cleanup and presented tails.</summary>
internal sealed class DiagnosticLiveTelemetry : INativeBattleTelemetrySink, IDisposable
{
    private readonly BattleSession battle;
    private readonly string instance;
    private readonly List<DiagnosticLiveExecution> executions = [];
    private readonly List<BattleEvent> events = [];
    private readonly List<BattleDiagnosticResolution> resolutions = [];
    private readonly List<BattleDiagnosticEffectDispatch> dispatches = [];
    private readonly TelemetryCoverageManifest coverage;
    private TelemetryTrafficCollector? traffic;
    private TelemetryFrameCollector? frames;
    private IDisposable? runtimeScope;
    private IDisposable? resourceScope;
    private BattleDiagnosticCombatant[] before = [];
    private TelemetryOwnerState? start;
    private long preceding;
    private long renders;
    private long previousRender;
    private long dropped;
    private long allocations;
    private int[] collections = [];
    private string? rendererOwner;
    private NativeBattlePoolObservation poolBefore;
    private long effectStartedTicks;
    private long effectCompletedTicks;
    private const int EventCapacity = 65536;

    internal DiagnosticLiveTelemetry(BattleSession battle, Guid instance, ushort expectedEffect = 1)
    {
        this.battle = battle;
        this.instance = instance.ToString();
        coverage = TelemetryCoverageManifest.ManagedPayload(expectedEffect);
        battle.DiagnosticEventPublished += ObserveEvent;
        battle.DiagnosticResolution += ObserveResolution;
        battle.DiagnosticEffectDispatched += ObserveDispatch;
    }

    internal TelemetryCoverageManifest Coverage => coverage;
    internal IReadOnlyList<DiagnosticLiveExecution> Executions => executions;
    internal bool Active => start is not null;

    private TelemetryOwnerState Owners(long frame)
    {
        return new(instance, 1, battle.DiagnosticLifecycleId.ToString(), rendererOwner ?? throw new InvalidOperationException("Live telemetry requires a bound renderer owner."), 1, battle.DiagnosticRestoreCount, battle.DiagnosticResetCount, battle.NativeHostCallbacks ?? battle.Frame, frame);
    }

    internal void Begin(long timestamp, long frame, long rendererVersion, string rendererId)
    {
        if (Active || executions.Count >= 2)
        {
            throw new InvalidOperationException("A live telemetry window cannot overlap or replace an execution.");
        }
        rendererOwner = rendererId;
        before = battle.ObserveDiagnosticCombatants();
        preceding = timestamp;
        start = Owners(frame);
        previousRender = rendererVersion;
        renders = dropped = 0;
        effectStartedTicks = effectCompletedTicks = 0;
        events.Clear();
        resolutions.Clear();
        dispatches.Clear();
        frames = new(timestamp, Stopwatch.Frequency, 65536);
        traffic = new(coverage);
        allocations = GC.GetTotalAllocatedBytes();
        poolBefore = NativeBattleTelemetry.PoolSnapshot();
        collections = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray();
        runtimeScope = NativeBattleTelemetry.BeginScope(this);
        resourceScope = ResourceTrafficTelemetry.BeginScope((site, bytes) => Record(site, NativeBattleTrafficKind.CpuCopyPayload, bytes));
    }

    public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
    {
        traffic?.Record(siteId, (TelemetryTrafficKind)kind, payloadBytes);
    }

    internal void MarkDrained(long timestamp)
    {
        if (Active && effectStartedTicks != 0)
        {
            effectCompletedTicks = timestamp;
        }
    }

    internal void Presented(long timestamp, long frame, string phase, long rendererVersion, int drawCalls, int textureUploads)
    {
        if (!Active)
        {
            return;
        }
        if (rendererVersion > previousRender && drawCalls > 0)
        {
            renders++;
        }
        previousRender = rendererVersion;
        frames!.RecordPresentation(new(timestamp, frame, phase, battle.NativeHostCallbacks ?? battle.Frame, renders, drawCalls, textureUploads));
    }

    internal void Complete(long frame, bool completed, int tailFrames, string? error)
    {
        if (start is null)
        {
            return;
        }
        runtimeScope?.Dispose();
        resourceScope?.Dispose();
        runtimeScope = resourceScope = null;
        TelemetryOwnerState end = Owners(frame);
        BattleDiagnosticCombatant[] after = battle.ObserveDiagnosticCombatants();
        NativeBattlePoolObservation poolAfter = NativeBattleTelemetry.PoolSnapshot();
        if (poolAfter.OutstandingCount != poolBefore.OutstandingCount || poolAfter.OutstandingBytes != poolBefore.OutstandingBytes)
        {
            error ??= "ARENA_WRITER_RENTAL_NOT_RELEASED";
        }
        double effectDuration = effectStartedTicks > 0 && effectCompletedTicks > effectStartedTicks ? (effectCompletedTicks - effectStartedTicks) * 1000.0 / Stopwatch.Frequency : 0;
        executions.Add(new(executions.Count + 1, completed, completed, tailFrames, start.NativeCallback, end.NativeCallback, start.FrameIndex, frame, renders, preceding, start, end, dispatches.ToArray(), frames!.Complete(), traffic!.Complete(), events.ToArray(), resolutions.ToArray(), before, after, dropped, error, GC.GetTotalAllocatedBytes() - allocations, collections.Select((count, index) => GC.CollectionCount(index) - count).ToArray(), poolBefore, poolAfter, effectStartedTicks, effectCompletedTicks, effectDuration));
        start = null;
        frames = null;
        traffic = null;
    }

    private void ObserveEvent(BattleEvent value)
    {
        if (Active)
        {
            if (events.Count < EventCapacity)
            {
                events.Add(value);
            }
            else
            {
                dropped++;
            }
        }
    }

    private void ObserveResolution(BattleDiagnosticResolution value)
    {
        if (Active)
        {
            if (resolutions.Count < EventCapacity)
            {
                resolutions.Add(value);
            }
            else
            {
                dropped++;
            }
        }
    }

    private void ObserveDispatch(BattleDiagnosticEffectDispatch value)
    {
        if (Active)
        {
            if (dispatches.Count < EventCapacity)
            {
                if (dispatches.Count == 0)
                {
                    effectStartedTicks = Stopwatch.GetTimestamp();
                }
                dispatches.Add(value);
            }
            else
            {
                dropped++;
            }
        }
    }

    public void Dispose()
    {
        runtimeScope?.Dispose();
        resourceScope?.Dispose();
        battle.DiagnosticEventPublished -= ObserveEvent;
        battle.DiagnosticResolution -= ObserveResolution;
        battle.DiagnosticEffectDispatched -= ObserveDispatch;
    }
}
