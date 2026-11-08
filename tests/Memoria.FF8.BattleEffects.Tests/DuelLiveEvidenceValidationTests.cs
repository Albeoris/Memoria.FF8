using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class DuelLiveEvidenceValidationTests
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    [Fact]
    public void Ordinary_duel_cannot_pass_using_the_generic_single_dispatch_contract()
    {
        JsonObject report = LiveEvidenceValidationTests.Report();
        report["input"] = JsonSerializer.SerializeToNode(new BattleInput(BattleInputKind.Action, ActorId: "party0", TargetId: "enemy0", Ability: BattleAbility.LimitBreak, ActionId: 11 << 16), Json);
        report["expected_effect"] = 274;
        report["expected_native_command"] = 241;
        report["expected_native_action"] = 0;
        Assert.Contains(Validate(report).Errors, error => error.Contains("complete cycle protocol", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("declaration_only")]
    [InlineData("receipts_only")]
    [InlineData("initialized_only")]
    public void Duel_protocol_cannot_be_added_or_removed_from_the_initialized_request(string fault)
    {
        JsonObject report = LiveEvidenceValidationTests.Report();
        JsonObject declaration = new() { ["basic_action"] = 0 };
        report["duel_cycles"] = new JsonArray();
        if (fault == "declaration_only")
        {
            report["duel_cycle"] = declaration;
        }
        else if (fault == "receipts_only")
        {
            report["duel_cycles"] = new JsonArray(new JsonObject());
        }
        else
        {
            report["initialization"] = new JsonObject { ["preparation"] = new JsonObject { ["driver"] = new JsonObject { ["duel_cycle"] = declaration } } };
        }
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }

    internal static BattleInput Input => new(BattleInputKind.Action, ActorId: "party0", TargetId: "enemy0", Ability: BattleAbility.LimitBreak, ActionId: 11 << 16);

    internal static DiagnosticDuelCycleReceipt Cycle(int index)
    {
        long callback = 10 + index * 10;
        BattleNativeDuelState opening = new(0, 8, 120, 120, BattleNativeDuelPhase.Opening) { Selection1D28DEF = 254 };
        BattleNativeDuelState ready = opening with { Phase = BattleNativeDuelPhase.Ready, RemainingTimer = 100 };
        BattleNativeDuelChoice choice = new(1, 0, "basic", true, false, [4, 8]);
        BattleNativeDuelState accepted = ready with { Phase = BattleNativeDuelPhase.AwaitingEffect, MoveSetId = 1, Selection1D28DEF = 1, InputRing = [4, 8, 0, 0, 0, 0, 0, 0], InputCursor = 1 };
        byte[] commands = [11, 241, 241, 241];
        int[] actions = [65531, 0, 0, 65532];
        BattleDiagnosticEffectDispatch[] dispatches = Enumerable.Range(0, 4).Select(stage => new BattleDiagnosticEffectDispatch(index * 4 + stage + 1, callback + stage, "party0", stage == 0 ? BattleAbility.LimitBreak : BattleAbility.NativeCommand, actions[stage], 274, 274, commands[stage]) { NativeActorIndex = 0, NativeTargetMask = 8, NativeHeaderIndex = 0, NativeHeaderGeneration = index * 4 + stage + 1 }).ToArray();
        DiagnosticDuelDispatchPackets Packet(BattleDiagnosticEffectDispatch dispatch)
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(0, dispatch.NativeCommand!.Value, 0, -1, checked((ushort)dispatch.ActionId!.Value), dispatch.ActionId == 65532 ? (ushort)0 : (ushort)274, null));
            (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            packets = packets.WritePlayerRepeatMode(0, 1).ActivateHeader(0);
            return new(dispatch.Serial, dispatch.Callback, 0, dispatch.NativeHeaderGeneration!.Value, packets)
            {
                Parameter80 = dispatch.NativeCommand == 11 ? (byte)0 : dispatch.ActionId == 65532 ? (byte)8 : (byte)2,
                Parameter82 = 0,
                HeaderEffect = dispatch.ActionId == 65532 ? (ushort)0 : (ushort)274,
                RetainedEffect = 274,
                Duel = dispatch.ActionId == 65532 ? accepted with { Phase = BattleNativeDuelPhase.Finisher, Selection1D28DEF = 254, RemainingTimer = 0 } : null
            };
        }
        return new(index + 1, "party0", "enemy0", 0, 3, new(0), new("party0", 1, 11, true, [1], NativeBattleRandomState.Initialize(0), NativeBattleRandomState.Initialize(0)), new(callback, null))
        {
            Opening = new(callback, opening),
            Inputs = [new(choice, new(callback + 1, ready), new(callback + 1, accepted))],
            ReadyAfterBasic = new(callback + 2, accepted with { Phase = BattleNativeDuelPhase.Ready, RemainingTimer = 90 }),
            TimedOut = new(callback + 3, accepted with { Phase = BattleNativeDuelPhase.Finisher, Selection1D28DEF = 255, RemainingTimer = 0 }),
            Completed = new(callback + 4, null),
            Dispatches = dispatches,
            DispatchPackets = dispatches.Select(Packet).ToArray(),
            FocalSerial = dispatches[2].Serial
        };
    }

    private static JsonObject DuelReport()
    {
        JsonObject report = LiveEvidenceValidationTests.Report();
        DiagnosticDuelCycleReceipt[] cycles = [Cycle(0), Cycle(1)];
        report["input"] = JsonSerializer.SerializeToNode(Input, Json);
        report["expected_effect"] = 274;
        report["expected_native_action"] = 0;
        report["expected_native_command"] = 241;
        report["duel_cycle"] = JsonSerializer.SerializeToNode(new DiagnosticDuelCycleDeclaration(0), Json);
        report["duel_cycles"] = JsonSerializer.SerializeToNode(cycles, Json);
        report["limit_selections"] = JsonSerializer.SerializeToNode(cycles.Select(cycle => cycle.Selection).ToArray(), Json);
        report["supporting_effects"] = new JsonArray();
        report["supporting_invocations"] = new JsonArray();
        report["initialization"] = new JsonObject { ["preparation"] = new JsonObject { ["driver"] = new JsonObject { ["input"] = JsonSerializer.SerializeToNode(Input, Json), ["expected_effect"] = 274, ["expected_native_action"] = 0, ["expected_native_command"] = 241, ["duel_cycle"] = JsonSerializer.SerializeToNode(new DiagnosticDuelCycleDeclaration(0), Json), ["limit_selection"] = JsonSerializer.SerializeToNode(new BattleDiagnosticLimitSelection(1, 1, 256), Json) } } };
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

    [Fact]
    public void Whole_duel_cycle_distinguishes_the_focal_basic_from_the_same_effect_opening()
    {
        LiveEvidenceVerdict verdict = Validate(DuelReport());
        Assert.True(verdict.Verdict == "PASS", string.Join(Environment.NewLine, verdict.Errors));
    }

    [Fact]
    public void Whole_duel_cycle_cannot_authenticate_an_initialized_preparation_prelude()
    {
        JsonObject report = DuelReport();
        Assert.Equal("PASS", Validate(report).Verdict);
        report["initialization"]!["preparation"]!["prelude"] = new JsonObject { ["scope"] = "ordinary-attack-prelude" };
        Assert.NotEqual("PASS", Validate(report).Verdict);
        report["initialization"]!["preparation"]!["prelude"] = null;
        Assert.Equal("PASS", Validate(report).Verdict);
    }

    [Theory]
    [InlineData("missing_timeout")]
    [InlineData("unlearned")]
    [InlineData("opening_as_focal")]
    [InlineData("wrong_actor")]
    [InlineData("extra_dispatch")]
    [InlineData("selection_ledger")]
    [InlineData("policy")]
    [InlineData("input_provenance")]
    [InlineData("rearm")]
    public void Whole_duel_cycle_cannot_waive_real_selection_input_completion_or_ledger_ownership(string fault)
    {
        JsonObject report = DuelReport();
        switch (fault)
        {
            case "missing_timeout":
                report["duel_cycles"]![0]!["timed_out"] = null;
                break;
            case "unlearned":
                report["duel_cycles"]![0]!["inputs"]![0]!["choice"]!["learned"] = false;
                break;
            case "opening_as_focal":
                report["duel_cycles"]![0]!["focal_serial"] = 1;
                break;
            case "wrong_actor":
                report["executions"]![0]!["after"]![0]!["native_index"] = 1;
                break;
            case "extra_dispatch":
                report["executions"]![0]!["dispatches"]!.AsArray().Add(report["executions"]![0]!["dispatches"]![0]!.DeepClone());
                break;
            case "selection_ledger":
                report["limit_selections"]![1]!["accepted"] = false;
                break;
            case "policy":
                report["initialization"]!["preparation"]!["driver"]!["limit_selection"]!["minimum_crisis_level"] = 2;
                break;
            case "input_provenance":
                report["initialization"]!["preparation"]!["driver"]!["input"]!["target_id"] = "other";
                break;
            default:
                report["duel_cycles"]![1]!["before_opening"]!["callback"] = 14;
                break;
        }
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }

    private static LiveEvidenceVerdict Validate(JsonObject report)
    {
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        return LiveEvidenceValidation.Validate(document.RootElement);
    }
}
