using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.BattleEffects;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotCase(string Id, string? Recipe, string Status = "RUNNABLE", string[]? Blockers = null, int? EffectId = null, string? RecipeSha256 = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotBudgets(double InitializationMs, double ActionOneMs, double CleanupRearmMs, double ActionTwoMs, double TeardownMs, double TotalMs, double NoProgressMs)
{
    public SupervisionBudgets ToSupervision()
    {
        double[] values = [InitializationMs, ActionOneMs, CleanupRearmMs, ActionTwoMs, TeardownMs, TotalMs, NoProgressMs];
        if (values.Any(value => !double.IsFinite(value) || value <= 0 || value > TimeSpan.FromDays(1).TotalMilliseconds))
        {
            throw new InvalidDataException("Every pilot deadline must be finite, positive and at most one day.");
        }
        return new(TimeSpan.FromMilliseconds(InitializationMs), TimeSpan.FromMilliseconds(ActionOneMs), TimeSpan.FromMilliseconds(CleanupRearmMs), TimeSpan.FromMilliseconds(ActionTwoMs), TimeSpan.FromMilliseconds(TeardownMs), TimeSpan.FromMilliseconds(TotalMs), TimeSpan.FromMilliseconds(NoProgressMs));
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PilotManifest(int SchemaVersion, string Executable, string WorkingDirectory, string GameDir, string OutputRoot, PilotBudgets Budgets, PilotCase[] Cases, string? BuildFingerprint = null)
{
    public bool MuteOutput { get; init; }
}
public sealed record PilotStageDurations(double StagingMs, double SupervisionMs, double ValidationMs, double ResultFinalizationMs);
public sealed record PilotCaseSummary(string Id, string Verdict, string[] Errors, int Executions, double TotalMs, double EffectDurationMs, double ExecutionWindowMs, double OverheadMs, bool OverheadWithinBudget, string Directory, bool ProcessExited, PilotStageDurations? Stages = null, int? EffectId = null, string ScenarioStatus = "RUNNABLE", string[]? DiagnosticIssues = null, string? CopyTrafficScope = null);
public sealed record PilotBatchSummary(int SchemaVersion, string Verdict, string BuildFingerprint, int Requested, int Passed, int Failed, int Errors, double TotalMs, PilotCaseSummary[] Cases);

/// <summary>External manifest runner. Each runnable case gets a fresh bounded owned host and two live executions; measurements never overlap.</summary>
public static class PilotRunner
{
    public const double MaximumOverheadMs = 10000;
    private static string MeasurementPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memoria.FF8", "Validation", "measurement.lock");

    public static async Task<PilotBatchSummary> RunAsync(PilotManifest manifest, CancellationToken cancellationToken = default, Action<PilotCaseSummary>? completed = null, string? measurementPath = null)
    {
        ValidateManifest(manifest);
        using FileStream slot = AcquireMeasurementSlot(measurementPath);
        Stopwatch batchTime = Stopwatch.StartNew();
        Directory.CreateDirectory(manifest.OutputRoot);
        using (new FileStream(Path.Combine(manifest.OutputRoot, "pilot-owner"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
        }
        BuildFile[] files = EvidenceStore.DescribeFiles(manifest.WorkingDirectory);
        string fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(files, RecordJson.Options)));
        if (manifest.BuildFingerprint is not null && manifest.BuildFingerprint != fingerprint)
        {
            throw new InvalidDataException("Pilot build directory differs from its explicit fingerprint.");
        }
        WriteNew(Path.Combine(manifest.OutputRoot, "manifest.json"), manifest);
        WriteNew(Path.Combine(manifest.OutputRoot, "build-fingerprint.json"), new { fingerprint, files });
        List<PilotCaseSummary> results = [];
        bool safeToContinue = true;
        int index = 0;
        foreach (PilotCase item in manifest.Cases)
        {
            index++;
            string directory = Path.Combine(manifest.OutputRoot, $"{index:D2}-{item.Id}");
            PilotCaseSummary result;
            if (item.Status != "RUNNABLE")
            {
                result = BlockedResult(item, directory);
            }
            else if (!safeToContinue)
            {
                result = Summarize(item.Id, "ERROR", ["PREVIOUS_PROCESS_EXIT_UNCONFIRMED"], 0, 0, 0, directory, false);
            }
            else
            {
                result = await RunCaseAsync(manifest, item, index, directory, measurementPath ?? MeasurementPath, cancellationToken).ConfigureAwait(false);
                safeToContinue = result.ProcessExited;
            }
            result = result with { EffectId = item.EffectId, ScenarioStatus = item.Status };
            Directory.CreateDirectory(directory);
            WriteNew(Path.Combine(directory, "pilot-result.json"), result);
            results.Add(result);
            completed?.Invoke(result);
        }
        batchTime.Stop();
        PilotBatchSummary summary = new(1, results.All(result => result.Verdict == "PASS") ? "PASS" : results.Any(result => result.Verdict == "ERROR") ? "ERROR" : "FAIL", fingerprint, manifest.Cases.Length, results.Count(result => result.Verdict == "PASS"), results.Count(result => result.Verdict == "FAIL"), results.Count(result => result.Verdict == "ERROR"), batchTime.Elapsed.TotalMilliseconds, results.ToArray());
        WriteNew(Path.Combine(manifest.OutputRoot, "batch-summary.json"), summary);
        return summary;
    }

    private static async Task<PilotCaseSummary> RunCaseAsync(PilotManifest manifest, PilotCase item, int index, string directory, string measurementPath, CancellationToken cancellationToken)
    {
        Stopwatch total = Stopwatch.StartNew();
        long stagingEnded = 0;
        long supervisionEnded = 0;
        long validationEnded = 0;
        string verdict = "ERROR";
        List<string> errors = [];
        string[] diagnosticIssues = [];
        string? copyTrafficScope = null;
        double effectMs = 0;
        double executionWindowMs = 0;
        int executions = 0;
        bool exited = true;
        try
        {
            string output = Path.Combine(directory, "output");
            string settings = Path.Combine(directory, "settings");
            string control = Path.Combine(directory, "control");
            string save = Path.Combine(directory, "saves", "checkpoint.json");
            Directory.CreateDirectory(directory);
            if (item.Recipe is null || !Path.IsPathFullyQualified(item.Recipe))
            {
                throw new InvalidDataException("A runnable scenario requires an explicit absolute recipe path.");
            }
            byte[] recipe = File.ReadAllBytes(item.Recipe);
            if (item.RecipeSha256 is not null && Convert.ToHexStringLower(SHA256.HashData(recipe)) != item.RecipeSha256)
            {
                throw new InvalidDataException("RECIPE_SHA256_MISMATCH: Submitted recipe differs from the frozen scenario input.");
            }
            string bootstrap = Path.Combine(directory, "bootstrap.json");
            OwnedChildSupervisor.PublishTransientImmutable(bootstrap, recipe);
            if (manifest.MuteOutput)
            {
                Directory.CreateDirectory(settings);
                OwnedChildSupervisor.PublishTransientImmutable(Path.Combine(settings, "ui-settings.json"), JsonSerializer.SerializeToUtf8Bytes(new { MusicVolume = 0, SoundVolume = 0, VoiceVolume = 0 }));
            }
            SupervisionRequest request = new($"pilot-{index:D2}-{item.Id}", manifest.Executable, ["--game-dir", manifest.GameDir, "--battle-test", bootstrap, "--resolution", "1920x1080", "--settings-dir", settings, "--control-dir", control, "--save-state", save], manifest.WorkingDirectory, settings, output, control, manifest.Budgets.ToSupervision(), new Dictionary<string, string> { ["MEMORIA_FF8_GAME_DIR"] = manifest.GameDir });
            stagingEnded = total.ElapsedTicks;
            SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(request, cancellationToken).ConfigureAwait(false);
            supervisionEnded = total.ElapsedTicks;
            exited = outcome.ProcessExited;
            if (!exited)
            {
                WriteNew(measurementPath + ".blocked.json", new { outcome.AttemptId, outcome.Process, outcome.Message, process_exited = false });
            }
            if (outcome.Status == "candidate_ready")
            {
                string path = Path.Combine(output, "telemetry-report.json");
                JsonElement report = ValidateCandidate(outcome.CandidatePayload!, path, recipe);
                if (item.EffectId is int effectId && report.GetProperty("expected_effect").GetInt32() != effectId)
                {
                    throw new InvalidDataException("Live report differs from the manifest scenario effect identity.");
                }
                LiveEvidenceVerdict validation = LiveEvidenceValidation.Validate(report);
                verdict = validation.Verdict;
                errors.AddRange(validation.Errors);
                diagnosticIssues = validation.DiagnosticIssues;
                copyTrafficScope = validation.CopyTrafficScope;
                effectMs = validation.EffectDurationsMs.Sum();
                executionWindowMs = validation.Executions.Sum(execution => execution.DurationMs);
                executions = validation.Executions.Length;
                if (executions != 2 || validation.EffectDurationsMs.Length != 2)
                {
                    errors.Add("EXACTLY_TWO_EXECUTIONS_REQUIRED");
                    verdict = "ERROR";
                }
                validationEnded = total.ElapsedTicks;
                OwnedChildSupervisor.PublishValidatedCandidate(outcome, _ => JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schema_version = 1,
                    verdict,
                    errors = errors.ToArray(),
                    diagnostic_issues = diagnosticIssues,
                    copy_traffic_scope = copyTrafficScope,
                    executions,
                    effect_duration_ms = effectMs,
                    execution_window_ms = executionWindowMs,
                    effect_fps = validation.EffectExecutions.Select((scope, index) => new { execution = index + 1, metrics = PilotFpsMetrics.From(scope.Frames), boundary_metrics = PilotFpsMetrics.From(TelemetryFrameCollector.Recompute(scope.BoundaryIntervals, 0)), scope.BoundaryIntervalCount, scope.Policy }).ToArray(),
                    invocation_fps = validation.Executions.Select(PilotFpsMetrics.From).ToArray(),
                    campaign_acceptance = false
                }, RecordJson.Options));
            }
            else
            {
                validationEnded = supervisionEnded;
                errors.Add((outcome.ErrorCategory ?? "SUPERVISION_FAILED") + (outcome.Message is null ? "" : ": " + outcome.Message));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or JsonException or ArgumentException or FormatException or OverflowException or KeyNotFoundException)
        {
            verdict = "ERROR";
            errors.Add(exception.GetType().Name + ": " + exception.Message);
        }
        // The test ends after validated result.json publication. The timing envelope,
        // batch inventory and caller progress reporting are supervisor bookkeeping.
        total.Stop();
        long totalTicks = total.ElapsedTicks;
        stagingEnded = stagingEnded == 0 ? totalTicks : stagingEnded;
        supervisionEnded = supervisionEnded == 0 ? totalTicks : supervisionEnded;
        validationEnded = validationEnded == 0 ? totalTicks : validationEnded;
        double millisecondsPerTick = 1000.0 / Stopwatch.Frequency;
        double totalMs = totalTicks * millisecondsPerTick;
        double stagingMs = stagingEnded * millisecondsPerTick;
        double supervisionMs = (supervisionEnded - stagingEnded) * millisecondsPerTick;
        double validationMs = (validationEnded - supervisionEnded) * millisecondsPerTick;
        PilotStageDurations stages = new(stagingMs, supervisionMs, validationMs, totalMs - stagingMs - supervisionMs - validationMs);
        return Summarize(item.Id, verdict, errors.ToArray(), executions, totalMs, effectMs, directory, exited, executionWindowMs, stages, diagnosticIssues, copyTrafficScope);
    }

    public static PilotCaseSummary Summarize(string id, string verdict, string[] errors, int executions, double totalMs, double effectDurationMs, string directory, bool processExited, double? executionWindowMs = null, PilotStageDurations? stages = null, string[]? diagnosticIssues = null, string? copyTrafficScope = null)
    {
        bool stagesInvalid = stages is not null && (new[] { stages.StagingMs, stages.SupervisionMs, stages.ValidationMs, stages.ResultFinalizationMs }.Any(value => !double.IsFinite(value) || value < -0.0000001) || Math.Abs(stages.StagingMs + stages.SupervisionMs + stages.ValidationMs + stages.ResultFinalizationMs - totalMs) > 0.0000001);
        if (!double.IsFinite(totalMs) || !double.IsFinite(effectDurationMs) || totalMs < 0 || effectDurationMs < 0 || effectDurationMs > totalMs || executions is < 0 or > 2 || executionWindowMs is double window && (!double.IsFinite(window) || window < effectDurationMs || window > totalMs) || stagesInvalid)
        {
            return new(id, "ERROR", [.. errors, "TIMING_EVIDENCE_INVALID"], executions, double.IsFinite(totalMs) && totalMs >= 0 ? totalMs : 0, double.IsFinite(effectDurationMs) && effectDurationMs >= 0 ? effectDurationMs : 0, 0, 0, false, directory, processExited, stages, DiagnosticIssues: diagnosticIssues, CopyTrafficScope: copyTrafficScope);
        }
        double overhead = totalMs - effectDurationMs;
        bool within = overhead <= MaximumOverheadMs;
        List<string> allErrors = [.. errors];
        if (verdict == "PASS" && (executions != 2 || errors.Length != 0))
        {
            allErrors.Add("COMPLETE_PASS_EVIDENCE_REQUIRED");
            verdict = "ERROR";
        }
        if (!within)
        {
            allErrors.Add("OVERHEAD_EXCEEDS_10000_MS");
            if (verdict == "PASS")
            {
                verdict = "FAIL";
            }
        }
        if (!processExited)
        {
            allErrors.Add("PROCESS_EXIT_UNCONFIRMED");
            verdict = "ERROR";
        }
        return new(id, verdict, allErrors.Distinct(StringComparer.Ordinal).ToArray(), executions, totalMs, effectDurationMs, executionWindowMs ?? effectDurationMs, overhead, within, directory, processExited, stages, DiagnosticIssues: diagnosticIssues, CopyTrafficScope: copyTrafficScope);
    }

    public static PilotCaseSummary BlockedResult(PilotCase item, string directory)
    {
        string[] errors = ["SCENARIO_" + item.Status, .. (item.Blockers ?? [])];
        return Summarize(item.Id, "ERROR", errors, 0, 0, 0, directory, true);
    }

    public static void ValidateManifest(PilotManifest manifest)
    {
        if (manifest.SchemaVersion != 1 || manifest.Cases is null || manifest.Cases.Length < 1 || manifest.Budgets is null)
        {
            throw new InvalidDataException("A scenario manifest requires schema v1, finite budgets and at least one explicitly selected case.");
        }
        manifest.Budgets.ToSupervision().Validate();
        if (!Path.IsPathFullyQualified(manifest.Executable) || !File.Exists(manifest.Executable) || !Path.IsPathFullyQualified(manifest.WorkingDirectory) || !Directory.Exists(manifest.WorkingDirectory) || !Path.IsPathFullyQualified(manifest.GameDir) || !Directory.Exists(manifest.GameDir) || !Path.IsPathFullyQualified(manifest.OutputRoot))
        {
            throw new InvalidDataException("Pilot executable, build, game and output paths must be explicit absolute paths; inputs must exist.");
        }
        string relative = Path.GetRelativePath(manifest.WorkingDirectory, manifest.Executable);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The pilot executable must belong to its pinned build directory.");
        }
        if (Directory.Exists(manifest.OutputRoot) && Directory.EnumerateFileSystemEntries(manifest.OutputRoot).Any())
        {
            throw new InvalidDataException("Pilot output root must be fresh and cannot replace prior evidence.");
        }
        try
        {
            PrivateEvidencePath.Validate(manifest.OutputRoot);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(exception.Message, exception);
        }
        string outputPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(manifest.OutputRoot));
        foreach (string inputRoot in new[] { manifest.WorkingDirectory, manifest.GameDir })
        {
            string inputPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(inputRoot));
            if (outputPath.Equals(inputPath, StringComparison.OrdinalIgnoreCase) || outputPath.StartsWith(inputPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || inputPath.StartsWith(outputPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Pilot evidence and supplied build/game directories must be disjoint.");
            }
        }
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        foreach (PilotCase item in manifest.Cases)
        {
            if (item is null || !ids.Add(EvidenceStore.Id(item.Id)))
            {
                throw new InvalidDataException("Scenarios require unique safe identities.");
            }
            if (item.Status is not ("RUNNABLE" or "PLANNED_BLOCKED" or "UNRESOLVED") || item.Blockers?.Any(string.IsNullOrWhiteSpace) == true || item.Status != "RUNNABLE" && item.Blockers is not { Length: > 0 } || item.EffectId is < 0)
            {
                throw new InvalidDataException("Scenarios require a known status, a nonnegative optional effect ID and explicit blockers for every nonrunnable case.");
            }
            if (item.Status == "RUNNABLE" && item.Blockers is { Length: > 0 })
            {
                throw new InvalidDataException("A runnable scenario cannot retain unresolved blockers.");
            }
            if (item.RecipeSha256 is not null && (item.RecipeSha256.Length != 64 || item.RecipeSha256.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))))
            {
                throw new InvalidDataException("A scenario recipe SHA256 must contain exactly 64 lowercase hexadecimal characters.");
            }
        }
    }

    public static FileStream AcquireMeasurementSlot(string? path = null)
    {
        path ??= MeasurementPath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        FileStream lease = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(path + ".blocked.json"))
        {
            lease.Dispose();
            throw new InvalidOperationException("A previous measurement did not confirm owned process exit; its durable quarantine must be resolved before another host starts.");
        }
        return lease;
    }

    internal static JsonElement ValidateCandidate(byte[] candidate, string reportPath, byte[] recipe)
    {
        FileInfo info = new(reportPath);
        if (info.Length is <= 0 or > 64 * 1024 * 1024 || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Pilot telemetry must be a bounded ordinary local file.");
        }
        using JsonDocument document = JsonDocument.Parse(candidate);
        JsonElement value = document.RootElement;
        byte[] bytes = File.ReadAllBytes(reportPath);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!value.TryGetProperty("telemetry_report", out JsonElement artifactName) || artifactName.ValueKind != JsonValueKind.String || artifactName.GetString() != "telemetry-report.json" || !value.TryGetProperty("telemetry_sha256", out JsonElement digest) || digest.ValueKind != JsonValueKind.String || digest.GetString() != hash)
        {
            throw new InvalidDataException("Pilot candidate does not authenticate its fixed live telemetry artifact.");
        }
        using JsonDocument report = JsonDocument.Parse(bytes);
        using JsonDocument bootstrap = JsonDocument.Parse(recipe);
        JsonElement observation = report.RootElement;
        if (!value.TryGetProperty("battle_instance_id", out JsonElement candidateBattle) || !observation.TryGetProperty("battle_instance_id", out JsonElement reportBattle) || candidateBattle.ValueKind != JsonValueKind.String || reportBattle.ValueKind != JsonValueKind.String || candidateBattle.GetString() != reportBattle.GetString() || !bootstrap.RootElement.TryGetProperty("driver", out JsonElement driver) || !driver.TryGetProperty("expected_effect", out JsonElement expected) || !observation.TryGetProperty("expected_effect", out JsonElement actual) || expected.GetInt32() != actual.GetInt32())
        {
            throw new InvalidDataException("Pilot candidate differs from its submitted effect or battle identity.");
        }
        JsonElement submitted = driver.GetProperty("input");
        JsonElement admitted = observation.GetProperty("input");
        if (!SameSemanticInput(submitted, admitted))
        {
            throw new InvalidDataException("Pilot action identity differs from its submitted recipe.");
        }
        foreach (string name in new[] { "expected_native_action", "expected_native_command", "supporting_effects" })
        {
            bool supplied = driver.TryGetProperty(name, out JsonElement before) && before.ValueKind != JsonValueKind.Null;
            bool recorded = observation.TryGetProperty(name, out JsonElement after) && after.ValueKind != JsonValueKind.Null;
            if (supplied ? !recorded || !JsonElement.DeepEquals(before, after) : recorded && !(name == "supporting_effects" && after.ValueKind == JsonValueKind.Array && after.GetArrayLength() == 0))
            {
                throw new InvalidDataException("Pilot native invocation identity differs from its submitted recipe.");
            }
        }
        bool submittedRepairs = driver.TryGetProperty("repairs", out JsonElement plannedRepairs);
        JsonElement normalizedRepairs = submittedRepairs ? JsonSerializer.SerializeToElement(plannedRepairs.Deserialize<Memoria.FF8.Runtime.BattleDiagnosticCombatRepair[]>(RecordJson.Options), RecordJson.Options) : default;
        if (observation.TryGetProperty("declared_repairs", out JsonElement declaredRepairs) && (submittedRepairs ? !JsonElement.DeepEquals(normalizedRepairs, declaredRepairs) : declaredRepairs.GetArrayLength() != 0) || submittedRepairs && !observation.TryGetProperty("declared_repairs", out _))
        {
            throw new InvalidDataException("Pilot repair declaration differs from its submitted recipe.");
        }
        return observation.Clone();
    }

    public static bool SameSemanticInput(JsonElement submitted, JsonElement admitted)
    {
        foreach (string name in new[] { "kind", "actor_id", "target_id", "ability", "action_id" })
        {
            if (!submitted.TryGetProperty(name, out JsonElement before) || !admitted.TryGetProperty(name, out JsonElement after))
            {
                return false;
            }
            bool equal = name is "kind" or "ability" && before.ValueKind == JsonValueKind.String && after.ValueKind == JsonValueKind.String ? string.Equals(before.GetString(), after.GetString(), StringComparison.OrdinalIgnoreCase) : JsonElement.DeepEquals(before, after);
            if (!equal)
            {
                return false;
            }
        }
        return true;
    }

    private static void WriteNew<T>(string path, T value)
    {
        OwnedChildSupervisor.PublishTransientImmutable(path, JsonSerializer.SerializeToUtf8Bytes(value, RecordJson.Options));
    }
}
