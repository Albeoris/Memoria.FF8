using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class CounterLiveEvidenceValidationTests
{
    private static JsonObject Report()
    {
        // Synthetic codec ledger only; no installed-resource admission or graphical host claim.
        JsonObject report = LiveEvidenceValidationTests.Report();
        var json = CounterCycleValidationTests.Json;
        DiagnosticCounterCycleReceipt[] cycles = [CounterCycleValidationTests.Cycle(), CounterCycleValidationTests.Cycle(2)];
        JsonNode input = JsonSerializer.SerializeToNode(CounterCycleValidationTests.Input, json)!;
        report["input"] = input.DeepClone();
        report["expected_effect"] = 178;
        report["expected_native_command"] = 8;
        report["expected_native_action"] = 230;
        report["counter_cycle"] = JsonSerializer.SerializeToNode(cycles[0].Declaration, json);
        report["counter_cycles"] = JsonSerializer.SerializeToNode(cycles, json);
        report["supporting_effects"] = new JsonArray();
        report["supporting_invocations"] = new JsonArray();
        report["declared_repairs"] = new JsonArray();
        report["repairs"] = new JsonArray();
        report["declared_terminal_hold"] = null;
        report["initialization"] = new JsonObject { ["preparation"] = new JsonObject { ["driver"] = new JsonObject { ["input"] = input.DeepClone(), ["expected_effect"] = 178, ["expected_native_command"] = 8, ["expected_native_action"] = 230, ["counter_cycle"] = report["counter_cycle"]!.DeepClone(), ["repairs"] = new JsonArray() } } };
        for (int index = 0; index < 2; index++)
        {
            JsonObject execution = report["executions"]![index]!.AsObject();
            long callback = (index + 1) * 10;
            long frame = index * 10;
            long ticks = 100 + index * 100;
            execution["end_callback"] = callback + 5;
            execution["end_frame"] = frame + 5;
            execution["renderer_frames"] = 5;
            execution["end_owners"]!["native_callback"] = callback + 5;
            execution["end_owners"]!["frame_index"] = frame + 5;
            execution["dispatches"] = JsonSerializer.SerializeToNode(new[] { cycles[index].Attempts[0].Dispatch }, json);
            TelemetryFrameCollector frames = new(ticks, 1000, 5);
            for (int ordinal = 1; ordinal <= 5; ordinal++)
            {
                frames.RecordPresentation(new(ticks + ordinal * 10, frame + ordinal, ordinal < 4 ? "effect" : "tail", callback + ordinal, ordinal, ordinal, 0));
            }
            execution["frames"] = JsonSerializer.SerializeToNode(frames.Complete(), json);
            execution["effect_started_ticks"] = ticks + 25;
            execution["effect_completed_ticks"] = ticks + 40;
            execution["effect_duration_ms"] = 15;
            int hp = 100 - index * 10;
            object State(string id, int physical, int model, int health) => new { id, native_index = physical, model_id = model, current_hp = health, maximum_hp = 100, statuses0 = 0, statuses1 = 0, magic_stocks = Array.Empty<object>() };
            execution["before"] = JsonSerializer.SerializeToNode(new[] { State("party0", 0, 0, hp), State("enemy0", 3, 84, hp) }, json);
            execution["after"] = JsonSerializer.SerializeToNode(new[] { State("party0", 0, 0, hp - 10), State("enemy0", 3, 84, hp - 10) }, json);
            execution["events"] = JsonSerializer.SerializeToNode(new[] { new { kind = "Damage", actor_id = "party0" }, new { kind = "Damage", actor_id = "enemy0" }, new { kind = "ActionComplete", actor_id = "party0" }, new { kind = "ActionComplete", actor_id = "enemy0" } }, json);
            object Resolution(string actor, string target, long cb) => new { frame = frame + 1, callback = cb, kind = "damage", actor_id = actor, original_target_id = target, actual_target_id = target, resolved_amount = 10, actual_amount = 10, before_hp = hp, after_hp = hp - 10, hp_owner = target == "party0" ? "character" : "enemy" };
            execution["resolutions"] = JsonSerializer.SerializeToNode(new[] { Resolution("party0", "enemy0", callback + 1), Resolution("enemy0", "party0", callback + 2) }, json);
        }
        return report;
    }

    private static LiveEvidenceVerdict Validate(JsonObject report)
    {
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        return LiveEvidenceValidation.Validate(document.RootElement);
    }

    [Fact]
    public void Typed_counter_source_chain_does_not_alias_the_control_input_actor_or_ability()
    {
        LiveEvidenceVerdict result = Validate(Report());
        Assert.True(result.Verdict == "PASS", string.Join(Environment.NewLine, result.Errors));
    }

    [Theory]
    [InlineData("declaration")]
    [InlineData("missing_cycles")]
    [InlineData("source_hp")]
    [InlineData("source_mask")]
    [InlineData("owner")]
    [InlineData("unbound_repair")]
    [InlineData("prelude")]
    [InlineData("missing_preliminary")]
    public void Missing_or_forged_counter_evidence_is_rejected(string change)
    {
        JsonObject report = Report();
        switch (change)
        {
            case "declaration":
                report["counter_cycle"]!["max_trigger_attempts"] = 33;
                break;
            case "missing_cycles":
                report.Remove("counter_cycles");
                break;
            case "source_hp":
                report["executions"]![0]!["resolutions"]![0]!["before_hp"] = 99;
                break;
            case "source_mask":
                report["counter_cycles"]![0]!["attempts"]![0]!["dispatch"]!["native_target_mask"] = 0;
                break;
            case "owner":
                report["executions"]![0]!["before"]![1]!["model_id"] = 85;
                break;
            case "missing_preliminary":
                report["counter_cycles"]![0]!["attempts"]![0]!["contacts"]![0]!["primary"]!.AsObject().Remove("preliminary");
                break;
            case "prelude":
                report["initialization"]!["preparation"]!["prelude"] = new JsonObject();
                break;
            case "unbound_repair":
                report["repairs"] = new JsonArray(new JsonObject());
                break;
        }
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }
}
