using System.Text.Json;

namespace Memoria.FF8.BattleEffects;

internal sealed record PilotMeasuredCase(PilotReusedCase Source, string ManifestSha256, string TelemetrySha256, string CandidateSha256, JsonElement ActionIdentity, LiveEvidenceVerdict Validation);
internal sealed record PilotFpsMeasurement(PilotResumePlan Plan, PilotMeasuredCase[] Cases, int PendingCases, PilotResumeDiagnostic[] Diagnostics);

public sealed record PilotFpsMetrics(int FrameCount, bool Available, double DurationMs, double? MinimumFps, double? MaximumFps, double? MedianFps, double? MinimumFrameMs, double? MedianFrameMs, double? P95FrameMs, double? P99FrameMs, double? MaximumFrameMs, int SlowIntervalCount)
{
    internal static PilotFpsMetrics From(TelemetryFrameSummary frames)
    {
        bool available = frames.FrameCount > 0 && frames.Valid;
        return new(frames.FrameCount, available, frames.DurationMs, available ? frames.MinimumFps : null, available ? frames.MaximumFps : null, available ? frames.MedianFps : null, available ? frames.MinimumFrameMs : null, available ? frames.MedianFrameMs : null, available ? frames.P95FrameMs : null, available ? frames.P99FrameMs : null, available ? frames.MaximumFrameMs : null, frames.SlowIntervalCount);
    }
}

public sealed record PilotFpsCast(int Index, PilotFpsMetrics Effect, PilotFpsMetrics Invocation, PilotFpsMetrics Boundary, int BoundaryIntervalCount, long EffectStartedTicks, long EffectCompletedTicks, string BoundaryPolicy);
public sealed record PilotFpsCase(PilotCase Case, string OriginalVerdict, string[] OriginalErrors, string EffectFpsVerdict, string SourceManifestPath, string SourceManifestSha256, string? SourceBuildFingerprint, string ResultPath, string TelemetryPath, string TelemetrySha256, string CandidatePath, string CandidateSha256, JsonElement ActionIdentity, PilotFpsMetrics Effect, PilotFpsMetrics Invocation, PilotFpsMetrics Boundary, PilotFpsCast[] Casts);
public sealed record PilotFpsRecalculationReport(int SchemaVersion, string FpsScope, double BelowFps, int Requested, int CompletedCases, int IgnoredErrors, int IgnoredBlocked, int PendingCases, int UnavailableEffectCasts, int UnavailableEffectCases, PilotFpsCase[] Cases, PilotResumeDiagnostic[] Diagnostics)
{
    public string BoundaryPolicy { get; init; } = TelemetryEffectFrames.Policy;
    public string EffectFpsVerdictPolicy { get; init; } = "FAIL if any whole effect interval is below 30 FPS; PASS if both casts have whole effect intervals and none is below 30 FPS; otherwise UNAVAILABLE. Independent of historical invocation acceptance and slow-query median threshold.";
}
public sealed record PilotFpsRecalculationResult(PilotFpsRecalculationReport Report, PilotSlowEffectsReport SlowEffects);

/// <summary>Derives compact FPS records from authenticated latest evidence without writing or launching a host.</summary>
public static class PilotFpsRecalculation
{
    public static PilotFpsRecalculationResult Recalculate(PilotManifest requested, string historyRoot, double belowFps)
    {
        PilotSlowEffects.ValidateThreshold(belowFps);
        PilotFpsMeasurement measured = PilotSlowEffects.Measure(requested, historyRoot);
        PilotSlowEffectsReport slow = PilotSlowEffects.Select(measured, belowFps);
        PilotFpsCase[] cases = measured.Cases.Select(CreateCase).ToArray();
        PilotFpsRecalculationReport report = new(1, "effect", belowFps, measured.Plan.Requested, cases.Length, measured.Plan.PreviouslyErrored, measured.Plan.PreviouslyBlocked, measured.PendingCases, slow.UnavailableEffectCasts, slow.UnavailableEffectCases, cases, measured.Diagnostics);
        return new(report, slow);
    }

    private static PilotFpsCase CreateCase(PilotMeasuredCase item)
    {
        LiveEvidenceVerdict validation = item.Validation;
        PilotFpsCast[] casts = validation.EffectExecutions.Select((effect, index) => new PilotFpsCast(index + 1, PilotFpsMetrics.From(effect.Frames), PilotFpsMetrics.From(validation.Executions[index]), PilotFpsMetrics.From(TelemetryFrameCollector.Recompute(effect.BoundaryIntervals.ToArray(), 0)), effect.BoundaryIntervalCount, effect.StartedTicks, effect.CompletedTicks, effect.Policy)).ToArray();
        TelemetryFrameSummary effectFrames = TelemetryFrameCollector.Combine(validation.EffectExecutions.Select(effect => effect.Frames).ToArray());
        TelemetryFrameSummary invocationFrames = TelemetryFrameCollector.Combine(validation.Executions);
        TelemetryFrameSummary boundaryFrames = TelemetryFrameCollector.Recompute(validation.EffectExecutions.SelectMany(effect => effect.BoundaryIntervals).ToArray(), 0);
        string verdict = validation.EffectExecutions.Any(effect => effect.Frames.SlowIntervalCount > 0) ? "FAIL" : validation.EffectExecutions.All(effect => effect.Available) ? "PASS" : "UNAVAILABLE";
        PilotReusedCase source = item.Source;
        return new(source.Case, source.PreviousResult.Verdict, source.PreviousResult.Errors, verdict, source.ManifestPath, item.ManifestSha256, source.PreviousBuildFingerprint, source.ResultPath, source.TelemetryPath!, item.TelemetrySha256, source.CandidatePath!, item.CandidateSha256, item.ActionIdentity, PilotFpsMetrics.From(effectFrames), PilotFpsMetrics.From(invocationFrames), PilotFpsMetrics.From(boundaryFrames), casts);
    }
}
