using Memoria.FF8.BattleEffects;
using System.Text.Json;

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
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest with { Cases = Enumerable.Range(0, 11).Select(index => new PilotCase("case" + index, manifest.Cases[0].Recipe)).ToArray() }));
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest with { Budgets = manifest.Budgets with { TotalMs = double.PositiveInfinity } }));
        Assert.Throws<InvalidDataException>(() => PilotRunner.ValidateManifest(manifest with { Budgets = manifest.Budgets with { ActionOneMs = 0 } }));
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
