using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed partial class LiveEvidenceValidationTests
{
    [Fact]
    public void Native_ejection_preserves_status_provenance_after_actual_party_slot_retirement()
    {
        Assert.Equal("PASS", Validate(RetiredStatusRemovalReport()).Verdict);
    }

    [Theory]
    [InlineData("logical-model")]
    [InlineData("logical-id")]
    [InlineData("maximum-hp")]
    [InlineData("slot-reoccupied")]
    [InlineData("alias-reused")]
    [InlineData("missing-event")]
    [InlineData("duplicate-event")]
    [InlineData("wrong-event-target")]
    [InlineData("wrong-event-source")]
    [InlineData("early-event")]
    [InlineData("late-event")]
    [InlineData("missing-event-frame")]
    [InlineData("missing-slot")]
    [InlineData("missing-alias")]
    [InlineData("retained-slot")]
    [InlineData("retained-alias")]
    [InlineData("ko")]
    [InlineData("hp")]
    [InlineData("action-bank")]
    [InlineData("applied-bank")]
    public void Retired_status_recipient_requires_exact_native_removal_and_observed_unbound_lifecycle(string corruption)
    {
        JsonObject report = RetiredStatusRemovalReport();
        JsonObject execution = Execution(report, 0);
        JsonObject victim = execution["after"]![0]!.AsObject();
        JsonObject resolution = execution["resolutions"]![0]!.AsObject();
        JsonObject proof = resolution["native_status_removal"]!.AsObject();
        JsonObject ejected = execution["events"]!.AsArray().Single(entry => entry!["kind"]!.GetValue<string>() == "Ejected")!.AsObject();
        switch (corruption)
        {
            case "logical-model":
                victim["model_id"] = 4;
                break;
            case "logical-id":
                victim["id"] = "foreign";
                break;
            case "maximum-hp":
                victim["maximum_hp"] = 501;
                break;
            case "slot-reoccupied":
                execution["after"]![2]!["slot"] = JsonSerializer.SerializeToNode(BattleRosterSlot.Ally(0), RecordJson.Options);
                break;
            case "alias-reused":
                execution["after"]![2]!["native_index"] = 0;
                break;
            case "missing-event":
                execution["events"]!.AsArray().Remove(ejected);
                break;
            case "duplicate-event":
                execution["events"]!.AsArray().Add(ejected.DeepClone());
                break;
            case "wrong-event-target":
                ejected["target_id"] = "party1";
                break;
            case "wrong-event-source":
                ejected["actor_id"] = "enemy";
                break;
            case "early-event":
                ejected["frame"] = resolution["frame"]!.GetValue<long>() - 1;
                break;
            case "late-event":
                ejected["frame"] = execution["end_frame"]!.GetValue<long>() + 1;
                break;
            case "missing-event-frame":
                ejected.Remove("frame");
                break;
            case "missing-slot":
                victim.Remove("slot");
                break;
            case "missing-alias":
                victim.Remove("native_index");
                break;
            case "retained-slot":
                victim["slot"] = JsonSerializer.SerializeToNode(BattleRosterSlot.Ally(0), RecordJson.Options);
                break;
            case "retained-alias":
                victim["native_index"] = 0;
                break;
            case "ko":
                victim["statuses0"] = 0;
                break;
            case "hp":
                victim["current_hp"] = 1;
                break;
            case "action-bank":
                proof["action_statuses1"] = 0;
                break;
            case "applied-bank":
                proof["applied_battle_statuses"] = 0;
                break;
        }

        LiveEvidenceVerdict verdict = Validate(report);
        Assert.Contains("NATIVE_STATUS_REMOVAL_PROVENANCE_INVALID", verdict.Errors);
        Assert.Contains("SEMANTIC_RESOLUTION_INVALID", verdict.Errors);
    }

    private static JsonObject RetiredStatusRemovalReport()
    {
        JsonObject report = StatusRemovalReport();
        for (int index = 0; index < 2; index++)
        {
            JsonObject execution = Execution(report, index);
            foreach (string boundary in new[] { "before", "after" })
            {
                foreach (JsonNode? row in execution[boundary]!.AsArray())
                {
                    int physical = row!["native_index"]!.GetValue<int>();
                    row["model_id"] = physical == 3 ? 22 : physical;
                    row["slot"] = JsonSerializer.SerializeToNode(physical < 3 ? BattleRosterSlot.Ally(physical) : BattleRosterSlot.Enemy(physical - 3), RecordJson.Options);
                    if (physical < index || boundary == "after" && physical == index)
                    {
                        row["native_index"] = null;
                        row["slot"] = null;
                    }
                }
            }
            JsonObject resolution = execution["resolutions"]![0]!.AsObject();
            resolution["frame"] = (long)index * 10 + 1;
            JsonObject proof = resolution["native_status_removal"]!.AsObject();
            proof["actor_slot"] = JsonSerializer.SerializeToNode(BattleRosterSlot.Enemy(0), RecordJson.Options);
            proof["recipient_slot"] = JsonSerializer.SerializeToNode(BattleRosterSlot.Ally(index), RecordJson.Options);
            execution["events"]!.AsArray().Add(new JsonObject { ["frame"] = resolution["frame"]!.GetValue<long>() + 1, ["kind"] = "Ejected", ["actor_id"] = null, ["target_id"] = $"party{index}", ["amount"] = 0 });
        }
        return report;
    }
}
