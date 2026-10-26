using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8.BattleEffects;

public sealed record PilotSlowCase(PilotCase Case, string SourceManifestPath, string SourceManifestSha256, string TelemetryPath, string? SourceBuildFingerprint, double?[] MedianFps, double MinimumCastMedianFps, string ResultPath, JsonElement ActionIdentity)
{
    public double[] InvocationMedianFps { get; init; } = [];
    public int[] EffectFrameCounts { get; init; } = [];
    public int[] BoundaryIntervalCounts { get; init; } = [];
}
public sealed record PilotSlowEffect(int EffectId, double MinimumCastMedianFps, PilotSlowCase[] Cases);
public sealed record PilotSlowEffectsReport(double BelowFps, int Requested, int CompletedCases, int IgnoredErrors, int IgnoredBlocked, int PendingCases, PilotSlowEffect[] Effects, PilotResumeDiagnostic[] Diagnostics)
{
    public string FpsScope { get; init; } = "invocation-legacy";
    public string? BoundaryPolicy { get; init; }
    public int UnavailableEffectCasts { get; init; }
    public int UnavailableEffectCases { get; init; }
}

/// <summary>Queries authenticated latest scenario evidence without changing history or resume state.</summary>
public static class PilotSlowEffects
{
    public static PilotSlowEffectsReport Query(PilotManifest requested, string historyRoot, double belowFps)
    {
        ValidateThreshold(belowFps);
        return Select(Measure(requested, historyRoot), belowFps);
    }

    internal static void ValidateThreshold(double belowFps)
    {
        if (!double.IsFinite(belowFps) || belowFps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(belowFps), "The FPS threshold must be finite and positive.");
        }
    }

    internal static PilotFpsMeasurement Measure(PilotManifest requested, string historyRoot)
    {
        PilotResumePlan plan = PilotResume.Plan(requested, historyRoot);
        List<PilotResumeDiagnostic> diagnostics = [.. plan.Diagnostics];
        List<PilotMeasuredCase> measured = [];
        int pending = plan.PendingCases.Length;
        foreach (PilotReusedCase reused in plan.ReusedCases.Where(item => item.Tested))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(ReadBytes(reused.TelemetryPath!, 64 * 1024 * 1024));
                JsonElement report = document.RootElement;
                LiveEvidenceVerdict validation = LiveEvidenceValidation.Validate(report);
                if (validation.Verdict is not ("PASS" or "FAIL") || validation.Executions.Length != 2 || reused.Case.EffectId is not int effectId || report.GetProperty("expected_effect").GetInt32() != effectId)
                {
                    throw new InvalidDataException("Historical scenario requires two completed casts and an explicit matching effect identity.");
                }
                if (validation.Executions.Any(execution => !double.IsFinite(execution.MedianFps) || execution.MedianFps <= 0) || validation.EffectExecutions.Length != 2)
                {
                    throw new InvalidDataException("Historical cast median FPS is not finite and positive.");
                }
                string manifestHash = Convert.ToHexStringLower(SHA256.HashData(ReadBytes(reused.ManifestPath, 4 * 1024 * 1024)));
                string telemetryHash = Convert.ToHexStringLower(SHA256.HashData(ReadBytes(reused.TelemetryPath!, 64 * 1024 * 1024)));
                string candidateHash = Convert.ToHexStringLower(SHA256.HashData(ReadBytes(reused.CandidatePath!, 4 * 1024 * 1024)));
                measured.Add(new(reused, manifestHash, telemetryHash, candidateHash, report.GetProperty("input").Clone(), validation));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or FormatException or OverflowException or NullReferenceException)
            {
                diagnostics.Add(new(reused.ResultPath, exception.Message));
                pending++;
            }
        }
        return new(plan, measured.ToArray(), pending, diagnostics.ToArray());
    }

    internal static PilotSlowEffectsReport Select(PilotFpsMeasurement measured, double belowFps)
    {
        List<PilotSlowCase> slow = [];
        foreach (PilotMeasuredCase item in measured.Cases)
        {
            double?[] medians = item.Validation.EffectExecutions.Select(execution => execution.MedianFps).ToArray();
            double[] available = medians.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
            if (available.Length == 0 || available.Min() >= belowFps)
            {
                continue;
            }
            PilotReusedCase reused = item.Source;
            slow.Add(new(reused.Case, reused.ManifestPath, item.ManifestSha256, reused.TelemetryPath!, reused.PreviousBuildFingerprint, medians, available.Min(), reused.ResultPath, item.ActionIdentity)
            {
                InvocationMedianFps = item.Validation.Executions.Select(execution => execution.MedianFps).ToArray(),
                EffectFrameCounts = item.Validation.EffectExecutions.Select(execution => execution.Frames.FrameCount).ToArray(),
                BoundaryIntervalCounts = item.Validation.EffectExecutions.Select(execution => execution.BoundaryIntervalCount).ToArray()
            });
        }
        PilotSlowEffect[] effects = slow.GroupBy(item => item.Case.EffectId!.Value).Select(group => new PilotSlowEffect(group.Key, group.Min(item => item.MinimumCastMedianFps), group.OrderBy(item => item.MinimumCastMedianFps).ThenBy(item => item.Case.Id, StringComparer.Ordinal).ToArray())).OrderBy(effect => effect.MinimumCastMedianFps).ThenBy(effect => effect.EffectId).ToArray();
        return new(belowFps, measured.Plan.Requested, measured.Cases.Length, measured.Plan.PreviouslyErrored, measured.Plan.PreviouslyBlocked, measured.PendingCases, effects, measured.Diagnostics)
        {
            FpsScope = "effect",
            BoundaryPolicy = TelemetryEffectFrames.Policy,
            UnavailableEffectCasts = measured.Cases.Sum(item => item.Validation.EffectExecutions.Count(execution => !execution.Available)),
            UnavailableEffectCases = measured.Cases.Count(item => item.Validation.EffectExecutions.All(execution => !execution.Available))
        };
    }

    private static byte[] ReadBytes(string path, int maximum)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 || stream.Length > maximum)
        {
            throw new InvalidDataException("History artifact exceeds its bounded size or is empty.");
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
