using Memoria.FF8.BattleEffects;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using System.Security.Cryptography;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class PilotRunnerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-pilot-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Muted_output_is_optional_and_roundtrips_without_changing_the_existing_manifest_constructor()
    {
        PilotManifest original = Manifest();
        Assert.False(original.MuteOutput);
        PilotManifest muted = original with { MuteOutput = true };
        string json = JsonSerializer.Serialize(muted, RecordJson.Options);
        PilotManifest restored = JsonSerializer.Deserialize<PilotManifest>(json, RecordJson.Options)!;
        Assert.True(restored.MuteOutput);
        Assert.Equal(original.Executable, restored.Executable);
        Assert.Equal(original.Budgets, restored.Budgets);
        SchemaValidation.ValidateRecord(restored, "pilot");
        JsonObject withoutOption = JsonNode.Parse(json)!.AsObject();
        withoutOption.Remove("mute_output");
        Assert.False(JsonSerializer.Deserialize<PilotManifest>(withoutOption.ToJsonString(), RecordJson.Options)!.MuteOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Muted_output_seeds_only_isolated_volume_preferences_before_an_invalid_host_can_start(bool muteOutput)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows owned-process launch validation is required.");
        }
        PilotManifest manifest = Manifest() with { MuteOutput = muteOutput };
        PilotBatchSummary summary = await PilotRunner.RunAsync(manifest, TestContext.Current.CancellationToken, measurementPath: Path.Combine(directory, "measurement.lock"));
        PilotCaseSummary result = Assert.Single(summary.Cases);
        Assert.Equal("ERROR", result.Verdict);
        Assert.Contains("SUPERVISION", result.Errors);
        Assert.True(result.ProcessExited);
        string settings = Path.Combine(result.Directory, "settings", "ui-settings.json");
        Assert.Equal(muteOutput, File.Exists(settings));
        if (muteOutput)
        {
            using JsonDocument preferences = JsonDocument.Parse(File.ReadAllBytes(settings));
            Assert.Equal(3, preferences.RootElement.EnumerateObject().Count());
            Assert.Equal(0, preferences.RootElement.GetProperty("MusicVolume").GetInt32());
            Assert.Equal(0, preferences.RootElement.GetProperty("SoundVolume").GetInt32());
            Assert.Equal(0, preferences.RootElement.GetProperty("VoiceVolume").GetInt32());
        }
        using JsonDocument started = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(result.Directory, "output", "started.json")));
        Assert.DoesNotContain(started.RootElement.GetProperty("arguments").EnumerateArray(), argument => argument.GetString() == "--mute-audio");
        using JsonDocument retained = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(manifest.OutputRoot, "manifest.json")));
        Assert.Equal(muteOutput, retained.RootElement.GetProperty("mute_output").GetBoolean());
    }

    [Fact]
    public void Candidate_binding_preserves_declared_native_identity_supporting_effects_and_normalized_repairs()
    {
        (JsonObject bootstrap, JsonObject report) = CandidateBinding();

        JsonElement admitted = ValidateCandidateBinding(bootstrap, report);

        Assert.Equal(64, admitted.GetProperty("expected_native_action").GetInt32());
        Assert.Equal(254, admitted.GetProperty("expected_native_command").GetInt32());
        Assert.Equal(2, admitted.GetProperty("supporting_effects")[0].GetInt32());
        Assert.False(admitted.GetProperty("declared_repairs")[0].GetProperty("revive").GetBoolean());
    }

    [Theory]
    [InlineData("native_action")]
    [InlineData("native_command")]
    [InlineData("supporting_effects")]
    [InlineData("repair_request")]
    [InlineData("repair_missing")]
    [InlineData("undeclared_native")]
    public void Candidate_cannot_change_the_frozen_bootstrap_invocation_or_repair_declaration(string forgery)
    {
        (JsonObject bootstrap, JsonObject report) = CandidateBinding();
        switch (forgery)
        {
            case "native_action":
                report["expected_native_action"] = 65;
                break;
            case "native_command":
                report["expected_native_command"] = 4;
                break;
            case "supporting_effects":
                report["supporting_effects"]!.AsArray().Add(3);
                break;
            case "repair_request":
                report["declared_repairs"]![0]!["hit_points"] = 200;
                break;
            case "repair_missing":
                report.Remove("declared_repairs");
                break;
            case "undeclared_native":
                bootstrap["driver"]!.AsObject().Remove("expected_native_action");
                bootstrap["driver"]!.AsObject().Remove("expected_native_command");
                break;
        }

        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => ValidateCandidateBinding(bootstrap, report));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private JsonElement ValidateCandidateBinding(JsonObject bootstrap, JsonObject report)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "telemetry-report.json");
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(report.ToJsonString());
        File.WriteAllBytes(path, bytes);
        JsonObject candidate = new()
        {
            ["telemetry_report"] = "telemetry-report.json",
            ["telemetry_sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            ["battle_instance_id"] = report["battle_instance_id"]!.DeepClone()
        };
        MethodInfo validator = typeof(PilotRunner).GetMethod("ValidateCandidate", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (JsonElement)validator.Invoke(null, [System.Text.Encoding.UTF8.GetBytes(candidate.ToJsonString()), path, System.Text.Encoding.UTF8.GetBytes(bootstrap.ToJsonString())])!;
    }

    private static (JsonObject Bootstrap, JsonObject Report) CandidateBinding()
    {
        JsonObject input = new() { ["kind"] = "Action", ["actor_id"] = "party-0", ["target_id"] = "enemy-0", ["ability"] = "GuardianForce", ["action_id"] = 0 };
        JsonObject bootstrap = new()
        {
            ["driver"] = new JsonObject
            {
                ["expected_effect"] = 1, ["input"] = input.DeepClone(), ["expected_native_action"] = 64, ["expected_native_command"] = 254,
                ["supporting_effects"] = new JsonArray(2), ["repairs"] = new JsonArray(new JsonObject { ["actor_id"] = "party-0", ["hit_points"] = 100 })
            }
        };
        JsonObject report = new()
        {
            ["battle_instance_id"] = "b8de1951-3be9-4360-85ac-9238406b20c8", ["expected_effect"] = 1, ["input"] = input,
            ["expected_native_action"] = 64, ["expected_native_command"] = 254, ["supporting_effects"] = new JsonArray(2),
            ["declared_repairs"] = JsonSerializer.SerializeToNode(new[] { new Memoria.FF8.Runtime.BattleDiagnosticCombatRepair("party-0", 100) }, RecordJson.Options)
        };
        return (bootstrap, report);
    }

    [Fact]
    public void Semantic_identity_accepts_enum_spelling_but_preserves_actor_target_and_action()
    {
        using JsonDocument submitted = JsonDocument.Parse("""{"kind":"action","actor_id":"party-0","target_id":"enemy-0","ability":"magic","action_id":1}""");
        using JsonDocument admitted = JsonDocument.Parse("""{"kind":"Action","actor_id":"party-0","target_id":"enemy-0","ability":"Magic","action_id":1}""");
        using JsonDocument wrongAction = JsonDocument.Parse("""{"kind":"Action","actor_id":"party-0","target_id":"enemy-0","ability":"Magic","action_id":2}""");
        using JsonDocument wrongTarget = JsonDocument.Parse("""{"kind":"Action","actor_id":"party-0","target_id":"enemy-1","ability":"Magic","action_id":1}""");
        Assert.True(PilotRunner.SameSemanticInput(submitted.RootElement, admitted.RootElement));
        Assert.False(PilotRunner.SameSemanticInput(submitted.RootElement, wrongAction.RootElement));
        Assert.False(PilotRunner.SameSemanticInput(submitted.RootElement, wrongTarget.RootElement));
    }

    [Fact]
    public void End_to_end_overhead_subtracts_both_executions_and_accepts_exact_budget()
    {
        PilotCaseSummary result = PilotRunner.Summarize("case", "PASS", [], 2, 25000, 7000 + 8000, directory, true);
        Assert.Equal(15000, result.EffectDurationMs);
        Assert.Equal(10000, result.OverheadMs);
        Assert.True(result.OverheadWithinBudget);
        Assert.Equal("PASS", result.Verdict);
        PilotCaseSummary slow = PilotRunner.Summarize("case", "PASS", [], 2, 25000.0001, 15000, directory, true);
        Assert.Equal("FAIL", slow.Verdict);
        Assert.Contains("OVERHEAD_EXCEEDS_10000_MS", slow.Errors);
    }

    [Fact]
    public void Admission_windup_and_tails_remain_overhead_even_when_the_inclusive_window_is_longer()
    {
        PilotCaseSummary result = PilotRunner.Summarize("case", "PASS", [], 2, 22000, 6000 + 4000, directory, true, 17000);
        Assert.Equal(17000, result.ExecutionWindowMs);
        Assert.Equal(10000, result.EffectDurationMs);
        Assert.Equal(12000, result.OverheadMs);
        Assert.False(result.OverheadWithinBudget);
        Assert.Equal("FAIL", result.Verdict);
    }

    [Fact]
    public void Optional_copy_diagnostics_remain_separate_from_the_external_smoke_verdict()
    {
        PilotCaseSummary result = PilotRunner.Summarize("case", "PASS", [], 2, 100, 50, directory, true, diagnosticIssues: ["LARGE_COPY_REVIEW_REQUIRED"], copyTrafficScope: "shared_partial_lower_bound");
        Assert.Equal("PASS", result.Verdict);
        Assert.Empty(result.Errors);
        Assert.Equal(new[] { "LARGE_COPY_REVIEW_REQUIRED" }, result.DiagnosticIssues);
        JsonElement record = JsonSerializer.SerializeToElement(result, RecordJson.Options);
        Assert.Equal("shared_partial_lower_bound", record.GetProperty("copy_traffic_scope").GetString());
        Assert.Equal("LARGE_COPY_REVIEW_REQUIRED", record.GetProperty("diagnostic_issues")[0].GetString());
    }

    [Fact]
    public void Nonoverlapping_stage_costs_reconcile_to_the_entire_end_to_end_total()
    {
        PilotStageDurations stages = new(10, 20000, 500, 20);
        PilotCaseSummary result = PilotRunner.Summarize("case", "PASS", [], 2, 20530, 15000, directory, true, 17000, stages);
        Assert.Equal("PASS", result.Verdict);
        Assert.Same(stages, result.Stages);
        Assert.Equal(result.TotalMs, stages.StagingMs + stages.SupervisionMs + stages.ValidationMs + stages.ResultFinalizationMs);
        Assert.Equal("ERROR", PilotRunner.Summarize("case", "PASS", [], 2, 20530, 15000, directory, true, 17000, stages with { ValidationMs = 499 }).Verdict);
    }

    [Fact]
    public void Invalid_or_incomplete_timing_and_unconfirmed_exit_cannot_pass()
    {
        Assert.Equal("ERROR", PilotRunner.Summarize("case", "PASS", [], 2, 10, 11, directory, true).Verdict);
        Assert.Equal("ERROR", PilotRunner.Summarize("case", "PASS", [], 1, 10, 5, directory, true).Verdict);
        Assert.Equal("ERROR", PilotRunner.Summarize("case", "PASS", [], 2, 10, 5, directory, false).Verdict);
        Assert.Equal("ERROR", PilotRunner.Summarize("case", "PASS", [], 2, double.NaN, 5, directory, true).Verdict);
        Assert.Equal("ERROR", PilotRunner.Summarize("case", "ERROR", ["UNSUPPORTED"], 0, 20000, 0, directory, true).Verdict);
    }

    [Fact]
    public void Operating_system_slot_prevents_overlapping_hosts_and_releases_after_disposal()
    {
        string path = Path.Combine(directory, "measurement.lock");
        using (PilotRunner.AcquireMeasurementSlot(path))
        {
            Assert.Throws<IOException>(() => PilotRunner.AcquireMeasurementSlot(path));
        }
        using FileStream next = PilotRunner.AcquireMeasurementSlot(path);
        Assert.True(next.CanWrite);
    }

    [Fact]
    public void Unconfirmed_exit_quarantine_refuses_another_measurement()
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "measurement.lock");
        File.WriteAllText(path + ".blocked.json", "{}");
        Assert.Throws<InvalidOperationException>(() => PilotRunner.AcquireMeasurementSlot(path));
    }

    [Fact]
    public void Manifest_accepts_only_explicit_bounded_unique_cases_and_finite_deadlines()
    {
        PilotManifest manifest = Manifest();
        PilotRunner.ValidateManifest(manifest);
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest with { Cases = [] }));
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest with { Cases = [manifest.Cases[0], manifest.Cases[0]] }));
        PilotRunner.ValidateManifest(manifest with { Cases = Enumerable.Range(0, 400).Select(index => new PilotCase("case" + index, manifest.Cases[0].Recipe)).ToArray() });
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest with { Budgets = manifest.Budgets with { TotalMs = double.PositiveInfinity } }));
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest with { Budgets = manifest.Budgets with { ActionOneMs = 0 } }));
    }

    [Fact]
    public async Task Full_explicit_manifest_records_each_blocker_and_independent_recipe_error_without_launching()
    {
        PilotManifest manifest = Manifest();
        PilotCase[] cases = Enumerable.Range(0, 12).Select(index => new PilotCase("effect-" + index, null, "PLANNED_BLOCKED", ["OWNER_UNRESOLVED_" + index], index)).ToArray();
        cases[1] = new("missing-recipe", Path.Combine(directory, "missing.json"));
        manifest = manifest with { Cases = cases };
        PilotBatchSummary summary = await PilotRunner.RunAsync(manifest, TestContext.Current.CancellationToken, measurementPath: Path.Combine(directory, "measurement.lock"));
        Assert.Equal(12, summary.Requested);
        Assert.Equal(12, summary.Errors);
        Assert.All(summary.Cases, result => Assert.Equal("ERROR", result.Verdict));
        Assert.Contains("OWNER_UNRESOLVED_0", summary.Cases[0].Errors);
        Assert.Contains(summary.Cases[1].Errors, error => error.StartsWith("FileNotFoundException:", StringComparison.Ordinal));
        Assert.Contains("OWNER_UNRESOLVED_11", summary.Cases[11].Errors);
        Assert.All(summary.Cases, result => Assert.True(result.ProcessExited));
        Assert.All(summary.Cases, result => Assert.True(File.Exists(Path.Combine(result.Directory, "pilot-result.json"))));
        Assert.DoesNotContain(summary.Cases, result => File.Exists(Path.Combine(result.Directory, "bootstrap.json")));
    }

    [Fact]
    public void Nonrunnable_scenarios_require_explicit_blockers_and_cannot_claim_a_pass()
    {
        PilotManifest manifest = Manifest();
        PilotCase blocked = new("blocked", null, "UNRESOLVED", ["REAL_ENCOUNTER_UNKNOWN"], 399);
        PilotRunner.ValidateManifest(manifest with { Cases = [blocked] });
        PilotCaseSummary result = PilotRunner.BlockedResult(blocked, directory);
        Assert.Equal("ERROR", result.Verdict);
        Assert.Contains("SCENARIO_UNRESOLVED", result.Errors);
        Assert.Contains("REAL_ENCOUNTER_UNKNOWN", result.Errors);
        Assert.Equal(0, result.Executions);
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest with { Cases = [blocked with { Blockers = [] }] }));
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest with { Cases = [blocked with { Status = "RUNNABLE" }] }));
        SchemaValidation.ValidateRecord(manifest with { Cases = [blocked] }, "pilot");
    }

    [Fact]
    public async Task Edited_frozen_recipe_is_an_independent_error_before_a_host_can_start()
    {
        PilotManifest manifest = Manifest();
        string recipe = manifest.Cases[0].Recipe!;
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(recipe)));
        PilotCase frozen = manifest.Cases[0] with { RecipeSha256 = hash };
        SchemaValidation.ValidateRecord(manifest with { Cases = [frozen] }, "pilot");
        PilotRunner.ValidateManifest(manifest with { Cases = [frozen] });
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest with { Cases = [frozen with { RecipeSha256 = "invalid" }] }));
        File.WriteAllText(recipe, "{\"edited\":true}");
        PilotCase next = new("later-blocked", null, "PLANNED_BLOCKED", ["OWNER_UNRESOLVED"]);
        PilotBatchSummary summary = await PilotRunner.RunAsync(manifest with { Cases = [frozen, next] }, TestContext.Current.CancellationToken, measurementPath: Path.Combine(directory, "measurement.lock"));
        Assert.Equal(2, summary.Errors);
        Assert.Contains(summary.Cases[0].Errors, error => error.Contains("RECIPE_SHA256_MISMATCH", StringComparison.Ordinal));
        Assert.Contains("OWNER_UNRESOLVED", summary.Cases[1].Errors);
        Assert.All(summary.Cases, result => Assert.False(File.Exists(Path.Combine(result.Directory, "bootstrap.json"))));
        Assert.All(summary.Cases, result => Assert.True(result.ProcessExited));
    }

    [Fact]
    public void Manifest_rejects_reused_evidence_and_output_inside_supplied_inputs()
    {
        PilotManifest manifest = Manifest();
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest with { OutputRoot = Path.Combine(manifest.GameDir, "output") }));
        Directory.CreateDirectory(manifest.OutputRoot);
        File.WriteAllText(Path.Combine(manifest.OutputRoot, "result.json"), "preserved");
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest));
        Assert.Equal("preserved", File.ReadAllText(Path.Combine(manifest.OutputRoot, "result.json")));
    }

    [Fact]
    public void Manifest_schema_rejects_missing_fields_duplicate_keys_and_enum_strings_in_deadlines()
    {
        JsonObject value = JsonSerializer.SerializeToNode(Manifest(), RecordJson.Options)!.AsObject();
        SchemaValidation.ValidateBytes(System.Text.Encoding.UTF8.GetBytes(value.ToJsonString()), "pilot");
        string duplicate = value.ToJsonString().Replace("\"schema_version\":1", "\"schema_version\":1,\"schema_version\":1", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateBytes(System.Text.Encoding.UTF8.GetBytes(duplicate), "pilot"));
        value["budgets"]!["action_one_ms"] = "Fast";
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateBytes(System.Text.Encoding.UTF8.GetBytes(value.ToJsonString()), "pilot"));
        value.Remove("budgets");
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateBytes(System.Text.Encoding.UTF8.GetBytes(value.ToJsonString()), "pilot"));
    }

    [Fact]
    public void Unknown_manifest_budget_and_case_fields_are_rejected_before_a_host_can_start()
    {
        JsonSerializerOptions permissive = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        JsonObject value = JsonSerializer.SerializeToNode(Manifest(), RecordJson.Options)!.AsObject();
        value["mode"] = "magic";
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateBytes(System.Text.Encoding.UTF8.GetBytes(value.ToJsonString()), "pilot"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PilotManifest>(value.ToJsonString(), permissive));
        value.Remove("mode");
        value["budgets"]!["speed"] = "fast";
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateBytes(System.Text.Encoding.UTF8.GetBytes(value.ToJsonString()), "pilot"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PilotManifest>(value.ToJsonString(), permissive));
        value["budgets"]!.AsObject().Remove("speed");
        value["cases"]![0]!["effect"] = "cure";
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateBytes(System.Text.Encoding.UTF8.GetBytes(value.ToJsonString()), "pilot"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PilotManifest>(value.ToJsonString(), permissive));
    }

    private PilotManifest Manifest()
    {
        string build = Path.Combine(directory, "build");
        string game = Path.Combine(directory, "game");
        Directory.CreateDirectory(build);
        Directory.CreateDirectory(game);
        string executable = Path.Combine(build, "host.exe");
        string recipe = Path.Combine(directory, "recipe.json");
        File.WriteAllText(executable, "synthetic manifest fixture; never executed");
        File.WriteAllText(recipe, "{}");
        return new(1, executable, build, game, Path.Combine(directory, "output"), new(1000, 1000, 1000, 1000, 1000, 10000, 1000), [new("case", recipe)]);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
