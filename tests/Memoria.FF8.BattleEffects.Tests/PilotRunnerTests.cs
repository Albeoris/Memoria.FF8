using Memoria.FF8.BattleEffects;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class PilotRunnerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-pilot-tests-" + Guid.NewGuid().ToString("N"));

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
