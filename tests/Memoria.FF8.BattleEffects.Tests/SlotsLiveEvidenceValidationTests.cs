using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class SlotsLiveEvidenceValidationTests
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Optin_binds_both_admissions_to_initialized_source_and_independent_execution_anchors()
    {
        JsonObject report = Report();
        string before = report.ToJsonString();
        Validate(report);
        Assert.Equal(before, report.ToJsonString());
    }

    [Fact]
    public void Ordinary_reports_do_not_require_or_read_Slot_source_owners()
    {
        Validate(new JsonObject());
        Validate(new JsonObject { ["slots_admission"] = null, ["slots_admissions"] = new JsonArray() });
        Assert.Throws<InvalidDataException>(() => Validate(new JsonObject { ["expected_native_command"] = 16 }));
    }

    [Theory]
    [InlineData("declaration")]
    [InlineData("initialized")]
    [InlineData("source")]
    [InlineData("actor")]
    [InlineData("rng")]
    [InlineData("do")]
    [InlineData("used_roll")]
    [InlineData("index")]
    [InlineData("start")]
    [InlineData("learned")]
    [InlineData("dispatch")]
    [InlineData("missing")]
    public void Independent_anchors_reject_coherent_receipt_substitution_and_incomplete_lifecycles(string fault)
    {
        JsonObject report = Report();
        JsonObject receipt = report["slots_admissions"]![0]!.AsObject();
        switch (fault)
        {
            case "declaration":
                report["slots_admission"] = null;
                break;
            case "initialized":
                report["initialization"]!["preparation"]!["driver"]!["slots_admission"] = null;
                break;
            case "source":
                DiagnosticSlotsSource source = report["initialization"]!["slots_source"]!.Deserialize<DiagnosticSlotsSource>(Json)!;
                ImmutableArray<byte> spells = source.SpellTable.SetItem(0, 52);
                source = source with { SpellTable = spells, Rules = source.Rules with { SlotsSpellBytes = spells, SlotsSelectionBytes = source.SelectionTable.Concat(spells).ToImmutableArray() } };
                source = source with { Fingerprint = source.ComputeFingerprint() };
                source.Validate();
                receipt["source_fingerprint"] = source.Fingerprint;
                break;
            case "actor":
                receipt["start"]!["actor_id"] = "squall";
                receipt["selection"]!["actor_id"] = "squall";
                receipt["rolls"]![0]!["before"]!["actor_id"] = "squall";
                receipt["rolls"]![0]!["after"]!["actor_id"] = "squall";
                receipt["do"]!["actor_id"] = "squall";
                break;
            case "rng":
                DiagnosticSlotsAdmissionReceipt current = receipt.Deserialize<DiagnosticSlotsAdmissionReceipt>(Json)!;
                NativeBattleRandomState random = NativeBattleRandomState.Initialize(99);
                NativeBattleRandomState next = random.Draw(out _).Draw(out _).Draw(out _).Draw(out _);
                current = current with { Start = current.Start with { Random = random }, Selection = current.Selection with { RandomBefore = random, RandomAfter = random }, Rolls = [current.Rolls[0] with { Before = current.Rolls[0].Before with { Random = random }, After = current.Rolls[0].After with { Random = next } }], Do = current.Do! with { Before = current.Do.Before with { Random = next }, After = current.Do.After with { Random = next } } };
                report["slots_admissions"]![0] = Node(current);
                break;
            case "do":
                receipt["do"] = null;
                break;
            case "used_roll":
                receipt["do"]!["used_roll"]!["magic_id"] = 52;
                break;
            case "index":
                receipt["rolls"]![0]!["index"] = 2;
                break;
            case "start":
                report["executions"]![0]!["slots_start"]!["hit_points"] = 2;
                break;
            case "learned":
                report["executions"]![0]!["slots_end"]!["learned_flags"] = 0;
                break;
            case "dispatch":
                report["executions"]![0]!["dispatches"]![0]!["native_command"] = 2;
                break;
            case "missing":
                report["slots_admissions"]!.AsArray().RemoveAt(1);
                break;
        }
        Assert.Throws<InvalidDataException>(() => Validate(report));
    }

    [Fact]
    public void Ready_self_batch2_binds_real_reselections_to_the_flat_independent_selection_log()
    {
        JsonObject report = Batch2Report();
        string before = report.ToJsonString();
        DiagnosticSlotsAdmissionReceipt first = report["slots_admissions"]![0]!.Deserialize<DiagnosticSlotsAdmissionReceipt>(Json)!;
        Assert.Equal(3, first.Rolls.Length);
        Assert.Single(first.Reselections);
        Assert.Equal(2, first.Reselections[0].AfterRollIndex);
        Assert.Equal(51, first.Rolls[^1].Result.MagicId);
        Assert.All(first.Rolls.Take(2), roll => Assert.NotEqual(51, roll.Result.MagicId));
        Validate(report);
        Assert.Equal(before, report.ToJsonString());
    }

    [Theory]
    [InlineData("unknown-mode")]
    [InlineData("missing-request-mode")]
    [InlineData("missing-selection-log")]
    [InlineData("extra-selection-log")]
    [InlineData("selection-log-order")]
    [InlineData("missing-transition")]
    [InlineData("extra-transition")]
    [InlineData("wrong-after-roll")]
    [InlineData("foreign-selection-owner")]
    [InlineData("unchanged-owned-roll")]
    [InlineData("changed-selection-rng")]
    [InlineData("global-selection-bound")]
    [InlineData("native-roll-bound")]
    [InlineData("deadline")]
    [InlineData("first-match")]
    [InlineData("once-per-turn")]
    public void Batch2_requires_exact_declared_native_transitions_and_global_bounds(string fault)
    {
        JsonObject report = Batch2Report();
        JsonObject receipt = report["slots_admissions"]![0]!.AsObject();
        JsonObject transition = receipt["reselections"]![0]!.AsObject();
        JsonArray selections = report["limit_selections"]!.AsArray();
        switch (fault)
        {
            case "unknown-mode":
                SetDeclaration(report, declaration => declaration with { Reselection = "advance-random" });
                break;
            case "missing-request-mode":
                report["initialization"]!["preparation"]!["driver"]!["slots_admission"]!["reselection"] = null;
                break;
            case "missing-selection-log":
                selections.RemoveAt(1);
                break;
            case "extra-selection-log":
                selections.Add(selections[^1]!.DeepClone());
                break;
            case "selection-log-order":
                JsonNode initial = selections[0]!.DeepClone();
                selections[0] = selections[1]!.DeepClone();
                selections[1] = initial;
                break;
            case "missing-transition":
                receipt["reselections"] = new JsonArray();
                break;
            case "extra-transition":
                receipt["reselections"]!.AsArray().Add(transition.DeepClone());
                break;
            case "wrong-after-roll":
                transition["after_roll_index"] = 1;
                break;
            case "foreign-selection-owner":
                transition["selection"]!["actor_id"] = "squall";
                break;
            case "unchanged-owned-roll":
                transition["after"]!["owned_roll"] = transition["before"]!["owned_roll"]!.DeepClone();
                break;
            case "changed-selection-rng":
                transition["after"]!["random"] = transition["before"]!["random"]!.DeepClone();
                break;
            case "global-selection-bound":
                DiagnosticSlotsAdmissionReceipt current = receipt.Deserialize<DiagnosticSlotsAdmissionReceipt>(Json)!;
                int initialCount = current.Selection.CrisisSelections.Length;
                report["initialization"]!["preparation"]!["driver"]!["limit_selection"]!["maximum_selections"] = initialCount;
                foreach (JsonNode? entry in report["slots_admissions"]!.AsArray())
                {
                    entry!["policy"]!["maximum_selections"] = initialCount;
                }
                break;
            case "native-roll-bound":
                SetDeclaration(report, declaration => declaration with { MaximumRolls = 2 });
                break;
            case "deadline":
                transition["after"]!["seconds"] = 106;
                break;
            case "first-match":
                receipt["rolls"]![0]!["result"]!["magic_id"] = 51;
                receipt["rolls"]![0]!["after"]!["owned_roll"]!["magic_id"] = 51;
                break;
            case "once-per-turn":
                report["executions"]![0]!["slots_start"]!["limit_once_per_turn"] = true;
                receipt["start"]!["limit_once_per_turn"] = true;
                break;
        }
        Assert.Throws<InvalidDataException>(() => Validate(report));
    }

    private static void SetDeclaration(JsonObject report, Func<DiagnosticSlotsDeclaration, DiagnosticSlotsDeclaration> change)
    {
        DiagnosticSlotsDeclaration declaration = change(report["slots_admission"]!.Deserialize<DiagnosticSlotsDeclaration>(Json)!);
        report["slots_admission"] = Node(declaration);
        report["initialization"]!["preparation"]!["driver"]!["slots_admission"] = Node(declaration);
        foreach (JsonNode? receipt in report["slots_admissions"]!.AsArray())
        {
            receipt!["declaration"] = Node(declaration);
        }
    }

    // Invented component tables and pure native rule replay, never installed-resource or gameplay evidence.
    private static JsonObject Batch2Report()
    {
        JsonObject report = Report();
        DiagnosticSlotsSource original = report["initialization"]!["slots_source"]!.Deserialize<DiagnosticSlotsSource>(Json)!;
        DiagnosticSlotsObservation model = report["executions"]![0]!["slots_start"]!.Deserialize<DiagnosticSlotsObservation>(Json)! with { LimitOncePerTurn = false, RetainedCrisis = null, Crisis = 0 };
        BattleDiagnosticLimitSelection policy = new(1, 4, 256);
        byte[] spells = Enumerable.Range(0, 256).Select(index => (byte)(index % 2 == 0 ? 52 : 1)).ToArray();
        NativeBattleRandomState initial = NativeBattleRandomState.Initialize(0);
        bool found = false;
        for (int seed = 0; seed < 256 && !found; seed++)
        {
            NativeBattleRandomState candidate = NativeBattleRandomState.Initialize((byte)seed);
            BattleDiagnosticLimitSelectionReceipt selected = Select(original.Rules, model with { Random = candidate }, policy);
            NativeBattleRandomState random = selected.RandomAfter!;
            random = random.Draw(out _).Draw(out _).Draw(out byte first).Draw(out _);
            random = random.Draw(out _).Draw(out _).Draw(out byte second).Draw(out _);
            BattleDiagnosticLimitSelectionReceipt next = Select(original.Rules, model with { Random = random }, policy);
            random = next.RandomAfter!.Draw(out _).Draw(out _).Draw(out byte third).Draw(out _);
            if ((third & 7) != (first & 7) && (third & 7) != (second & 7))
            {
                spells[2 * (third & 7)] = 51;
                initial = candidate;
                found = true;
            }
        }
        Assert.True(found);
        BattleNativeLimitDefinition rules = original.Rules with { SlotsSpellBytes = spells.ToImmutableArray(), SlotsSelectionBytes = original.SelectionTable.Concat(spells).ToImmutableArray() };
        DiagnosticSlotsSource source = original with { Rules = rules, SpellTable = spells.ToImmutableArray() };
        source = source with { Fingerprint = source.ComputeFingerprint() };
        source.Validate();
        report["initialization"]!["slots_source"] = Node(source);
        DiagnosticSlotsDeclaration declaration = new(51, 256, 5) { Reselection = DiagnosticSlotsDeclaration.ReadySelfBatch2 };
        SetDeclaration(report, _ => declaration);
        report["initialization"]!["preparation"]!["driver"]!["limit_selection"] = Node(policy);
        List<DiagnosticSlotsAdmissionReceipt> receipts = [];
        List<BattleDiagnosticLimitSelectionReceipt> selections = [];
        NativeBattleRandomState currentRandom = initial;
        for (int execution = 0; execution < 2; execution++)
        {
            JsonObject window = report["executions"]![execution]!.AsObject();
            DiagnosticSlotsObservation start = window["slots_start"]!.Deserialize<DiagnosticSlotsObservation>(Json)! with { LimitOncePerTurn = false, RetainedCrisis = null, Crisis = 0, Random = currentRandom, OwnedRoll = null };
            BattleDiagnosticLimitSelectionReceipt selected = Select(rules, start, policy);
            selections.Add(selected);
            BattleInput input = report["input"]!.Deserialize<BattleInput>(Json)!;
            DiagnosticSlotsAdmission admission = new(source, new(execution + 1, source.Fingerprint, declaration, input, start, policy, selected));
            DiagnosticSlotsObservation before = start with { Seconds = start.Seconds + 0.01, Crisis = selected.CrisisSelections[^1], Random = selected.RandomAfter! };
            for (int index = 1; index <= declaration.MaximumRolls; index++)
            {
                NativeBattleRandomState next = before.Random.Draw(out byte a).Draw(out byte b).Draw(out byte c).Draw(out byte d);
                BattleNativeSlotsRoll rolled = NativeBattleSlotsRules.Roll(rules, before.Level, before.Crisis, a, b, c, d);
                DiagnosticSlotsObservation after = before with { Random = next, OwnedRoll = rolled, Seconds = before.Seconds + 0.01 };
                admission.Rolled(new(index, before, after, rolled));
                if (admission.Matched)
                {
                    DiagnosticSlotsObservation afterDo = after with { Seconds = after.Seconds + 0.01, Ready = false, Crisis = 0, RetainedCrisis = null, OwnedRoll = null };
                    admission.Did(new(after, afterDo, "selphie", "selphie", 1, 16, 51, 280, 0x8007, rolled));
                    receipts.Add(admission.Receipt);
                    window["slots_start"] = Node(start);
                    window["slots_end"] = Node(afterDo with { Callback = start.Callback + 5, SimulationFrame = start.SimulationFrame + 5, Seconds = start.Seconds + 4, LearnedFlags = 1 });
                    currentRandom = next;
                    break;
                }
                if (admission.RequiresReselection)
                {
                    BattleDiagnosticLimitSelectionReceipt selection = Select(rules, after, policy with { MaximumSelections = admission.RemainingSelections });
                    selections.Add(selection);
                    DiagnosticSlotsObservation reselected = after with { Seconds = after.Seconds + 0.01, Crisis = selection.CrisisSelections[^1], Random = selection.RandomAfter!, OwnedRoll = null };
                    admission.Reselected(new(index, after, selection, reselected));
                    before = reselected;
                }
                else
                {
                    before = after;
                }
            }
        }
        Assert.Equal(2, receipts.Count);
        report["slots_admissions"] = Node(receipts);
        report["limit_selections"] = Node(selections);
        return report;
    }

    private static BattleDiagnosticLimitSelectionReceipt Select(BattleNativeLimitDefinition rules, DiagnosticSlotsObservation before, BattleDiagnosticLimitSelection policy)
    {
        NativeBattleRandomState random = before.Random;
        ImmutableArray<byte>.Builder choices = ImmutableArray.CreateBuilder<byte>();
        for (int index = 0; index < policy.MaximumSelections; index++)
        {
            random = random.Draw(out byte draw);
            byte crisis = NativeBattleLimitRules.CrisisLevel(rules, before.HitPoints, before.MaximumHitPoints, before.Statuses0, before.Statuses1, before.DeadParty, draw, false);
            choices.Add(crisis);
            if (crisis >= policy.MinimumCrisisLevel && crisis <= policy.MaximumCrisisLevel)
            {
                return new(before.ActorId, 5, 16, true, choices.ToImmutable(), before.Random, random);
            }
        }
        throw new InvalidDataException("Synthetic component selection exhausted its unchanged native bound.");
    }
    private static void Validate(JsonObject report)
    {
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        SlotsLiveEvidenceValidation.ValidateReport(document.RootElement);
    }

    private static JsonNode Node<T>(T value) => JsonSerializer.SerializeToNode(value, Json)!;

    // Synthetic native-layout source records exercise validation without embedding installed game resources.
    private static JsonObject Report()
    {
        Guid battle = Guid.NewGuid();
        Guid lifecycle = Guid.NewGuid();
        byte[] character = new byte[36];
        character[2] = 100;
        character[4] = 16;
        byte[] selection = new byte[60];
        byte[] spells = new byte[256];
        for (int index = 0; index < spells.Length; index += 2)
        {
            spells[index] = 51;
            spells[index + 1] = 1;
        }
        byte[] action = new byte[60];
        action[4] = 24;
        action[5] = 1;
        action[10] = 8;
        BattleNativeLimitDefinition rules = new(5, 16, 0, 100, new byte[32].ToImmutableArray()) { SlotsSelectionBytes = selection.Concat(spells).ToImmutableArray(), SlotsSpellBytes = spells.ToImmutableArray() };
        DiagnosticSlotsSource source = new(battle, lifecycle, "selphie", 1, 51, 0, rules, character.ToImmutableArray(), selection.ToImmutableArray(), spells.ToImmutableArray(), new byte[60].ToImmutableArray(), action.ToImmutableArray(), "");
        source = source with { Fingerprint = source.ComputeFingerprint() };
        BattleInput input = new(BattleInputKind.Action, ActorId: "selphie", TargetId: "selphie", Ability: BattleAbility.LimitBreak, ActionId: 16 << 16 | 51);
        DiagnosticSlotsDeclaration declaration = new(51, 256, 5);
        BattleDiagnosticLimitSelection policy = new(4, 4, 256);
        List<DiagnosticSlotsAdmissionReceipt> receipts = [];
        List<BattleDiagnosticLimitSelectionReceipt> selections = [];
        JsonArray executions = [];
        NativeBattleRandomState random = NativeBattleRandomState.Initialize(17);
        for (int index = 0; index < 2; index++)
        {
            long callback = 10 + index * 10;
            byte learned = index == 0 ? (byte)0 : (byte)1;
            DiagnosticSlotsObservation start = new(battle, lifecycle, 0, 0, callback, callback, 100 + index * 10, "selphie", 1, 5, 5, 16, 8, 1, 1000, 0, 0, 0, learned, 0, false, true, 4, true, true, false, false, random, null);
            BattleDiagnosticLimitSelectionReceipt selected = new("selphie", 5, 16, true, [4], random, random);
            DiagnosticSlotsAdmission admission = new(source, new(index + 1, source.Fingerprint, declaration, input, start, policy, selected));
            DiagnosticSlotsObservation before = start with { Crisis = 4, Seconds = start.Seconds + 0.01 };
            random = random.Draw(out byte a).Draw(out byte b).Draw(out byte c).Draw(out byte d);
            BattleNativeSlotsRoll result = NativeBattleSlotsRules.Roll(rules, 8, 4, a, b, c, d);
            DiagnosticSlotsObservation after = before with { Seconds = start.Seconds + 0.02, Random = random, OwnedRoll = result };
            admission.Rolled(new(1, before, after, result));
            DiagnosticSlotsObservation afterDo = after with { Seconds = start.Seconds + 0.03, Ready = false, Crisis = 0, RetainedCrisis = null, OwnedRoll = null };
            admission.Did(new(after, afterDo, "selphie", "selphie", 1, 16, 51, 280, 0x8007, result));
            receipts.Add(admission.Receipt);
            selections.Add(selected);
            BattleDiagnosticEffectDispatch dispatch = new(index + 1, callback + 1, "selphie", BattleAbility.LimitBreak, 51, 280, 280, 16) { NativeActorIndex = 1, NativeTargetMask = 2, NativeHeaderIndex = 0, NativeHeaderGeneration = index + 1 };
            TelemetryOwnerState owners = new(battle.ToString(), 1, lifecycle.ToString(), "renderer", 1, 0, 0, callback, callback);
            BattleDiagnosticCombatant actor = new("selphie", 1, 1000, 0, 0, []) { NativeIndex = 1, ModelId = 5 };
            executions.Add(new JsonObject { ["slots_start"] = Node(start), ["slots_end"] = Node(afterDo with { Callback = callback + 5, SimulationFrame = callback + 5, Seconds = start.Seconds + 1, LearnedFlags = 1 }), ["start_callback"] = callback, ["end_callback"] = callback + 5, ["start_owners"] = Node(owners), ["before"] = Node(new[] { actor }), ["dispatches"] = Node(new[] { dispatch }) });
        }
        JsonObject driver = new() { ["input"] = Node(input), ["expected_effect"] = 280, ["expected_native_action"] = 51, ["expected_native_command"] = 16, ["limit_selection"] = Node(policy), ["slots_admission"] = Node(declaration) };
        return new() { ["battle_instance_id"] = battle.ToString(), ["input"] = Node(input), ["expected_effect"] = 280, ["expected_native_action"] = 51, ["expected_native_command"] = 16, ["slots_admission"] = Node(declaration), ["slots_admissions"] = Node(receipts), ["limit_selections"] = Node(selections), ["supporting_effects"] = new JsonArray(), ["supporting_invocations"] = new JsonArray(), ["declared_repairs"] = new JsonArray(), ["declared_terminal_hold"] = null, ["initialization"] = new JsonObject { ["battle_instance_id"] = battle.ToString(), ["slots_source"] = Node(source), ["preparation"] = new JsonObject { ["driver"] = driver } }, ["executions"] = executions };
    }
}
