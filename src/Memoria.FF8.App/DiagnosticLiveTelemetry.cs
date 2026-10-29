using System.Diagnostics;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed record DiagnosticLiveExecution(int Index, bool Completed, bool CleanupCompleted, int TailPresentedFrames, long StartCallback, long EndCallback, long StartFrame, long EndFrame, long RendererFrames, long PrecedingPresentationTicks, TelemetryOwnerState StartOwners, TelemetryOwnerState EndOwners, BattleDiagnosticEffectDispatch[] Dispatches, TelemetryFrameSummary Frames, TelemetryTrafficSnapshot Traffic, BattleEvent[] Events, BattleDiagnosticResolution[] Resolutions, BattleDiagnosticCombatant[] Before, BattleDiagnosticCombatant[] After, long DroppedEvents, string? IntegrityError, long AllocatedBytes, int[] Collections, NativeBattlePoolObservation PoolBefore, NativeBattlePoolObservation PoolAfter, long EffectStartedTicks, long EffectCompletedTicks, double EffectDurationMs, TelemetryProcessSample ProcessBefore, TelemetryProcessSample ProcessAfter)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DiagnosticPacingSummary? PresentationPacing { get; init; }
    public int ReflectionDispatchCount => Dispatches.Count(dispatch => dispatch.Reflection is not null);
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DiagnosticVisualChangesSummary? VisualChanges { get; init; }
}

/// <summary>Observes one live session. Scored windows include ordinary admission, first-use work, cleanup and presented tails.</summary>
internal sealed class DiagnosticLiveTelemetry : INativeBattleTelemetrySink, IDisposable
{
    private readonly BattleSession battle;
    private readonly string instance;
    private readonly ushort expectedEffect;
    private readonly byte? expectedNativeCommand;
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
    private TelemetryProcessSample? processBefore;
    private long effectStartedTicks;
    private long effectCompletedTicks;
    private readonly DiagnosticPresentationProgress presentationProgress = new(Stopwatch.Frequency);
    private const int EventCapacity = 65536;
    private readonly bool pacingEnabled = Environment.GetEnvironmentVariable("MEMORIA_DIAGNOSTIC_PACING") == "1";
    private DiagnosticPresentationPacing? pacing;
    private PresentationPacerObservation pacingObservation;
    private long presentBegin;
    private long presentEnd;
    private readonly bool visualChangesEnabled = Environment.GetEnvironmentVariable("MEMORIA_DIAGNOSTIC_VISUAL_CHANGES") == "1";
    private DiagnosticVisualChanges? visualChanges;
    private DiagnosticVisualChanges? firstVisualWindow;
    private DiagnosticVisualChanges? secondVisualWindow;
    private long observedVisualFrame = -1;
    private long successfulVisualFrame = -1;
    private long successfulVisualTimestamp;
    private NativeBattlePilotSubmission observedVisualSubmission;

    internal bool PacingEnabled => pacingEnabled;
    internal bool VisualChangesEnabled => visualChangesEnabled;

    internal void ObserveBattleImageRendered(long frame, NativeBattlePilotSubmission submission)
    {
        if (visualChangesEnabled)
        {
            observedVisualFrame = frame;
            observedVisualSubmission = submission;
        }
    }

    internal void ObserveBattleImagePresented(long frame, long timestamp)
    {
        if (visualChangesEnabled)
        {
            successfulVisualFrame = frame;
            successfulVisualTimestamp = timestamp;
        }
    }

    internal void ResolveBattleImage(long frame, bool? changed, bool droppedComparison = false)
    {
        if (firstVisualWindow?.Resolve(frame, changed, droppedComparison) == true)
        {
            return;
        }
        secondVisualWindow?.Resolve(frame, changed, droppedComparison);
    }

    internal void ObservePresentationPacing(long begin, long end, PresentationPacerObservation observation)
    {
        presentBegin = begin;
        presentEnd = end;
        pacingObservation = observation;
    }


    internal DiagnosticLiveTelemetry(BattleSession battle, Guid instance, ushort expectedEffect = 1, byte? expectedNativeCommand = null)
    {
        this.battle = battle;
        this.instance = instance.ToString();
        this.expectedEffect = expectedEffect;
        this.expectedNativeCommand = expectedNativeCommand;
        coverage = TelemetryCoverageManifest.ManagedPayload(expectedEffect);
        battle.DiagnosticEventPublished += ObserveEvent;
        battle.DiagnosticResolution += ObserveResolution;
        battle.DiagnosticEffectDispatched += ObserveDispatch;
    }

    internal TelemetryCoverageManifest Coverage => coverage;
    internal IReadOnlyList<DiagnosticLiveExecution> Executions => executions;
    internal bool Active => start is not null;
    internal DiagnosticPresentationProgressSnapshot PresentationProgress(int sequence) => presentationProgress.Snapshot(sequence, effectStartedTicks > 0, effectCompletedTicks > 0);
    internal DiagnosticPacingProgress? PresentationPacingProgress() => pacing?.SnapshotProgress();

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
        pacing = pacingEnabled ? new(Stopwatch.Frequency, 65536, timestamp, pacingObservation) : null;
        if (visualChangesEnabled)
        {
            visualChanges = new(Stopwatch.Frequency, 65536, timestamp, frame);
            if (executions.Count == 0)
            {
                firstVisualWindow = visualChanges;
            }
            else
            {
                secondVisualWindow = visualChanges;
            }
        }
        start = Owners(frame);
        previousRender = rendererVersion;
        renders = dropped = 0;
        effectStartedTicks = effectCompletedTicks = 0;
        presentationProgress.Reset(executions.Count + 1, timestamp);
        events.Clear();
        resolutions.Clear();
        dispatches.Clear();
        frames = new(timestamp, Stopwatch.Frequency, 65536);
        traffic = new(coverage);
        allocations = GC.GetTotalAllocatedBytes();
        poolBefore = NativeBattleTelemetry.PoolSnapshot();
        processBefore = TelemetryProcessSample.Capture();
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
        pacing?.Record(frame, timestamp, presentBegin, presentEnd, pacingObservation);
        visualChanges?.Presented(frame, successfulVisualFrame == frame ? successfulVisualTimestamp : timestamp, observedVisualFrame == frame, observedVisualFrame == frame ? observedVisualSubmission : default);
        presentationProgress.Presented(timestamp, effectStartedTicks, effectCompletedTicks);
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
        TelemetryProcessSample processAfter = TelemetryProcessSample.Capture();
        executions.Add(new(executions.Count + 1, completed, completed, tailFrames, start.NativeCallback, end.NativeCallback, start.FrameIndex, frame, renders, preceding, start, end, dispatches.ToArray(), frames!.Complete(), traffic!.Complete(), events.ToArray(), resolutions.ToArray(), before, after, dropped, error, GC.GetTotalAllocatedBytes() - allocations, collections.Select((count, index) => GC.CollectionCount(index) - count).ToArray(), poolBefore, poolAfter, effectStartedTicks, effectCompletedTicks, effectDuration, processBefore!, processAfter) { PresentationPacing = pacing?.Complete(), VisualChanges = visualChanges?.Complete(effectStartedTicks, effectCompletedTicks) });
        start = null;
        pacing = null;
        visualChanges = null;
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
                if (effectStartedTicks == 0 && value.RequestedEffect == expectedEffect && value.ExecutedEffect == expectedEffect && (expectedNativeCommand is null || value.NativeCommand == expectedNativeCommand))
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
