using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class PilotFunctionalAcceptanceTests
{
    [Fact]
    public void Only_explicit_functional_selection_excludes_fps_acceptance_and_retains_the_raw_failure()
    {
        PilotCaseSummary raw = Summary("FAIL", ["PRESENTATION_BELOW_30_FPS"]);
        Assert.Same(raw, PilotRunner.ApplyAcceptance(raw, false));
        PilotCaseSummary functional = PilotRunner.ApplyAcceptance(raw, true);
        Assert.Equal("PASS", functional.Verdict);
        Assert.Empty(functional.Errors);
        Assert.True(functional.FunctionalOnly);
        Assert.Equal("deferred", functional.PerformanceStatus);
        Assert.Equal(new[] { "PRESENTATION_BELOW_30_FPS" }, functional.PerformanceIssues);
        Assert.Equal("FAIL", raw.Verdict);
        Assert.Equal(new[] { "PRESENTATION_BELOW_30_FPS" }, raw.Errors);
        Assert.Equal(raw.EffectDurationMs, functional.EffectDurationMs);
        Assert.Equal(raw.ExecutionWindowMs, functional.ExecutionWindowMs);
    }

    [Theory]
    [InlineData("TERMINAL_HOLD_PARTY_PROVENANCE_INVALID")]
    [InlineData("INVOCATION_RELEASE_INVALID")]
    [InlineData("REPORT_INVALID")]
    public void Functional_selection_preserves_every_non_fps_error(string error)
    {
        PilotCaseSummary functional = PilotRunner.ApplyAcceptance(Summary("ERROR", ["PRESENTATION_BELOW_30_FPS", error]), true);
        Assert.Equal("ERROR", functional.Verdict);
        Assert.Equal(new[] { error }, functional.Errors);
        Assert.Equal(new[] { "PRESENTATION_BELOW_30_FPS" }, functional.PerformanceIssues);
    }

    [Fact]
    public void Functional_selection_retains_timing_execution_count_and_process_exit_guards()
    {
        PilotCaseSummary overhead = PilotRunner.ApplyAcceptance(PilotRunner.Summarize("case", "FAIL", ["PRESENTATION_BELOW_30_FPS"], 2, 20000, 100, "unused", true), true);
        Assert.Equal("FAIL", overhead.Verdict);
        Assert.Equal(new[] { "OVERHEAD_EXCEEDS_10000_MS" }, overhead.Errors);
        PilotCaseSummary exit = PilotRunner.ApplyAcceptance(PilotRunner.Summarize("case", "FAIL", ["PRESENTATION_BELOW_30_FPS"], 2, 100, 50, "unused", false), true);
        Assert.Equal("ERROR", exit.Verdict);
        Assert.Equal(new[] { "PROCESS_EXIT_UNCONFIRMED" }, exit.Errors);
        PilotCaseSummary timing = PilotRunner.ApplyAcceptance(PilotRunner.Summarize("case", "FAIL", ["PRESENTATION_BELOW_30_FPS"], 2, 100, 101, "unused", true), true);
        Assert.Equal("ERROR", timing.Verdict);
        Assert.Contains("TIMING_EVIDENCE_INVALID", timing.Errors);
        Assert.Equal("FAIL", PilotRunner.ApplyAcceptance(Summary("FAIL", ["PRESENTATION_BELOW_30_FPS"]) with { Executions = 1 }, true).Verdict);
        Assert.Equal("ERROR", PilotRunner.ApplyAcceptance(Summary("ERROR", ["PRESENTATION_BELOW_30_FPS"]), true).Verdict);
    }

    [Fact]
    public void Functional_manifest_selection_roundtrips_and_absent_selection_remains_strict()
    {
        PilotManifest manifest = new(1, "host", "working", "game", "output", new(1, 1, 1, 1, 1, 1, 1), [new("case", "recipe")]);
        Assert.False(manifest.FunctionalOnly);
        JsonObject json = JsonSerializer.SerializeToNode(manifest with { FunctionalOnly = true }, RecordJson.Options)!.AsObject();
        PilotManifest restored = JsonSerializer.Deserialize<PilotManifest>(json.ToJsonString(), RecordJson.Options)!;
        Assert.True(restored.FunctionalOnly);
        SchemaValidation.ValidateRecord(restored, "pilot");
        json.Remove("functional_only");
        Assert.False(JsonSerializer.Deserialize<PilotManifest>(json.ToJsonString(), RecordJson.Options)!.FunctionalOnly);
    }

    private static PilotCaseSummary Summary(string verdict, string[] errors)
    {
        return PilotRunner.Summarize("case", verdict, errors, 2, 100, 50, "unused", true);
    }
}
