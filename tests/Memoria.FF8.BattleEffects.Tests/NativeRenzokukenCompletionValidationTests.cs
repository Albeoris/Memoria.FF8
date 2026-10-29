using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class NativeRenzokukenCompletionValidationTests
{
    private static JsonObject Report(ushort effect)
    {
        // Synthetic codec/qualification fixture only; no original-resource admission or finisher RNG replay.
        JsonObject report = LiveEvidenceValidationTests.Report();
        var json = CounterCycleValidationTests.Json;
        BattleInput input = new(BattleInputKind.Action, ActorId: "party0", TargetId: "enemy0", Ability: BattleAbility.LimitBreak, ActionId: 5 << 16);
        BattleDiagnosticLimitSelection policy = new(4, 4, 256);
        report["input"] = JsonSerializer.SerializeToNode(input, json);
        report["expected_effect"] = effect;
        report["expected_native_command"] = 250;
        report["expected_native_action"] = 65535;
        report["supporting_effects"] = new JsonArray(340);
        report["supporting_invocations"] = new JsonArray();
        report["limit_selections"] = JsonSerializer.SerializeToNode(Enumerable.Range(0, 2).Select(index => new BattleDiagnosticLimitSelectionReceipt("party0", 0, 5, true, [0, 4], NativeBattleRandomState.Initialize((byte)index), NativeBattleRandomState.Initialize((byte)(index + 1)))).ToArray(), json);
        report["initialization"] = new JsonObject { ["preparation"] = new JsonObject { ["driver"] = new JsonObject { ["input"] = report["input"]!.DeepClone(), ["expected_effect"] = effect, ["expected_native_command"] = 250, ["expected_native_action"] = 65535, ["supporting_effects"] = new JsonArray(340), ["supporting_invocations"] = new JsonArray(), ["limit_selection"] = JsonSerializer.SerializeToNode(policy, json) } } };
        report["initialization"]!["target"] = new JsonObject { ["target"] = new JsonObject { ["actor_id"] = "enemy0", ["physical_index"] = 3, ["alive"] = true, ["statistics_ready"] = true } };
        report["initialization"]!["preparation"]!["target"] = new JsonObject { ["actor_id"] = "enemy0", ["physical_index"] = 3 };
        List<long> triggers = [];
        for (int index = 0; index < 2; index++)
        {
            JsonObject execution = report["executions"]![index]!.AsObject();
            long callback = 10 + index * 20;
            long frame = index * 20;
            long ticks = 100 + index * 200;
            execution["start_callback"] = callback;
            execution["end_callback"] = callback + 12;
            execution["start_frame"] = frame;
            execution["end_frame"] = frame + 12;
            execution["renderer_frames"] = 12;
            execution["start_owners"]!["native_callback"] = callback;
            execution["start_owners"]!["frame_index"] = frame;
            execution["end_owners"]!["native_callback"] = callback + 12;
            execution["end_owners"]!["frame_index"] = frame + 12;
            BattleDiagnosticEffectDispatch Point(int part) => new(index * 2 + part + 1, callback + (part == 0 ? 2 : 10), "party0", BattleAbility.LimitBreak, part == 0 ? 65534 : 65535, part == 0 ? (ushort)340 : effect, part == 0 ? (ushort)340 : effect, part == 0 ? (byte)5 : (byte)250) { NativeActorIndex = 0, NativeTargetMask = 8, NativeHeaderIndex = part, NativeHeaderGeneration = index * 2 + part + 1 };
            execution["dispatches"] = JsonSerializer.SerializeToNode(new[] { Point(0), Point(1) }, json);
            TelemetryFrameCollector frames = new(ticks, 1000, 12);
            for (int ordinal = 1; ordinal <= 12; ordinal++)
            {
                frames.RecordPresentation(new(ticks + ordinal * 10, frame + ordinal, ordinal <= 10 ? "effect" : "tail", callback + ordinal, ordinal, ordinal, 0));
            }
            execution["frames"] = JsonSerializer.SerializeToNode(frames.Complete(), json);
            execution["preceding_presentation_ticks"] = ticks;
            execution["effect_started_ticks"] = ticks + 15;
            execution["effect_completed_ticks"] = ticks + 100;
            execution["effect_duration_ms"] = 85;
            object State(string id, int physical, int model, int hp) => new { id, native_index = physical, model_id = model, current_hp = hp, maximum_hp = id == "party0" ? 500 : 10000, statuses0 = 0, statuses1 = 0, magic_stocks = Array.Empty<object>() };
            int enemyHp = 10000 - index * 10;
            execution["before"] = JsonSerializer.SerializeToNode(new[] { State("party0", 0, 0, 1), State("enemy0", 3, 126, enemyHp) }, json);
            execution["after"] = JsonSerializer.SerializeToNode(new[] { State("party0", 0, 0, 1), State("enemy0", 3, 126, enemyHp - 10) }, json);
            List<BattleEvent> events = [new(frame, "Renzokuken", "party0", "enemy0", 7, "Component start.")];
            for (int hit = 0; hit < 7; hit++)
            {
                events.Add(new(frame + hit + 1, "GunbladeTrigger", "party0", "enemy0", 0, "Component trigger.", BattleAbility.LimitBreak));
                triggers.Add(callback + hit + 3);
            }
            events.Add(new(frame + 9, "Damage", "party0", "enemy0", 10, "Component damage.", BattleAbility.LimitBreak));
            execution["events"] = JsonSerializer.SerializeToNode(events, json);
            execution["resolutions"] = JsonSerializer.SerializeToNode(new[] { new BattleDiagnosticResolution(frame + 9, callback + 9, "damage", "party0", "enemy0", "enemy0", 10, 10, enemyHp, enemyHp - 10, "enemy") }, json);
        }
        report["gunblade_triggers"] = JsonSerializer.SerializeToNode(triggers, json);
        return report;
    }

    private static LiveEvidenceVerdict Validate(JsonObject report)
    {
        using JsonDocument source = JsonDocument.Parse(report.ToJsonString());
        return LiveEvidenceValidation.Validate(source.RootElement);
    }

    [Theory]
    [InlineData((ushort)162)]
    [InlineData((ushort)163)]
    [InlineData((ushort)164)]
    [InlineData((ushort)165)]
    public void Four_native_finisher_families_complete_without_fabricating_a_generic_ActionComplete(ushort effect)
    {
        JsonObject report = Report(effect);
        LiveEvidenceVerdict result = Validate(report);
        Assert.True(result.Verdict == "PASS", string.Join(Environment.NewLine, result.Errors));
        Assert.DoesNotContain(report["executions"]![0]!["events"]!.AsArray(), entry => entry!["kind"]!.GetValue<string>() == "ActionComplete");
    }

    [Theory]
    [InlineData("input")]
    [InlineData("initialization")]
    [InlineData("selection")]
    [InlineData("selection_owner")]
    [InlineData("selection_policy")]
    [InlineData("opening")]
    [InlineData("header_generation")]
    [InlineData("cross_generation")]
    [InlineData("target_mask")]
    [InlineData("physical_actor")]
    [InlineData("both_masks_wrong_owner")]
    [InlineData("initialized_target")]
    [InlineData("trigger_event")]
    [InlineData("trigger_callback")]
    [InlineData("extra_trigger")]
    [InlineData("start_marker")]
    [InlineData("cleanup")]
    [InlineData("completed")]
    [InlineData("integrity")]
    [InlineData("drop")]
    [InlineData("restore")]
    [InlineData("damage")]
    public void Every_critical_completion_gate_remains_strict_and_native_damage_validation_is_retained(string change)
    {
        JsonObject report = Report(163);
        JsonNode execution = report["executions"]![0]!;
        switch (change)
        {
            case "input":
                report["input"]!["action_id"] = 1;
                break;
            case "initialization":
                report["initialization"]!["preparation"]!["driver"]!["expected_native_action"] = 1;
                break;
            case "selection":
                report["limit_selections"]![0]!["accepted"] = false;
                break;
            case "selection_owner":
                report["limit_selections"]![0]!["command_owner"] = 6;
                break;
            case "selection_policy":
                report["limit_selections"]![0]!["crisis_selections"] = new JsonArray(0, 3);
                break;
            case "opening":
                execution["dispatches"]![0]!["requested_effect"] = 341;
                break;
            case "header_generation":
                execution["dispatches"]![1]!["native_header_generation"] = 1;
                break;
            case "cross_generation":
                report["executions"]![1]!["dispatches"]![0]!["native_header_generation"] = 2;
                break;
            case "target_mask":
                execution["dispatches"]![1]!["native_target_mask"] = 1;
                break;
            case "physical_actor":
                execution["before"]![0]!["native_index"] = 1;
                break;
            case "both_masks_wrong_owner":
                execution["before"]![1]!["native_index"] = 4;
                execution["after"]![1]!["native_index"] = 4;
                execution["dispatches"]![0]!["native_target_mask"] = 16;
                execution["dispatches"]![1]!["native_target_mask"] = 16;
                break;
            case "initialized_target":
                report["initialization"]!["target"]!["target"]!["physical_index"] = 4;
                break;
            case "trigger_event":
                execution["events"]!.AsArray().RemoveAt(1);
                break;
            case "trigger_callback":
                report["gunblade_triggers"]!.AsArray().RemoveAt(0);
                break;
            case "extra_trigger":
                report["gunblade_triggers"]!.AsArray().Add(99);
                break;
            case "start_marker":
                execution["events"]![0]!["amount"] = 8;
                break;
            case "cleanup":
                execution["cleanup_completed"] = false;
                break;
            case "completed":
                execution["completed"] = false;
                break;
            case "integrity":
                execution["integrity_error"] = "Component integrity mismatch";
                break;
            case "drop":
                execution["dropped_events"] = 1;
                break;
            case "restore":
                execution["end_owners"]!["restore_count"] = 1;
                break;
            case "damage":
                execution["resolutions"]![0]!["actual_amount"] = 9;
                break;
        }
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }
}
