namespace Memoria.FF8.BattleEffects;

/// <summary>Generation identities belong to logical owners and survive immutable record replacement. Lifecycle counters are cumulative and never reset by a cast.</summary>
public sealed record TelemetryOwnerState(string BattleInstanceId, long LifecycleGeneration, string RetainedMemoryOwnerId, string RendererOwnerId, long ConstructorCount, long RestoreCount, long ResetCount, long NativeCallback, long FrameIndex);

public sealed class TelemetryContinuityGuard
{
    private readonly TelemetryOwnerState initial;
    private TelemetryOwnerState previous;

    public TelemetryContinuityGuard(TelemetryOwnerState initial)
    {
        this.initial = initial;
        previous = initial;
    }

    public void Observe(TelemetryOwnerState current)
    {
        if (current.BattleInstanceId != initial.BattleInstanceId || current.LifecycleGeneration != initial.LifecycleGeneration || current.RetainedMemoryOwnerId != initial.RetainedMemoryOwnerId || current.RendererOwnerId != initial.RendererOwnerId || current.ConstructorCount != initial.ConstructorCount || current.RestoreCount != initial.RestoreCount || current.ResetCount != initial.ResetCount)
        {
            throw new InvalidDataException("CONTINUITY_BROKEN: battle or retained owners were reconstructed, restored or reset.");
        }
        if (current.NativeCallback < previous.NativeCallback || current.FrameIndex < previous.FrameIndex)
        {
            throw new InvalidDataException("CONTINUITY_BROKEN: callback/frame history rewound.");
        }
        previous = current;
    }
}

public sealed record TelemetryExecutionEvidence(string RequestedAction, string AdmittedAction, string ExpectedBinding, string ActualBinding, bool Started, bool Completed, bool CleanupCompleted, int TailPresentedFrames, bool RendererParticipated, bool ExpectedMilestonesObserved, bool PendingWorkDrained, bool IntegrityChecksPassed, long NativeCallbackProgress, long UniqueRenderProgress, long DroppedEvents, TelemetryFrameSummary Frames, TelemetryTrafficSnapshot Traffic, TelemetryOwnerState StartOwners, TelemetryOwnerState EndOwners);
public sealed record TelemetryEvidenceVerdict(string Verdict, IReadOnlyList<string> Errors);

/// <summary>Conservative independent telemetry admission. Semantic fixture/outcome and authenticated-host validation remain separately required.</summary>
public static class TelemetryEvidenceValidator
{
    public static TelemetryEvidenceVerdict Validate(IReadOnlyList<TelemetryExecutionEvidence> executions, bool initializationVerified, bool semanticOutcomesVerified, bool referenceEnvironmentVerified, TelemetryCoverageManifest? coverage = null)
    {
        List<string> errors = [];
        if (!initializationVerified)
        {
            errors.Add("INITIALIZATION_UNVERIFIED");
        }
        if (!semanticOutcomesVerified)
        {
            errors.Add("SEMANTIC_OUTCOMES_UNVERIFIED");
        }
        if (!referenceEnvironmentVerified)
        {
            errors.Add("ENVIRONMENT_UNVERIFIED");
        }
        if (executions.Count < 2)
        {
            errors.Add("MISSING_SECOND_EXECUTION");
        }
        if (coverage is null || !coverage.MandatoryCoverageAvailable)
        {
            errors.Add("TELEMETRY_UNAVAILABLE");
        }
        TelemetryContinuityGuard? continuity = executions.Count > 0 ? new(executions[0].StartOwners) : null;
        foreach (TelemetryExecutionEvidence execution in executions)
        {
            try
            {
                continuity!.Observe(execution.StartOwners);
                continuity.Observe(execution.EndOwners);
            }
            catch (InvalidDataException)
            {
                errors.Add("CONTINUITY_BROKEN");
            }
            if (!execution.Started || execution.RequestedAction != execution.AdmittedAction || execution.ExpectedBinding != execution.ActualBinding)
            {
                errors.Add("DISPATCH_UNVERIFIED");
            }
            if (!execution.Completed || !execution.CleanupCompleted || !execution.PendingWorkDrained || execution.TailPresentedFrames < 2)
            {
                errors.Add("COMPLETION_UNVERIFIED");
            }
            if (!execution.RendererParticipated || !execution.ExpectedMilestonesObserved || execution.NativeCallbackProgress <= 0 || execution.UniqueRenderProgress <= 0 || execution.EndOwners.NativeCallback <= execution.StartOwners.NativeCallback || execution.EndOwners.FrameIndex <= execution.StartOwners.FrameIndex)
            {
                errors.Add("PRESENTATION_UNVERIFIED");
            }
            TelemetryFrameSummary recomputed;
            try
            {
                recomputed = TelemetryFrameCollector.Recompute(execution.Frames.Intervals.ToArray(), execution.Frames.DroppedSamples);
            }
            catch (InvalidDataException)
            {
                errors.Add("TELEMETRY_INVALID");
                continue;
            }
            if (!recomputed.Valid || execution.DroppedEvents != 0)
            {
                errors.Add("TELEMETRY_LOSS");
            }
            if (recomputed.SlowIntervalCount != 0)
            {
                errors.Add("PRESENTATION_BELOW_30_FPS");
            }
            if (!execution.Traffic.Available)
            {
                errors.Add("TELEMETRY_UNAVAILABLE");
            }
            if (!execution.IntegrityChecksPassed)
            {
                errors.Add("INTEGRITY_CHECK_FAILED");
            }
        }
        return new(errors.Count == 0 ? "PASS" : "ERROR", errors.Distinct().ToArray());
    }
}
