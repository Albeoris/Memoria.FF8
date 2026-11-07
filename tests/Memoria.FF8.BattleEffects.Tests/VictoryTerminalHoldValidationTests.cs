using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class VictoryTerminalHoldValidationTests
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Single_fresh_victory_requires_real_ko_and_one_uninterrupted_held_released_path()
    {
        Validate(Report());
    }

    [Theory]
    [InlineData("candidate")]
    [InlineData("lifecycle")]
    [InlineData("native-result")]
    [InlineData("reset")]
    [InlineData("rearm")]
    [InlineData("not-drained")]
    [InlineData("budget")]
    [InlineData("living-enemy")]
    [InlineData("party-state")]
    [InlineData("two-executions")]
    public void Forged_or_rearmed_victory_hold_evidence_is_rejected(string corruption)
    {
        JsonObject report = Report();
        JsonObject release = report["terminal_holds"]![1]!.AsObject();
        switch (corruption)
        {
            case "candidate":
                release["candidate"] = "Defeat";
                break;
            case "lifecycle":
                release["lifecycle_id"] = Guid.NewGuid().ToString();
                break;
            case "native-result":
                release["native_result"] = 4;
                break;
            case "reset":
                release["reset_count"] = 1;
                break;
            case "rearm":
                release["kind"] = "rearmed";
                break;
            case "not-drained":
                release["drained"] = false;
                break;
            case "budget":
                release["callback"] = 200;
                break;
            case "living-enemy":
                report["executions"]![0]!["after"]![1]!["current_hp"] = 100;
                report["executions"]![0]!["after"]![1]!["statuses0"] = 0;
                break;
            case "party-state":
                release["party"]![0]!["current_hp"] = 90;
                break;
            case "two-executions":
                report["executions"]!.AsArray().Add(report["executions"]![0]!.DeepClone());
                break;
        }
        Assert.Throws<InvalidDataException>(() => Validate(report));
    }

    private static JsonObject Report()
    {
        Guid lifecycle = Guid.NewGuid();
        Guid holdId = Guid.NewGuid();
        BattleDiagnosticCombatant party = new("party", 100, 100, 0, 0, []) { NativeIndex = 0, ModelId = 5 };
        BattleDiagnosticCombatant enemy = new("enemy", 100, 100, 0, 0, []) { NativeIndex = 3, ModelId = 4 };
        BattleDiagnosticRepairRandomState random = new(NativeBattleRandomState.Initialize(0), null, null, null);
        BattleDiagnosticTerminalHoldDeclaration declaration = new("Single-use native victory cleanup", ["party"], 100) { Candidate = BattleOutcome.Victory };
        DiagnosticFreshEntryDeclaration fresh = new(DiagnosticFreshEntryDeclaration.SingleUse, [new("FF8_EN.exe", new string('a', 64))]);
        JsonObject driver = new()
        {
            ["terminal_hold"] = JsonSerializer.SerializeToNode(declaration, Json),
            ["fresh_entry"] = JsonSerializer.SerializeToNode(fresh, Json),
            ["input"] = JsonSerializer.SerializeToNode(new BattleInput(BattleInputKind.Action, ActorId: "party", TargetId: "enemy", Ability: BattleAbility.LimitBreak, ActionId: 16 << 16 | 53), Json),
            ["slots_admission"] = JsonSerializer.SerializeToNode(new DiagnosticSlotsDeclaration(53, 256, 5), Json),
            ["expected_effect"] = 288,
            ["expected_native_action"] = 53,
            ["expected_native_command"] = 16,
            ["repairs"] = new JsonArray()
        };
        BattleDiagnosticTerminalHoldReceipt held = new(holdId, lifecycle, "held", 1, 10, 10, BattleOutcome.Victory, null, false, 0, 0, random, [party]);
        return new()
        {
            ["initialization"] = new JsonObject { ["preparation"] = new JsonObject { ["driver"] = driver } },
            ["declared_terminal_hold"] = JsonSerializer.SerializeToNode(declaration, Json),
            ["fresh_entry"] = JsonSerializer.SerializeToNode(fresh, Json),
            ["repairs"] = new JsonArray(),
            ["declared_repairs"] = new JsonArray(),
            ["terminal_holds"] = JsonSerializer.SerializeToNode(new[] { held, held with { Kind = "released", Frame = 40, Callback = 40, Drained = true } }, Json),
            ["executions"] = new JsonArray(new JsonObject
            {
                ["start_callback"] = 5,
                ["end_callback"] = 30,
                ["start_owners"] = new JsonObject { ["retained_memory_owner_id"] = lifecycle.ToString() },
                ["before"] = JsonSerializer.SerializeToNode(new[] { party, enemy }, Json),
                ["after"] = JsonSerializer.SerializeToNode(new[] { party, enemy with { CurrentHp = 0, Statuses0 = 1 } }, Json)
            })
        };
    }

    private static void Validate(JsonObject report)
    {
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        VictoryTerminalHoldValidation.Validate(document.RootElement);
    }
}
