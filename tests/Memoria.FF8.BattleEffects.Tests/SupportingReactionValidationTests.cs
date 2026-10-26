using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class SupportingReactionValidationTests
{
    [Fact]
    public void Declared_other_actor_reaction_requires_actual_callback_and_script_owners()
    {
        LiveEvidenceVerdict result = Validate(Report());
        Assert.True(result.Verdict == "PASS", string.Join(Environment.NewLine, result.Errors));
    }

    [Theory]
    [InlineData("undeclared")]
    [InlineData("ordinary_callback")]
    [InlineData("missing_callback")]
    [InlineData("script")]
    [InlineData("after_script")]
    [InlineData("physical_owner")]
    [InlineData("duplicate_physical")]
    [InlineData("command")]
    [InlineData("action")]
    [InlineData("declared_ordinary")]
    [InlineData("focal_replacement")]
    [InlineData("initialized_declaration")]
    public void Supporting_reaction_cannot_mask_an_unproved_dispatch(string corruption)
    {
        JsonObject report = Report();
        JsonNode dispatch = report["executions"]![0]!["dispatches"]![1]!;
        switch (corruption)
        {
            case "undeclared":
                report.Remove("supporting_invocations");
                break;
            case "ordinary_callback":
                dispatch["native_ai_callback_stage"] = 1;
                break;
            case "missing_callback":
                dispatch.AsObject().Remove("native_ai_callback_stage");
                break;
            case "script":
                dispatch["native_ai_script_sha256"] = new string('B', 64);
                break;
            case "after_script":
                report["executions"]![0]!["after"]![1]!["native_ai_script_sha256"]![2] = new string('B', 64);
                break;
            case "physical_owner":
                report["executions"]![0]!["after"]![1]!["native_index"] = 5;
                break;
            case "duplicate_physical":
                foreach (string boundary in new[] { "before", "after" })
                {
                    JsonNode duplicate = report["executions"]![0]![boundary]![1]!.DeepClone();
                    duplicate["id"] = "enemy2";
                    report["executions"]![0]![boundary]!.AsArray().Add(duplicate);
                }
                break;
            case "command":
                dispatch["native_command"] = 2;
                break;
            case "action":
                dispatch["action_id"] = 83;
                break;
            case "declared_ordinary":
                report["supporting_invocations"]![0]!["callback_stage"] = 1;
                break;
            case "focal_replacement":
                report["supporting_invocations"]![0]!["effect_id"] = 1;
                break;
            case "initialized_declaration":
                report["initialization"] = new JsonObject { ["preparation"] = new JsonObject { ["driver"] = new JsonObject { ["supporting_invocations"] = new JsonArray() } } };
                break;
        }
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }

    private static LiveEvidenceVerdict Validate(JsonObject report)
    {
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        return LiveEvidenceValidation.Validate(document.RootElement);
    }

    private static JsonObject Report()
    {
        JsonObject report = (JsonObject)typeof(LiveEvidenceValidationTests).GetMethod("Report", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        string hash = new('A', 64);
        report["supporting_invocations"] = new JsonArray(new JsonObject { ["actor_id"] = "enemy1", ["native_command"] = 8, ["action_id"] = 82, ["effect_id"] = 179, ["callback_stage"] = 2, ["script_sha256"] = hash });
        for (int index = 0; index < 2; index++)
        {
            JsonNode execution = report["executions"]![index]!;
            JsonObject enemy = new()
            {
                ["id"] = "enemy1", ["current_hp"] = 400, ["maximum_hp"] = 400, ["statuses0"] = 0, ["statuses1"] = 0,
                ["magic_stocks"] = new JsonArray(), ["native_index"] = 4, ["model_id"] = 43,
                ["native_ai_script_sha256"] = new JsonArray(hash, hash, hash, hash, hash)
            };
            execution["before"]!.AsArray().Add(enemy.DeepClone());
            execution["after"]!.AsArray().Add(enemy.DeepClone());
            execution["dispatches"]![0]!["serial"] = index * 2 + 1;
            execution["dispatches"]!.AsArray().Add(new JsonObject
            {
                ["serial"] = index * 2 + 2, ["callback"] = 11 + index * 10, ["actor_id"] = "enemy1", ["ability"] = "EnemyAbility",
                ["action_id"] = 82, ["requested_effect"] = 179, ["executed_effect"] = 179, ["native_command"] = 8,
                ["native_ai_callback_stage"] = 2, ["native_ai_script_sha256"] = hash
            });
        }
        return report;
    }
}
