using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class PilotRunner
{
    private static bool HasFreshEntryDeclaration(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return false;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            return document.RootElement.TryGetProperty("driver", out JsonElement driver) && driver.TryGetProperty("fresh_entry", out JsonElement declaration) && declaration.ValueKind != JsonValueKind.Null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task<PilotCaseSummary> RunFreshEntryPairAsync(PilotManifest manifest, PilotCase item, int index, string directory, string measurementPath, CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        List<string> errors = [];
        List<LiveEvidenceVerdict> validations = [];
        List<FreshEntryRunPins> entries = [];
        bool exited = true;
        string verdict = "ERROR";
        string pinsPath = Path.Combine(directory, "fresh-entry-pair-pins.json");
        long stagingTicks = 0;
        long supervisionTicks = 0;
        long validationTicks = 0;
        try
        {
            long phaseStart = elapsed.ElapsedTicks;
            Directory.CreateDirectory(directory);
            byte[] recipe = File.ReadAllBytes(item.Recipe ?? throw new InvalidDataException("Fresh pair requires an explicit recipe."));
            string recipeHash = Convert.ToHexStringLower(SHA256.HashData(recipe));
            if (item.RecipeSha256 is not null && item.RecipeSha256 != recipeHash)
            {
                throw new InvalidDataException("RECIPE_SHA256_MISMATCH: Submitted recipe differs from the frozen scenario input.");
            }
            using JsonDocument bootstrap = JsonDocument.Parse(recipe);
            DiagnosticFreshEntryDeclaration declaration = bootstrap.RootElement.GetProperty("driver").GetProperty("fresh_entry").Deserialize<DiagnosticFreshEntryDeclaration>(RecordJson.Options) ?? throw new InvalidDataException("Fresh-entry declaration is absent.");
            declaration.Validate();
            string originalPath = Path.Combine(manifest.GameDir, "FF8_EN.exe");
            string selectedStack = ResolveFreshEntryExecutionStack(manifest.ExecutionStackProfile);
            byte[] stackBytes = File.ReadAllBytes(selectedStack);
            NativeBattleExecutionStack stack = JsonSerializer.Deserialize<NativeBattleExecutionStack>(stackBytes) ?? throw new InvalidDataException("Selected execution-stack profile is empty.");
            stack.Validate();
            stack.HeroCrtStack?.Validate(stack);
            string stackPath = Path.Combine(directory, "execution-stack.json");
            OwnedChildSupervisor.PublishTransientImmutable(stackPath, stackBytes);
            string recipePath = Path.Combine(directory, "bootstrap.json");
            OwnedChildSupervisor.PublishTransientImmutable(recipePath, recipe);
            BuildFile[] files = EvidenceStore.DescribeFiles(manifest.WorkingDirectory);
            string fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(files, RecordJson.Options)));
            if (manifest.BuildFingerprint is not null && manifest.BuildFingerprint != fingerprint)
            {
                throw new InvalidDataException("Fresh pair build differs from the declared fingerprint.");
            }
            FreshEntryFilePin originalPin = PinFreshEntryFile(originalPath);
            stagingTicks += elapsed.ElapsedTicks - phaseStart;
            for (int entry = 1; entry <= 2; entry++)
            {
                phaseStart = elapsed.ElapsedTicks;
                string root = Path.Combine(directory, $"entry-{entry}");
                string output = Path.Combine(root, "output");
                string settings = Path.Combine(root, "settings");
                string control = Path.Combine(root, "control");
                Directory.CreateDirectory(root);
                byte[]? presentationSettings = PilotPresentationSettings.CreateUiSettings(manifest.MuteOutput, manifest.PresentationVSync);
                if (presentationSettings is not null)
                {
                    Directory.CreateDirectory(settings);
                    OwnedChildSupervisor.PublishTransientImmutable(Path.Combine(settings, "ui-settings.json"), presentationSettings);
                }
                string entryRecipe = Path.Combine(root, "bootstrap.json");
                OwnedChildSupervisor.PublishTransientImmutable(entryRecipe, recipe);
                SupervisionRequest request = new($"pilot-{index:D2}-{item.Id}-fresh-{entry}", manifest.Executable, ["--game-dir", manifest.GameDir, "--battle-test", entryRecipe, "--resolution", "1920x1080", "--settings-dir", settings, "--control-dir", control, "--save-state", Path.Combine(root, "saves", "checkpoint.json")], manifest.WorkingDirectory, settings, output, control, manifest.Budgets.ToSupervision(), new Dictionary<string, string> { ["MEMORIA_FF8_GAME_DIR"] = manifest.GameDir, ["MEMORIA_FF8_EXECUTION_STACK_PROFILE"] = stackPath });
                stagingTicks += elapsed.ElapsedTicks - phaseStart;
                phaseStart = elapsed.ElapsedTicks;
                SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(request, cancellationToken).ConfigureAwait(false);
                supervisionTicks += elapsed.ElapsedTicks - phaseStart;
                exited &= outcome.ProcessExited;
                if (!outcome.ProcessExited)
                {
                    WriteNew(measurementPath + ".blocked.json", new { outcome.AttemptId, outcome.Process, outcome.Message, process_exited = false });
                }
                if (outcome.Status != "candidate_ready")
                {
                    errors.Add((outcome.ErrorCategory ?? "SUPERVISION_FAILED") + (outcome.Message is null ? "" : ": " + outcome.Message));
                    break;
                }
                phaseStart = elapsed.ElapsedTicks;
                string reportPath = Path.Combine(output, "telemetry-report.json");
                JsonElement report = ValidateCandidate(outcome.CandidatePayload!, reportPath, recipe);
                if (item.EffectId is int effectId && report.GetProperty("expected_effect").GetInt32() != effectId)
                {
                    throw new InvalidDataException("Fresh-entry report differs from the selected scenario effect.");
                }
                LiveEvidenceVerdict validation = LiveEvidenceValidation.ValidateFreshEntry(report);
                validations.Add(validation);
                errors.AddRange(validation.Errors);
                entries.Add(new(PinFreshEntryFile(reportPath), PinFreshEntryFile(Path.Combine(output, "fresh-entry-initialization.json"))));
                OwnedChildSupervisor.PublishValidatedCandidate(outcome, _ => JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 1, execution_protocol = "single-use-fresh-entry-v1", verdict = validation.Verdict, errors = validation.Errors, executions = validation.Executions.Length, campaign_acceptance = false }, RecordJson.Options));
                validationTicks += elapsed.ElapsedTicks - phaseStart;
                if (!outcome.ProcessExited || validation.Errors.Any(error => error != "PRESENTATION_BELOW_30_FPS"))
                {
                    break;
                }
            }
            long pairValidationStart = elapsed.ElapsedTicks;
            FreshEntryPairPins pins = new(PinFreshEntryFile(recipePath), originalPin, PinFreshEntryFile(stackPath), manifest.GameDir, manifest.WorkingDirectory, fingerprint, entries.ToImmutableArray());
            WriteNew(pinsPath, pins);
            FreshEntryPairVerdict pair = FreshEntryPairValidation.ValidateFiles(pins);
            errors.AddRange(pair.Errors);
            if (pair.FunctionalPass && validations.Count == 2)
            {
                verdict = errors.Count == 0 ? "PASS" : "FAIL";
            }
            Directory.CreateDirectory(Path.Combine(directory, "output"));
            WriteNew(Path.Combine(directory, "output", "result.json"), new
            {
                schema_version = 1,
                execution_protocol = "fresh-entry-pair",
                pair_pins_path = pinsPath,
                verdict,
                functional_pass = pair.FunctionalPass,
                errors = errors.Distinct().ToArray(),
                diagnostic_issues = validations.SelectMany(validation => validation.DiagnosticIssues).Distinct().ToArray(),
                copy_traffic_scope = validations.Select(validation => validation.CopyTrafficScope).FirstOrDefault(),
                executions = validations.Sum(validation => validation.Executions.Length),
                effect_duration_ms = validations.Sum(validation => validation.EffectDurationsMs.Sum()),
                execution_window_ms = validations.Sum(validation => validation.Executions.Sum(execution => execution.DurationMs)),
                effect_fps = validations.SelectMany(validation => validation.EffectExecutions).Select((scope, ordinal) => new { execution = ordinal + 1, metrics = PilotFpsMetrics.From(scope.Frames), boundary_metrics = PilotFpsMetrics.From(TelemetryFrameCollector.Recompute(scope.BoundaryIntervals, 0)), scope.BoundaryIntervalCount, scope.Policy }).ToArray(),
                invocation_fps = validations.SelectMany(validation => validation.Executions).Select(PilotFpsMetrics.From).ToArray(),
                campaign_acceptance = false
            });
            validationTicks += elapsed.ElapsedTicks - pairValidationStart;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or JsonException or ArgumentException or FormatException or OverflowException or KeyNotFoundException or NotSupportedException)
        {
            errors.Add(exception.GetType().Name + ": " + exception.Message);
        }
        elapsed.Stop();
        double factor = 1000.0 / Stopwatch.Frequency;
        PilotStageDurations stages = new(stagingTicks * factor, supervisionTicks * factor, validationTicks * factor, (elapsed.ElapsedTicks - stagingTicks - supervisionTicks - validationTicks) * factor);
        return Summarize(item.Id, verdict, errors.Distinct().ToArray(), validations.Sum(validation => validation.Executions.Length), elapsed.ElapsedTicks * factor, validations.Sum(validation => validation.EffectDurationsMs.Sum()), directory, exited, validations.Sum(validation => validation.Executions.Sum(execution => execution.DurationMs)), stages, validations.SelectMany(validation => validation.DiagnosticIssues).Distinct().ToArray(), validations.Select(validation => validation.CopyTrafficScope).FirstOrDefault()) with { ExecutionProtocol = "fresh-entry-pair", PairPinsPath = pinsPath };
    }

    public static string ResolveFreshEntryExecutionStack(string? selected)
    {
        selected ??= Environment.GetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE");
        if (string.IsNullOrEmpty(selected))
        {
            selected = Path.Combine(Memoria.FF8.Platform.ApplicationPaths.Current.LegacyState, "native-execution-stack.json");
        }
        return File.Exists(selected) ? Path.GetFullPath(selected) : throw new InvalidDataException("Fresh-entry pair requires the selected actual native execution-stack profile.");
    }

    private static FreshEntryFilePin PinFreshEntryFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return new(Path.GetFullPath(path), Convert.ToHexStringLower(SHA256.HashData(stream)));
    }
}
