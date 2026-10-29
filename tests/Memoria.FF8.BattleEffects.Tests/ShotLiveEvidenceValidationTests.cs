using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ShotLiveEvidenceValidationTests
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    private static readonly ushort[] Effects = [188, 192, 193, 194, 195, 196, 197, 198];
    private static BattleInput Input => new(BattleInputKind.Action, ActorId: "party0", TargetId: "enemy0", Ability: BattleAbility.LimitBreak, ActionId: 14 << 16);

    private static DiagnosticShotCycleReceipt Cycle(int index, byte ammo = 0)
    {
        long callback = 10 + index * 10;
        int inventory = 3 - index;
        ushort effect = Effects[ammo];
        byte learnedAfter = (byte)(1 << ammo);
        DiagnosticShotCycleDeclaration declaration = new(ammo, effect, effect);
        DiagnosticShotObservation Point(long cb, int stock, BattleNativeShotPhase? phase, byte learned) => new(cb, stock, learned, phase is { } value ? new(ammo, 8, 120, value == BattleNativeShotPhase.Ending ? (short)-1 : value == BattleNativeShotPhase.Opening ? (short)120 : (short)100, (byte)stock, value) : null);
        byte learned = index == 0 ? (byte)0 : learnedAfter;
        byte[] commands = [14, 237, 238];
        BattleDiagnosticEffectDispatch[] dispatches = commands.Select((command, stage) => new BattleDiagnosticEffectDispatch(index * 3 + stage + 1, callback + stage + 1, "party0", command == 14 ? BattleAbility.LimitBreak : BattleAbility.NativeCommand, command == 14 ? 101 + ammo : ammo, effect, effect, command) { NativeActorIndex = 0, NativeTargetMask = 8, NativeHeaderIndex = 0, NativeHeaderGeneration = index * 3 + stage + 1 }).ToArray();
        return new(index + 1, "party0", "enemy0", 0, 3, declaration, effect, new("party0", 2, 14, true, [1], NativeBattleRandomState.Initialize(0), NativeBattleRandomState.Initialize(0)), Point(callback, inventory, null, learned))
        {
            Opening = Point(callback, inventory, BattleNativeShotPhase.Opening, learned),
            ReadyToFire = Point(callback + 1, inventory, BattleNativeShotPhase.Ready, learned),
            Triggered = Point(callback + 1, inventory - 1, BattleNativeShotPhase.AwaitingEffect, learned),
            ReadyToEnd = Point(callback + 2, inventory - 1, BattleNativeShotPhase.Ready, learnedAfter),
            Ending = Point(callback + 2, inventory - 1, BattleNativeShotPhase.Ending, learnedAfter),
            Completed = Point(callback + 4, inventory - 1, null, learnedAfter),
            Dispatches = dispatches,
            DispatchPackets = dispatches.Select(PacketProof).ToArray()
        };
    }

    private static DiagnosticShotDispatchPackets PacketProof(BattleDiagnosticEffectDispatch dispatch)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(dispatch.NativeActorIndex!.Value, dispatch.NativeCommand!.Value, 0, -1, checked((ushort)dispatch.ActionId!.Value), dispatch.RequestedEffect, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1).ActivateHeader(0);
        return new(dispatch.Serial, dispatch.Callback, dispatch.NativeHeaderIndex!.Value, dispatch.NativeHeaderGeneration!.Value, packets);
    }

    private static JsonObject Report(byte ammo = 0)
    {
        JsonObject report = LiveEvidenceValidationTests.Report();
        ushort effect = Effects[ammo];
        DiagnosticShotCycleDeclaration declaration = new(ammo, effect, effect);
        BattleInput input = Input with { ActionId = (14 << 16) | ammo };
        report["expected_effect"] = effect;
        report["expected_native_action"] = ammo;
        report["expected_native_command"] = 237;
        report["input"] = JsonSerializer.SerializeToNode(input, Json);
        report["shot_cycle"] = JsonSerializer.SerializeToNode(declaration, Json);
        DiagnosticShotCycleReceipt[] cycles = [Cycle(0, ammo), Cycle(1, ammo)];
        report["shot_cycles"] = JsonSerializer.SerializeToNode(cycles, Json);
        report["limit_selections"] = JsonSerializer.SerializeToNode(cycles.Select(cycle => cycle.Selection).ToArray(), Json);
        report["supporting_effects"] = new JsonArray();
        report["supporting_invocations"] = new JsonArray();
        report["declared_repairs"] = new JsonArray();
        report["declared_terminal_hold"] = null;
        report["initialization"] = new JsonObject { ["preparation"] = new JsonObject { ["fixture"] = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["id"] = 101 + ammo, ["count"] = 3 }) }, ["driver"] = new JsonObject { ["input"] = JsonSerializer.SerializeToNode(input, Json), ["expected_effect"] = effect, ["expected_native_action"] = ammo, ["expected_native_command"] = 237, ["limit_selection"] = JsonSerializer.SerializeToNode(new BattleDiagnosticLimitSelection(1, 4, 256), Json), ["shot_cycle"] = JsonSerializer.SerializeToNode(declaration, Json) } } };
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

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    public void Explicit_shot_protocol_admits_one_fired_dispatch_between_verified_controls_per_execution(byte ammo)
    {
        LiveEvidenceVerdict result = Validate(Report(ammo));
        Assert.True(result.Verdict == "PASS", string.Join(Environment.NewLine, result.Errors));
    }

    [Theory]
    [InlineData((byte)0, 0, 0)]
    [InlineData((byte)7, 0, 7)]
    [InlineData((byte)0, 1, 101)]
    [InlineData((byte)7, 1, 108)]
    [InlineData((byte)0, 2, 101)]
    [InlineData((byte)7, 2, 108)]
    public void Opening_item_and_firing_ending_ammo_namespaces_are_not_interchangeable(byte ammo, int stage, int forged)
    {
        JsonObject report = Report(ammo);
        report["executions"]![0]!["dispatches"]![stage]!["action_id"] = forged;
        report["shot_cycles"]![0]!["dispatches"]![stage]!["action_id"] = forged;
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }

    [Theory]
    [InlineData(0, "NativeCommand")]
    [InlineData(1, "LimitBreak")]
    [InlineData(2, "LimitBreak")]
    public void Typed_shot_semantics_reject_ability_aliases_in_each_native_stage(int stage, string ability)
    {
        JsonObject report = Report();
        report["executions"]![0]!["dispatches"]![stage]!["ability"] = ability;
        report["shot_cycles"]![0]!["dispatches"]![stage]!["ability"] = ability;
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("generation")]
    [InlineData("header_action")]
    [InlineData("contact_index")]
    [InlineData("contact_bounds")]
    [InlineData("recipient_subset")]
    [InlineData("queued_mask_alias")]
    [InlineData("actor_ledger")]
    [InlineData("target_ledger")]
    public void Typed_shot_requires_exact_canonical_contact_header_and_generation_proof(string forgery)
    {
        JsonObject report = Report();
        JsonNode proof = report["shot_cycles"]![0]!["dispatch_packets"]![2]!;
        switch (forgery)
        {
            case "missing":
                report["shot_cycles"]![0]!["dispatch_packets"] = new JsonArray();
                break;
            case "generation":
                proof["header_generation"] = 1;
                break;
            case "header_action":
                proof["packets"]!["headers"]![0]!["payload"]![4] = 1;
                break;
            case "contact_index":
                proof["packets"]!["contacts"]![0]!["primary"]!["actor_index"] = 4;
                break;
            case "contact_bounds":
                proof["packets"]!["contacts"]![0]!["primary"]!["actor_index"] = 7;
                break;
            case "actor_ledger":
                report["executions"]![0]!["before"]![0]!["native_index"] = 1;
                break;
            case "target_ledger":
                report["executions"]![0]!["after"]![1]!["native_index"] = 4;
                break;
            case "recipient_subset":
                report["executions"]![0]!["dispatches"]![2]!["native_target_mask"] = 0;
                report["shot_cycles"]![0]!["dispatches"]![2]!["native_target_mask"] = 0;
                break;
            default:
                report["executions"]![0]!["dispatches"]![2]!["native_target_mask"] = 0x80F8;
                report["shot_cycles"]![0]!["dispatches"]![2]!["native_target_mask"] = 0x80F8;
                break;
        }
        Assert.NotEqual("PASS", Validate(report).Verdict);
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
