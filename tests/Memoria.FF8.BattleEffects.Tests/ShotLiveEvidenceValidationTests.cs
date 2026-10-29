using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ShotLiveEvidenceValidationTests
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    private static DiagnosticShotCycleDeclaration Declaration => new(0, 188, 188);
    private static BattleInput Input => new(BattleInputKind.Action, ActorId: "party0", TargetId: "enemy0", Ability: BattleAbility.LimitBreak, ActionId: 14 << 16);

    private static DiagnosticShotCycleReceipt Cycle(int index)
    {
        long callback = 10 + index * 10;
        int inventory = 3 - index;
        DiagnosticShotObservation Point(long cb, int stock, BattleNativeShotPhase? phase, byte learned) => new(cb, stock, learned, phase is { } value ? new(0, 8, 120, value == BattleNativeShotPhase.Ending ? (short)-1 : value == BattleNativeShotPhase.Opening ? (short)120 : (short)100, (byte)stock, value) : null);
        byte learned = index == 0 ? (byte)0 : (byte)1;
        byte[] commands = [14, 237, 238];
        BattleDiagnosticEffectDispatch[] dispatches = commands.Select((command, stage) => new BattleDiagnosticEffectDispatch(index * 3 + stage + 1, callback + stage + 1, "party0", BattleAbility.LimitBreak, 0, 188, 188, command) { NativeActorIndex = 0, NativeTargetMask = command == 238 ? (ushort)0x80F8 : (ushort)8, NativeHeaderIndex = 0, NativeHeaderGeneration = index * 3 + stage + 1 }).ToArray();
        return new(index + 1, "party0", "enemy0", 0, 3, Declaration, 188, new("party0", 2, 14, true, [1], NativeBattleRandomState.Initialize(0), NativeBattleRandomState.Initialize(0)), Point(callback, inventory, null, learned))
        {
            Opening = Point(callback, inventory, BattleNativeShotPhase.Opening, learned),
            ReadyToFire = Point(callback + 1, inventory, BattleNativeShotPhase.Ready, learned),
            Triggered = Point(callback + 1, inventory - 1, BattleNativeShotPhase.AwaitingEffect, learned),
            ReadyToEnd = Point(callback + 2, inventory - 1, BattleNativeShotPhase.Ready, 1),
            Ending = Point(callback + 2, inventory - 1, BattleNativeShotPhase.Ending, 1),
            Completed = Point(callback + 4, inventory - 1, null, 1),
            Dispatches = dispatches
        };
    }

    private static JsonObject Report()
    {
        JsonObject report = LiveEvidenceValidationTests.Report();
        report["expected_effect"] = 188;
        report["expected_native_action"] = 0;
        report["expected_native_command"] = 237;
        report["input"] = JsonSerializer.SerializeToNode(Input, Json);
        report["shot_cycle"] = JsonSerializer.SerializeToNode(Declaration, Json);
        DiagnosticShotCycleReceipt[] cycles = [Cycle(0), Cycle(1)];
        report["shot_cycles"] = JsonSerializer.SerializeToNode(cycles, Json);
        report["limit_selections"] = JsonSerializer.SerializeToNode(cycles.Select(cycle => cycle.Selection).ToArray(), Json);
        report["supporting_effects"] = new JsonArray();
        report["supporting_invocations"] = new JsonArray();
        report["declared_repairs"] = new JsonArray();
        report["declared_terminal_hold"] = null;
        report["initialization"] = new JsonObject { ["preparation"] = new JsonObject { ["fixture"] = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["id"] = 101, ["count"] = 3 }) }, ["driver"] = new JsonObject { ["input"] = JsonSerializer.SerializeToNode(Input, Json), ["expected_effect"] = 188, ["expected_native_action"] = 0, ["expected_native_command"] = 237, ["limit_selection"] = JsonSerializer.SerializeToNode(new BattleDiagnosticLimitSelection(1, 4, 256), Json), ["shot_cycle"] = JsonSerializer.SerializeToNode(Declaration, Json) } } };
        for (int index = 0; index < 2; index++)
        {
            JsonObject execution = report["executions"]![index]!.AsObject();
            long callback = 10 + index * 10;
            long frame = index * 10;
            long ticks = 100 + index * 100;
            execution["end_callback"] = callback + 4;
            execution["end_frame"] = frame + 4;
            execution["renderer_frames"] = 4;
            execution["end_owners"]!["native_callback"] = callback + 4;
            execution["end_owners"]!["frame_index"] = frame + 4;
            execution["dispatches"] = JsonSerializer.SerializeToNode(cycles[index].Dispatches, Json);
            TelemetryFrameCollector frames = new(ticks, 1000, 4);
            for (int ordinal = 1; ordinal <= 4; ordinal++)
            {
                frames.RecordPresentation(new(ticks + ordinal * 10, frame + ordinal, ordinal < 3 ? "effect" : "tail", callback + ordinal, ordinal, ordinal, 0));
            }
            execution["frames"] = JsonSerializer.SerializeToNode(frames.Complete(), Json);
            execution["effect_started_ticks"] = ticks + 15;
            execution["effect_completed_ticks"] = ticks + 30;
            execution["effect_duration_ms"] = 15;
            int hp = 500 - index;
            object State(string id, int health, int physical) => new { id, native_index = physical, current_hp = health, maximum_hp = 500, statuses0 = 0, statuses1 = 0, magic_stocks = Array.Empty<object>() };
            execution["before"] = JsonSerializer.SerializeToNode(new[] { State("party0", 1, 0), State("enemy0", hp, 3) }, Json);
            execution["after"] = JsonSerializer.SerializeToNode(new[] { State("party0", 1, 0), State("enemy0", hp - 1, 3) }, Json);
            execution["events"] = JsonSerializer.SerializeToNode(new[] { new { kind = "Damage", actor_id = "party0" }, new { kind = "ActionComplete", actor_id = "party0" } }, Json);
            execution["resolutions"] = JsonSerializer.SerializeToNode(new[] { new { frame = frame + 2, callback = callback + 2, kind = "damage", actor_id = "party0", original_target_id = "enemy0", actual_target_id = "enemy0", resolved_amount = 1, actual_amount = 1, before_hp = hp, after_hp = hp - 1, hp_owner = "enemy" } }, Json);
        }
        return report;
    }

    private static LiveEvidenceVerdict Validate(JsonObject report)
    {
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        return LiveEvidenceValidation.Validate(document.RootElement);
    }

    [Fact]
    public void Explicit_shot_protocol_admits_one_fired_dispatch_between_verified_controls_per_execution()
    {
        LiveEvidenceVerdict result = Validate(Report());
        Assert.True(result.Verdict == "PASS", string.Join(Environment.NewLine, result.Errors));
    }

    [Theory]
    [InlineData("omit_declaration")]
    [InlineData("recipe_mismatch")]
    [InlineData("early_focal")]
    [InlineData("extra_dispatch")]
    [InlineData("refill")]
    [InlineData("missing_cancel")]
    [InlineData("selection")]
    [InlineData("bypass")]
    [InlineData("header")]
    [InlineData("initial_stock")]
    [InlineData("selection_policy")]
    [InlineData("input_provenance")]
    public void Shot_receipt_cannot_waive_ordinary_identity_or_finite_cycle_lineage(string fault)
    {
        JsonObject report = Report();
        switch (fault)
        {
            case "omit_declaration":
                report.Remove("shot_cycle");
                break;
            case "recipe_mismatch":
                report["initialization"]!["preparation"]!["driver"]!["shot_cycle"]!["opening_effect"] = 192;
                break;
            case "early_focal":
                report["executions"]![0]!["dispatches"]![0]!["native_command"] = 237;
                break;
            case "extra_dispatch":
                report["executions"]![0]!["dispatches"]!.AsArray().Add(report["executions"]![0]!["dispatches"]![0]!.DeepClone());
                break;
            case "refill":
                report["shot_cycles"]![1]!["before_opening"]!["inventory"] = 3;
                break;
            case "missing_cancel":
                report["shot_cycles"]![0]!["ending"] = null;
                break;
            case "selection":
                report["limit_selections"]![1]!["accepted"] = false;
                break;
            case "bypass":
                report["supporting_effects"]!.AsArray().Add(192);
                break;
            case "initial_stock":
                report["initialization"]!["preparation"]!["fixture"]!["items"]![0]!["count"] = 4;
                break;
            case "selection_policy":
                report["initialization"]!["preparation"]!["driver"]!["limit_selection"]!["minimum_crisis_level"] = 2;
                break;
            case "input_provenance":
                report["initialization"]!["preparation"]!["driver"]!["input"]!["target_id"] = "other";
                break;
            default:
                report["shot_cycles"]![1]!["dispatches"]![0]!["native_header_generation"] = 1;
                report["executions"]![1]!["dispatches"]![0]!["native_header_generation"] = 1;
                break;
        }
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }
}
