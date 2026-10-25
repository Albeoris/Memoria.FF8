using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8.BattleEffects;

public sealed record PilotReusedCase(PilotCase Case, PilotCaseSummary PreviousResult, string ManifestPath, string ResultPath, string? TelemetryPath, string? CandidatePath, string? PreviousBuildFingerprint, bool Tested);
public sealed record PilotResumeDiagnostic(string Path, string Message);
public sealed record PilotResumePlan(PilotCase[] PendingCases, PilotReusedCase[] ReusedCases, PilotResumeDiagnostic[] Diagnostics)
{
    public int Requested => PendingCases.Length + ReusedCases.Length;
    public int Reused => ReusedCases.Length;
    public int PreviouslyTested => ReusedCases.Count(item => item.Tested);
    public int PreviouslyPassed => ReusedCases.Count(item => item.Tested && item.PreviousResult.Verdict == "PASS");
    public int PreviouslyFailed => ReusedCases.Count(item => item.Tested && item.PreviousResult.Verdict == "FAIL");
    public int PreviouslyErrored => ReusedCases.Count(item => item.Case.Status == "RUNNABLE" && item.PreviousResult.Verdict == "ERROR");
    public int PreviouslyBlocked => ReusedCases.Count(item => !item.Tested && item.Case.Status != "RUNNABLE");
}

/// <summary>Retains authenticated historical checks as lineage, without certifying the requested build.</summary>
public static class PilotResume
{
    private const int MaximumRecordBytes = 4 * 1024 * 1024;

    public static PilotResumePlan Plan(PilotManifest requested, string historyRoot)
    {
        List<PilotResumeDiagnostic> diagnostics = [];
        List<PilotCase> pending = [];
        List<PilotReusedCase> reused = [];
        Dictionary<string, (byte[] Bytes, string Hash)> recipes = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        List<(string Path, PilotManifest Manifest)> histories = [];
        try
        {
            OrdinaryPath(historyRoot);
            // Manifest publication time orders interrupted attempts as well as completed batches.
            string[] attempts = Directory.GetDirectories(historyRoot).OrderByDescending(path => File.GetLastWriteTimeUtc(Path.Combine(path, "manifest.json"))).ThenByDescending(path => path, StringComparer.Ordinal).ToArray();
            foreach (string attempt in attempts)
            {
                string manifestPath = Path.Combine(attempt, "manifest.json");
                try
                {
                    PilotManifest manifest = Read<PilotManifest>(manifestPath);
                    if (manifest.SchemaVersion != 1 || manifest.Cases is null || manifest.Cases.Any(item => item is null) || manifest.Cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != manifest.Cases.Length)
                    {
                        throw new InvalidDataException("History manifest has an invalid scenario inventory.");
                    }
                    histories.Add((manifestPath, manifest));
                }
                catch (Exception exception) when (IsInvalidEvidence(exception))
                {
                    diagnostics.Add(new(manifestPath, exception.Message));
                }
            }
        }
        catch (Exception exception) when (IsInvalidEvidence(exception))
        {
            diagnostics.Add(new(historyRoot, exception.Message));
        }

        foreach (PilotCase item in requested.Cases)
        {
            string diagnosticPath = item.Recipe ?? historyRoot;
            try
            {
                EvidenceStore.Id(item.Id);
                byte[]? recipe = null;
                if (item.Status == "RUNNABLE")
                {
                    if (item.Recipe is null || !Path.IsPathFullyQualified(item.Recipe) || !ValidHash(item.RecipeSha256))
                    {
                        throw new InvalidDataException("Resume requires an absolute recipe path and an explicit recipe SHA256.");
                    }
                    string recipePath = Path.GetFullPath(item.Recipe);
                    if (!recipes.TryGetValue(recipePath, out (byte[] Bytes, string Hash) authenticated))
                    {
                        byte[] bytes = ReadBytes(recipePath, MaximumRecordBytes);
                        authenticated = (bytes, Hash(bytes));
                        recipes.Add(recipePath, authenticated);
                    }
                    recipe = authenticated.Bytes;
                    if (authenticated.Hash != item.RecipeSha256)
                    {
                        throw new InvalidDataException("RECIPE_SHA256_MISMATCH");
                    }
                }
                bool found = false;
                foreach ((string manifestPath, PilotManifest manifest) in histories)
                {
                    int index = Array.FindIndex(manifest.Cases, previous => SameCase(item, previous));
                    if (index < 0)
                    {
                        continue;
                    }
                    string caseDirectory = Path.Combine(Path.GetDirectoryName(manifestPath)!, $"{index + 1:D2}-{item.Id}");
                    if (!Directory.Exists(caseDirectory) && !File.Exists(caseDirectory))
                    {
                        // A campaign can be interrupted before reaching a declared scenario.
                        continue;
                    }
                    // Once a scenario directory exists, the newest matching attempt is authoritative.
                    string resultPath = Path.Combine(caseDirectory, "pilot-result.json");
                    diagnosticPath = resultPath;
                    PilotCaseSummary result = Read<PilotCaseSummary>(resultPath);
                    if (result.Id != item.Id || result.EffectId != item.EffectId || result.ScenarioStatus != item.Status || result.Errors is null || result.Errors.Any(string.IsNullOrWhiteSpace))
                    {
                        throw new InvalidDataException("Historical result identity or errors are invalid.");
                    }
                    bool tested = item.Status == "RUNNABLE" && result.Verdict is "PASS" or "FAIL";
                    string? telemetryPath = null;
                    string? candidatePath = null;
                    if (item.Status == "RUNNABLE" && result.Verdict == "ERROR")
                    {
                        if (result.Errors.Length == 0 || result.Executions is < 0 or > 2)
                        {
                            throw new InvalidDataException("Historical ERROR result has no finalized error evidence.");
                        }
                        string optionalCandidate = Path.Combine(caseDirectory, "output", "candidate.json");
                        string optionalTelemetry = Path.Combine(caseDirectory, "output", "telemetry-report.json");
                        if (result.Errors.Any(ContainsFocusFailure) || HasFocusFailure(optionalCandidate, "reason", MaximumRecordBytes, diagnostics) || HasFocusFailure(optionalTelemetry, "blocker", 64 * 1024 * 1024, diagnostics))
                        {
                            throw new InvalidDataException("GRAPHICAL_HOST_INACTIVE: Historical focus failure requires retry.");
                        }
                        candidatePath = File.Exists(optionalCandidate) ? optionalCandidate : null;
                        telemetryPath = File.Exists(optionalTelemetry) ? optionalTelemetry : null;
                    }
                    else if (tested)
                    {
                        if (!result.ProcessExited || result.Executions != 2 || result.Errors.Any(error => error is not ("PRESENTATION_BELOW_30_FPS" or "OVERHEAD_EXCEEDS_10000_MS")))
                        {
                            throw new InvalidDataException("Historical scenario was not completely checked; it must be retried.");
                        }
                        telemetryPath = Path.Combine(caseDirectory, "output", "telemetry-report.json");
                        candidatePath = Path.Combine(caseDirectory, "output", "candidate.json");
                        byte[] candidateBytes = ReadBytes(candidatePath, MaximumRecordBytes);
                        OrdinaryPath(telemetryPath);
                        using JsonDocument candidate = JsonDocument.Parse(candidateBytes);
                        RejectDuplicateProperties(candidate.RootElement);
                        if (candidate.RootElement.GetProperty("status").GetString() != "CANDIDATE_COMPLETED" || candidate.RootElement.TryGetProperty("reason", out JsonElement reason) && reason.ValueKind != JsonValueKind.Null)
                        {
                            throw new InvalidDataException("Historical candidate was blocked or incomplete.");
                        }
                        JsonElement report = PilotRunner.ValidateCandidate(candidateBytes, telemetryPath, recipe!);
                        if (item.EffectId is int effect && report.GetProperty("expected_effect").GetInt32() != effect)
                        {
                            throw new InvalidDataException("Historical report differs from the requested effect.");
                        }
                        LiveEvidenceVerdict validation = LiveEvidenceValidation.Validate(report);
                        PilotCaseSummary recomputed = PilotRunner.Summarize(item.Id, validation.Verdict, validation.Errors, validation.Executions.Length, result.TotalMs, validation.EffectDurationsMs.Sum(), caseDirectory, true, validation.Executions.Sum(execution => execution.DurationMs), result.Stages);
                        if (validation.Verdict == "ERROR" || validation.EffectDurationsMs.Length != 2 || recomputed.Verdict != result.Verdict || !recomputed.Errors.Order(StringComparer.Ordinal).SequenceEqual(result.Errors.Order(StringComparer.Ordinal)) || recomputed.EffectDurationMs != result.EffectDurationMs || recomputed.ExecutionWindowMs != result.ExecutionWindowMs || recomputed.OverheadMs != result.OverheadMs || recomputed.OverheadWithinBudget != result.OverheadWithinBudget)
                        {
                            throw new InvalidDataException("Historical summary does not reconcile with complete raw telemetry.");
                        }
                    }
                    else if (item.Status is not ("PLANNED_BLOCKED" or "UNRESOLVED") || result.Verdict != "ERROR" || result.Executions != 0 || !result.Errors.SequenceEqual(new[] { "SCENARIO_" + item.Status }.Concat(item.Blockers ?? [])))
                    {
                        throw new InvalidDataException("Historical blocked scenario does not preserve the exact declared blockers.");
                    }
                    string fingerprintPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, "build-fingerprint.json");
                    string? fingerprint = manifest.BuildFingerprint;
                    if (File.Exists(fingerprintPath))
                    {
                        JsonElement build = Read<JsonElement>(fingerprintPath);
                        fingerprint = build.GetProperty("fingerprint").GetString();
                    }
                    reused.Add(new(item, result, manifestPath, resultPath, telemetryPath, candidatePath, fingerprint, tested));
                    found = true;
                    break;
                }
                if (!found)
                {
                    pending.Add(item);
                }
            }
            catch (Exception exception) when (IsInvalidEvidence(exception))
            {
                diagnostics.Add(new(diagnosticPath, exception.Message));
                pending.Add(item);
            }
        }
        return new(pending.ToArray(), reused.ToArray(), diagnostics.ToArray());
    }

    private static bool SameCase(PilotCase requested, PilotCase previous)
    {
        return requested.Id == previous.Id && requested.EffectId == previous.EffectId && requested.Status == previous.Status && (requested.Status == "RUNNABLE" ? requested.RecipeSha256 is not null && requested.RecipeSha256 == previous.RecipeSha256 : (requested.Blockers ?? []).SequenceEqual(previous.Blockers ?? []) && requested.RecipeSha256 == previous.RecipeSha256);
    }

    private static bool ContainsFocusFailure(string value) => value.Contains("GRAPHICAL_HOST_INACTIVE", StringComparison.Ordinal);

    private static bool HasFocusFailure(string path, string property, int maximumBytes, List<PilotResumeDiagnostic> diagnostics)
    {
        if (!File.Exists(path))
        {
            return false;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(ReadBytes(path, maximumBytes));
            RejectDuplicateProperties(document.RootElement);
            return document.RootElement.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String && ContainsFocusFailure(value.GetString()!);
        }
        catch (Exception exception) when (IsInvalidEvidence(exception))
        {
            // An ERROR can legitimately retain incomplete optional output. Its finalized summary remains authoritative.
            diagnostics.Add(new(path, exception.Message));
            return false;
        }
    }

    private static bool ValidHash(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private static bool IsInvalidEvidence(Exception exception) => exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or FormatException or OverflowException or NullReferenceException;

    private static T Read<T>(string path)
    {
        using JsonDocument document = JsonDocument.Parse(ReadBytes(path, MaximumRecordBytes));
        RejectDuplicateProperties(document.RootElement);
        return document.RootElement.Deserialize<T>(RecordJson.Options) ?? throw new InvalidDataException("History record is null.");
    }

    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException("History artifact has duplicate JSON properties.");
                }
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement element in value.EnumerateArray())
            {
                RejectDuplicateProperties(element);
            }
        }
    }

    private static byte[] ReadBytes(string path, int maximum)
    {
        OrdinaryPath(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 || stream.Length > maximum)
        {
            throw new InvalidDataException("History artifact exceeds its bounded size or is empty.");
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static void OrdinaryPath(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Resume evidence cannot traverse symbolic links or reparse points.");
            }
            current = Path.GetDirectoryName(current);
        }
    }
}
