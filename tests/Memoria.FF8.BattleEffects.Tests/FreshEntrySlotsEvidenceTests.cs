using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class FreshEntrySlotsEvidenceTests
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new JsonStringEnumConverter() } };
    private static JsonNode Node<T>(T value) => JsonSerializer.SerializeToNode(value, Json)!;

    [Fact]
    public void Fresh_entry_accepts_one_genuine_owned_roll_and_Do_but_default_still_requires_two()
    {
        var report = FreshReport();
        using var document = JsonDocument.Parse(report.ToJsonString());
        SlotsLiveEvidenceValidation.ValidateFreshEntryReport(document.RootElement);
        Assert.Throws<InvalidDataException>(() => SlotsLiveEvidenceValidation.ValidateReport(document.RootElement));
    }

    [Theory]
    [InlineData("rng")]
    [InlineData("used-roll")]
    [InlineData("actor")]
    [InlineData("latch")]
    [InlineData("index")]
    public void One_entry_preserves_original_roll_RNG_owner_Do_and_index_authentication(string fault)
    {
        var report = FreshReport();
        var receipt = report["slots_admissions"]![0]!.AsObject();
        switch (fault)
        {
            case "rng":
                DiagnosticSlotsAdmissionReceipt current = receipt.Deserialize<DiagnosticSlotsAdmissionReceipt>(Json)!;
                NativeBattleRandomState random = NativeBattleRandomState.Initialize(99);
                NativeBattleRandomState next = random.Draw(out _).Draw(out _).Draw(out _).Draw(out _);
                current = current with { Start = current.Start with { Random = random }, Selection = current.Selection with { RandomBefore = random, RandomAfter = random }, Rolls = [current.Rolls[0] with { Before = current.Rolls[0].Before with { Random = random }, After = current.Rolls[0].After with { Random = next } }], Do = current.Do! with { Before = current.Do.Before with { Random = next }, After = current.Do.After with { Random = next } } };
                report["slots_admissions"]![0] = Node(current);
                break;
            case "used-roll":
                receipt["do"]!["used_roll"]!["magic_id"] = 52;
                break;
            case "actor":
                receipt["do"]!["actor_id"] = "squall";
                break;
            case "latch":
                receipt["do"] = null;
                break;
            case "index":
                receipt["rolls"]![0]!["index"] = 2;
                break;
        }
        using var document = JsonDocument.Parse(report.ToJsonString());
        Assert.Throws<InvalidDataException>(() => SlotsLiveEvidenceValidation.ValidateFreshEntryReport(document.RootElement));
    }

    private static JsonObject FreshReport()
    {
        var report = Report();
        foreach (string name in new[] { "slots_admissions", "limit_selections", "executions" })
        {
            report[name]!.AsArray().RemoveAt(1);
        }
        report["executions"]![0]!["end_owners"] = report["executions"]![0]!["start_owners"]!.DeepClone();
        report["executions"]![0]!["end_owners"]!["native_callback"] = report["executions"]![0]!["end_callback"]!.DeepClone();
        report["executions"]![0]!["end_owners"]!["frame_index"] = report["executions"]![0]!["end_callback"]!.DeepClone();
        DiagnosticFreshEntryDeclaration declaration = new(DiagnosticFreshEntryDeclaration.SingleUse, [new("Data/battle.fs", new string('a', 64))]);
        var driver = report["initialization"]!["preparation"]!["driver"]!.AsObject();
        driver["fresh_entry"] = Node(declaration);
        driver["repairs"] = new JsonArray();
        driver["supporting_effects"] = new JsonArray();
        driver["supporting_invocations"] = new JsonArray();
        report["fresh_entry"] = Node(declaration);
        report["initialization"]!["initializer_version"] = "direct-new-game-v2";
        report["initialization"]!["ordered_stages"] = new JsonArray("installed-cold-process", "canonical-encounter-construction", "resource-derived-new-game");
        report["initialization"]!["fresh_entry_host"] = Node(new DiagnosticFreshEntryHostReceipt(Guid.Parse(report["battle_instance_id"]!.GetValue<string>()), Guid.Parse(report["executions"]![0]!["start_owners"]!["retained_memory_owner_id"]!.GetValue<string>()), Guid.NewGuid(), 1, 1000, new string('a', 64), new string('b', 64), new string('c', 64), declaration.SourceFiles));
        return report;
    }

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
