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
                if (!File.Exists(manifestPath))
                {
                    continue;
                }
                try
                {
                    PilotManifest manifest = ReadManifest(manifestPath);
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
                    if (result.ExecutionProtocol is not ("same-battle-repeat" or "fresh-entry-pair"))
                    {
                        throw new InvalidDataException("Historical execution protocol is unsupported.");
                    }
                    if (result.Id != item.Id || result.EffectId != item.EffectId || result.ScenarioStatus != item.Status || result.Errors is null || result.Errors.Any(string.IsNullOrWhiteSpace))
                    {
                        throw new InvalidDataException("Historical result identity or errors are invalid.");
                    }
                    bool tested = item.Status == "RUNNABLE" && result.Verdict is "PASS" or "FAIL";
                    string? telemetryPath = null;
                    string? candidatePath = null;
                    string? pairFingerprint = null;
                    if (item.Status == "RUNNABLE" && result.Verdict == "ERROR")
                    {
                        if (result.ExecutionProtocol == "fresh-entry-pair")
                        {
                            throw new InvalidDataException("Incomplete fresh-entry pair requires a new authenticated attempt.");
                        }
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
                        LiveEvidenceVerdict validation;
                        if (result.ExecutionProtocol == "fresh-entry-pair")
                        {
                            PilotFreshEntryEvidence pair = PilotFreshEntryHistory.Read(result, caseDirectory, recipe!, item.EffectId);
                            validation = pair.Validation;
                            pairFingerprint = pair.Pins.CandidateFingerprint;
                        }
                        else
                        {
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
                            validation = LiveEvidenceValidation.Validate(report);
                        }
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
                    if (pairFingerprint is not null && pairFingerprint != fingerprint)
                    {
                        throw new InvalidDataException("Fresh-entry pair source build differs from its historical build fingerprint.");
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

    private static PilotManifest ReadManifest(string path)
    {
        OrdinaryPath(path);
        // Full selections grow with the catalog; the per-case record limit does not apply to their manifest.
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using JsonDocument document = JsonDocument.Parse(stream);
        RejectDuplicateProperties(document.RootElement);
        return document.RootElement.Deserialize<PilotManifest>(RecordJson.Options) ?? throw new InvalidDataException("History manifest is null.");
    }

    internal static void RejectDuplicateProperties(JsonElement value)
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

    internal static void OrdinaryPath(string path)
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

internal sealed record PilotFreshEntryEvidence(FreshEntryPairPins Pins, LiveEvidenceVerdict Validation, JsonElement ActionIdentity);

internal static class PilotFreshEntryHistory
{
    internal static PilotFreshEntryEvidence Read(PilotCaseSummary result, string caseDirectory, byte[] recipe, int? expectedEffect)
    {
        string path = Path.Combine(caseDirectory, "fresh-entry-pair-pins.json");
        PilotResume.OrdinaryPath(path);
        if (result.PairPinsPath is null || !Path.IsPathFullyQualified(result.PairPinsPath) || Path.GetFullPath(result.PairPinsPath) != Path.GetFullPath(path))
        {
            throw new InvalidDataException("Fresh-entry history requires its explicit case-local pair pins path.");
        }
        FileInfo file = new(path);
        if (file.Length is <= 0 or > 4 * 1024 * 1024)
        {
            throw new InvalidDataException("Fresh-entry pair pins exceed their bounded size.");
        }
        using JsonDocument pinDocument = JsonDocument.Parse(File.ReadAllBytes(path));
        PilotResume.RejectDuplicateProperties(pinDocument.RootElement);
        FreshEntryPairPins pins = pinDocument.RootElement.Deserialize<FreshEntryPairPins>(RecordJson.Options) ?? throw new InvalidDataException("Fresh-entry pair pins are absent.");
        if (pins.Entries.IsDefault || pins.Entries.Length != 2 || !string.Equals(pins.Recipe.Sha256, Convert.ToHexStringLower(SHA256.HashData(recipe)), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Fresh-entry pair differs from the requested recipe or entry count.");
        }
        for (int index = 0; index < 2; index++)
        {
            PilotResume.OrdinaryPath(pins.Entries[index].Report.Path);
            PilotResume.OrdinaryPath(pins.Entries[index].Initialization.Path);
            string output = Path.Combine(caseDirectory, $"entry-{index + 1}", "output");
            if (Path.GetFullPath(pins.Entries[index].Report.Path) != Path.GetFullPath(Path.Combine(output, "telemetry-report.json")) || Path.GetFullPath(pins.Entries[index].Initialization.Path) != Path.GetFullPath(Path.Combine(output, "fresh-entry-initialization.json")))
            {
                throw new InvalidDataException("Fresh-entry pair must retain both independent case-local report and initialization paths.");
            }
        }
        FreshEntryPairVerdict pair = FreshEntryPairValidation.ValidateFiles(pins);
        if (!pair.FunctionalPass || pair.Entries.Length != 2)
        {
            throw new InvalidDataException("Fresh-entry pair authentication failed: " + string.Join("; ", pair.Errors));
        }
        JsonElement identity = default;
        foreach (FreshEntryRunPins entry in pins.Entries)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(entry.Report.Path));
            JsonElement report = document.RootElement;
            PilotResume.RejectDuplicateProperties(report);
            if (expectedEffect is not int effect || report.GetProperty("expected_effect").GetInt32() != effect)
            {
                throw new InvalidDataException("Fresh-entry report differs from the requested effect.");
            }
            JsonElement current = report.GetProperty("input");
            if (identity.ValueKind == JsonValueKind.Undefined)
            {
                identity = current.Clone();
            }
            else if (!JsonElement.DeepEquals(identity, current))
            {
                throw new InvalidDataException("Fresh-entry pair differs in action identity.");
            }
        }
        string[] errors = pair.Entries.SelectMany(entry => entry.Errors).Distinct(StringComparer.Ordinal).ToArray();
        LiveEvidenceVerdict validation = pair.Entries[0] with
        {
            Verdict = errors.Length == 0 ? "PASS" : "FAIL",
            Errors = errors,
            Executions = pair.Entries.SelectMany(entry => entry.Executions).ToArray(),
            EffectDurationsMs = pair.Entries.SelectMany(entry => entry.EffectDurationsMs).ToArray(),
            EffectExecutions = pair.Entries.SelectMany(entry => entry.EffectExecutions).ToArray(),
            Combined = null,
            CampaignAcceptance = false
        };
        return new(pins, validation, identity);
    }
}
