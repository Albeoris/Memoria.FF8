using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8.BattleEffects;

public sealed record PilotSlowCase(PilotCase Case, string SourceManifestPath, string SourceManifestSha256, string TelemetryPath, string? SourceBuildFingerprint, double[] MedianFps, double MinimumCastMedianFps, string ResultPath, JsonElement ActionIdentity);
public sealed record PilotSlowEffect(int EffectId, double MinimumCastMedianFps, PilotSlowCase[] Cases);
public sealed record PilotSlowEffectsReport(double BelowFps, int Requested, int CompletedCases, int IgnoredErrors, int IgnoredBlocked, int PendingCases, PilotSlowEffect[] Effects, PilotResumeDiagnostic[] Diagnostics);

/// <summary>Queries authenticated latest scenario evidence without changing history or resume state.</summary>
public static class PilotSlowEffects
{
    public static PilotSlowEffectsReport Query(PilotManifest requested, string historyRoot, double belowFps)
    {
        if (!double.IsFinite(belowFps) || belowFps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(belowFps), "The FPS threshold must be finite and positive.");
        }
        PilotResumePlan plan = PilotResume.Plan(requested, historyRoot);
        List<PilotResumeDiagnostic> diagnostics = [.. plan.Diagnostics];
        List<PilotSlowCase> slow = [];
        int completed = 0;
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
                double[] medians = validation.Executions.Select(execution => execution.MedianFps).ToArray();
                if (medians.Any(value => !double.IsFinite(value) || value <= 0))
                {
                    throw new InvalidDataException("Historical cast median FPS is not finite and positive.");
                }
                double minimum = medians.Min();
                if (minimum < belowFps)
                {
                    string manifestHash = Convert.ToHexStringLower(SHA256.HashData(ReadBytes(reused.ManifestPath, 4 * 1024 * 1024)));
                    slow.Add(new(reused.Case, reused.ManifestPath, manifestHash, reused.TelemetryPath!, reused.PreviousBuildFingerprint, medians, minimum, reused.ResultPath, report.GetProperty("input").Clone()));
                }
                completed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or FormatException or OverflowException or NullReferenceException)
            {
                diagnostics.Add(new(reused.ResultPath, exception.Message));
                pending++;
            }
        }
        PilotSlowEffect[] effects = slow.GroupBy(item => item.Case.EffectId!.Value).Select(group => new PilotSlowEffect(group.Key, group.Min(item => item.MinimumCastMedianFps), group.OrderBy(item => item.MinimumCastMedianFps).ThenBy(item => item.Case.Id, StringComparer.Ordinal).ToArray())).OrderBy(effect => effect.MinimumCastMedianFps).ThenBy(effect => effect.EffectId).ToArray();
        return new(belowFps, plan.Requested, completed, plan.PreviouslyErrored, plan.PreviouslyBlocked, pending, effects, diagnostics.ToArray());
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
