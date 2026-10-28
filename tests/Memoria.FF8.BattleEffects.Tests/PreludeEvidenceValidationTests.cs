using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

/// <summary>Synthetic binding-function checks only. These records do not contain complete live execution evidence.</summary>
public sealed class PreludeEvidenceValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_activation_or_observed_allocation_can_bind_only_the_declared_focal_input(bool allocation)
    {
        (JsonObject bootstrap, JsonObject report) = Binding(allocation);
        JsonElement resolved = Resolve(bootstrap, report);
        Assert.Equal(allocation ? "native-allocation-1" : "enemy-2", resolved.GetProperty("actor_id").GetString());
        Assert.Equal("party-0", resolved.GetProperty("target_id").GetString());
        Assert.Equal("EnemyAbility", resolved.GetProperty("ability").GetString());
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        PreludeEvidenceValidation.ValidateReport(document.RootElement);
        Assert.Equal("ERROR", LiveEvidenceValidation.Validate(document.RootElement).Verdict);
    }

    [Fact]
    public void Report_validation_rejects_a_changed_focal_command_after_otherwise_valid_binding()
    {
        (_, JsonObject report) = Binding(true);
        report["input"]!["action_id"] = 22;
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        Assert.Throws<InvalidDataException>(() => PreludeEvidenceValidation.ValidateReport(document.RootElement));
    }

    [Theory]
    [InlineData("undeclared")]
    [InlineData("missing-receipt")]
    [InlineData("model")]
    [InlineData("scene")]
    [InlineData("physical")]
    [InlineData("flags")]
    [InlineData("relaxed-target")]
    [InlineData("command")]
    [InlineData("ordinary-command")]
    [InlineData("lifecycle")]
    [InlineData("battle")]
    [InlineData("source-model")]
    [InlineData("source-slot")]
    [InlineData("death-hash")]
    [InlineData("missing-spawn")]
    [InlineData("spawn-slot")]
    [InlineData("source-physical")]
    [InlineData("level-band")]
    [InlineData("warm-focal")]
    [InlineData("overlapping-callbacks")]
    public void Tampering_cannot_authorize_an_undeclared_or_different_native_owner(string mutation)
    {
        (JsonObject bootstrap, JsonObject report) = Binding(true);
        JsonObject initialization = report["initialization"]!.AsObject();
        JsonObject receipt = initialization["prelude"]!.AsObject();
        switch (mutation)
        {
            case "undeclared":
                bootstrap.Remove("prelude");
                break;
            case "missing-receipt":
                initialization.Remove("prelude");
                break;
            case "model":
                receipt["target"]!["model"] = 123;
                break;
            case "scene":
                receipt["target"]!["scene_slot"] = 3;
                break;
            case "physical":
                receipt["target"]!["physical_index"] = 4;
                break;
            case "flags":
                receipt["target"]!["flags"] = 0x41;
                break;
            case "relaxed-target":
                receipt["resolved_target"]!["flags_mask"] = 1;
                break;
            case "command":
                receipt["resolved_input"]!["action_id"] = 22;
                break;
            case "ordinary-command":
                bootstrap["prelude"]!["input"]!["ability"] = "Magic";
                receipt["declaration"]!["input"]!["ability"] = "Magic";
                break;
            case "lifecycle":
                receipt["lifecycle_id"] = Guid.NewGuid().ToString();
                break;
            case "battle":
                receipt["battle_instance_id"] = Guid.NewGuid().ToString();
                break;
            case "source-model":
                receipt["declaration"]!["defeated_model"] = 120;
                break;
            case "source-slot":
                receipt["declaration"]!["defeated_scene_slot"] = 1;
                break;
            case "death-hash":
                receipt["declaration"]!["death_script_sha256"] = new string('b', 64);
                break;
            case "missing-spawn":
                receipt["events"]!.AsArray().RemoveAt(1);
                break;
            case "spawn-slot":
                receipt["events"]![1]!["amount"] = 3;
                break;
            case "source-physical":
                receipt["predecessor_physical_index"] = 4;
                break;
            case "level-band":
                report["enemy_submissions"]![0]!["selection"]!["level_band"] = 1;
                break;
            case "warm-focal":
                receipt["dispatches"]![0]!["requested_effect"] = 21;
                break;
            case "overlapping-callbacks":
                receipt["end_callback"] = 101;
                break;
            default:
                throw new InvalidOperationException("Unknown synthetic mutation.");
        }
        Assert.Throws<InvalidDataException>(() => Resolve(bootstrap, report));
    }

    private static JsonElement Resolve(JsonObject bootstrap, JsonObject report)
    {
        using JsonDocument submitted = JsonDocument.Parse(bootstrap.ToJsonString());
        using JsonDocument observed = JsonDocument.Parse(report.ToJsonString());
        return PreludeEvidenceValidation.ResolveSubmittedInput(submitted.RootElement, observed.RootElement).Clone();
    }

    private static (JsonObject Bootstrap, JsonObject Report) Binding(bool allocation)
    {
        const string battle = "8e126333-8932-4acd-85e6-d8d6823e37a5";
        const string lifecycle = "611e959f-481e-4ea6-82ae-4e963032490b";
        string resolvedActor = allocation ? "native-allocation-1" : "enemy-2";
        int model = allocation ? 122 : 100;
        int physical = allocation ? 3 : 5;
        JsonObject Input(string actor) => new() { ["kind"] = "Action", ["actor_id"] = actor, ["target_id"] = "party-0", ["ability"] = "EnemyAbility", ["action_id"] = 21 };
        JsonObject Selection() => new() { ["model"] = model, ["scene_slot"] = 2, ["level_band"] = 0 };
        JsonObject declaration = new()
        {
            ["input"] = new JsonObject { ["kind"] = "Action", ["actor_id"] = "party-0", ["target_id"] = "enemy-0", ["ability"] = "Attack", ["action_id"] = 0 },
            ["defeated_model"] = allocation ? 121 : 87, ["defeated_scene_slot"] = 0, ["death_script_sha256"] = new string('a', 64),
            ["require_native_allocation"] = allocation, ["maximum_attacks"] = 256, ["maximum_callbacks"] = 18000
        };
        JsonObject target = new() { ["actor_id"] = "enemy-2", ["stage"] = 142, ["model"] = model, ["scene_slot"] = 2, ["physical_index"] = physical, ["flags_mask"] = 195, ["flags_value"] = 1, ["opening_action"] = null, ["opening_effect"] = null };
        JsonObject bootstrap = new() { ["prelude"] = declaration.DeepClone(), ["target"] = target.DeepClone(), ["driver"] = new JsonObject { ["input"] = Input("enemy-2"), ["enemy_ability"] = Selection() } };
        JsonObject resolvedTarget = target.DeepClone().AsObject();
        resolvedTarget["actor_id"] = resolvedActor;
        JsonArray events = new(new JsonObject { ["kind"] = "Damage", ["actor_id"] = "party-0", ["target_id"] = "enemy-0", ["amount"] = 100 });
        if (allocation)
        {
            events.Add(new JsonObject { ["kind"] = "NativeSpawn", ["actor_id"] = "enemy-0", ["target_id"] = resolvedActor, ["amount"] = 2 });
        }
        JsonObject receipt = new()
        {
            ["declaration"] = declaration.DeepClone(), ["battle_instance_id"] = battle, ["lifecycle_id"] = lifecycle,
            ["start_callback"] = 0, ["end_callback"] = 50, ["restore_count"] = 0, ["reset_count"] = 0, ["attacks"] = 1, ["milliseconds"] = 10,
            ["predecessor_physical_index"] = 3, ["predecessor_hit_points_before"] = 100, ["predecessor_hit_points_after"] = 0,
            ["original_input"] = Input("enemy-2"), ["resolved_input"] = Input(resolvedActor), ["original_target"] = target.DeepClone(), ["resolved_target"] = resolvedTarget,
            ["target"] = new JsonObject { ["actor_id"] = resolvedActor, ["stage"] = 142, ["model"] = model, ["scene_slot"] = 2, ["physical_index"] = physical, ["flags"] = 1, ["alive"] = true, ["statistics_ready"] = true },
            ["events"] = events, ["dispatches"] = new JsonArray(new JsonObject { ["requested_effect"] = 0, ["executed_effect"] = 0 })
        };
        JsonArray executions = new();
        JsonArray submissions = new();
        for (int index = 0; index < 2; index++)
        {
            executions.Add(new JsonObject { ["start_callback"] = 100 + index * 100, ["start_owners"] = new JsonObject { ["retained_memory_owner_id"] = lifecycle } });
            submissions.Add(new JsonObject { ["actor_id"] = resolvedActor, ["lifecycle_id"] = lifecycle, ["selection"] = Selection() });
        }
        JsonObject report = new()
        {
            ["battle_instance_id"] = battle, ["expected_effect"] = 21, ["input"] = Input(resolvedActor),
            ["initialization"] = new JsonObject { ["preparation"] = bootstrap.DeepClone(), ["prelude"] = receipt },
            ["executions"] = executions, ["enemy_submissions"] = submissions
        };
        return (bootstrap, report);
    }
}
