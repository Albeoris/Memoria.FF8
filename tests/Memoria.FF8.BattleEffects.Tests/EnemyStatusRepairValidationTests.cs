using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class EnemyStatusRepairValidationTests
{
    [Fact]
    public void Original_enemy_reflect_rearm_preserves_hp_and_saved_low_hp_flags()
    {
        Assert.Empty(Validate(Report()));
    }

    [Theory]
    [InlineData("hp", "REPAIR_STATUS_MUTATION_INVALID")]
    [InlineData("revive", "REPAIR_STATUS_MUTATION_INVALID")]
    [InlineData("remove_ko", "REPAIR_DECLARATION_MISMATCH")]
    [InlineData("physical_owner", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    [InlineData("model_owner", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    [InlineData("script_owner", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    [InlineData("invalid_physical", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    [InlineData("missing_model", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    [InlineData("missing_scripts", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    [InlineData("added_status", "REPAIR_STATUS_MUTATION_INVALID")]
    [InlineData("party_low_hp_flags", "REPAIR_STATUS_MUTATION_INVALID")]
    public void Enemy_rearm_rejects_hp_revival_owner_and_undeclared_status_changes(string forgery, string expectedError)
    {
        JsonObject report = Report();
        JsonObject change = report["repairs"]![0]!["changes"]![0]!.AsObject();
        JsonNode before = change["before"]!;
        JsonNode after = change["after"]!;
        JsonNode request = change["request"]!;
        switch (forgery)
        {
            case "hp":
                request["hit_points"] = 401;
                after["current_hp"] = 401;
                break;
            case "revive":
                request["revive"] = true;
                break;
            case "remove_ko":
                request["remove_statuses0"] = 1;
                break;
            case "physical_owner":
                after["native_index"] = 4;
                break;
            case "model_owner":
                after["model_id"] = 7;
                break;
            case "script_owner":
                after["native_ai_script_sha256"]![0] = new string('B', 64);
                break;
            case "invalid_physical":
                before["native_index"] = 7;
                after["native_index"] = 7;
                break;
            case "missing_model":
                before["model_id"] = null;
                after["model_id"] = null;
                break;
            case "missing_scripts":
                before["native_ai_script_sha256"] = null;
                after["native_ai_script_sha256"] = null;
                break;
            case "added_status":
                after["statuses1"] = 8193;
                break;
            case "party_low_hp_flags":
                after["statuses0"] = 0x300;
                break;
        }
        report["declared_repairs"]![0] = request.DeepClone();
        report["executions"]![0]!["after"]![0] = before.DeepClone();
        report["executions"]![1]!["before"]![0] = after.DeepClone();

        Assert.Contains(expectedError, Validate(report));
    }

    private static List<string> Validate(JsonObject report)
    {
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        List<string> errors = [];
        MethodInfo method = typeof(LiveEvidenceValidation).GetMethod("ValidateRepairs", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new InvalidOperationException("Repair validator unavailable.");
        method.Invoke(null, [document.RootElement, document.RootElement.GetProperty("executions"), errors]);
        return errors;
    }

    private static JsonObject Report()
    {
        JsonObject before = new()
        {
            ["id"] = "enemy-0", ["current_hp"] = 400, ["maximum_hp"] = 2000, ["statuses0"] = 0, ["statuses1"] = 8320,
            ["magic_stocks"] = new JsonArray(), ["native_index"] = 3, ["model_id"] = 6,
            ["native_ai_script_sha256"] = new JsonArray(Enumerable.Range(0, 5).Select(_ => (JsonNode?)JsonValue.Create(new string('A', 64))).ToArray())
        };
        JsonObject after = before.DeepClone().AsObject();
        after["statuses1"] = 8192;
        JsonObject request = new() { ["actor_id"] = "enemy-0", ["hit_points"] = 400, ["revive"] = false, ["remove_statuses0"] = 0, ["remove_statuses1"] = 128 };
        JsonObject random = new() { ["lane"] = 0 };
        return new JsonObject
        {
            ["declared_repairs"] = new JsonArray(request.DeepClone()),
            ["executions"] = new JsonArray(
                new JsonObject { ["end_callback"] = 195, ["start_owners"] = new JsonObject { ["retained_memory_owner_id"] = "memory" }, ["after"] = new JsonArray(before.DeepClone()) },
                new JsonObject { ["start_callback"] = 270, ["before"] = new JsonArray(after.DeepClone()) }),
            ["repairs"] = new JsonArray(new JsonObject
            {
                ["callback_before"] = 195, ["callback_after"] = 195, ["frame"] = 100, ["restore_count"] = 0, ["reset_count"] = 0, ["lifecycle_id"] = "memory",
                ["random_before"] = random.DeepClone(), ["random_after"] = random.DeepClone(), ["items_before"] = new JsonArray(), ["items_after"] = new JsonArray(),
                ["changes"] = new JsonArray(new JsonObject { ["request"] = request, ["before"] = before, ["after"] = after })
            })
        };
    }
}
