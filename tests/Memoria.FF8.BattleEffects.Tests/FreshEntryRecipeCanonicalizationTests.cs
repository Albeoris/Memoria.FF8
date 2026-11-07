using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class FreshEntryRecipeCanonicalizationTests
{
    [Fact]
    public void Omitted_optional_preparation_defaults_match_the_actual_initialized_serialization()
    {
        JsonObject submitted = Recipe();
        JsonObject explicitDefaults = Recipe();
        explicitDefaults["fixture"]!["characters"]![0]!["knocked_out"] = false;
        explicitDefaults["fixture"]!["pocket_station"] = null;
        JsonObject canonical = FreshEntryRecipeCanonicalization.Canonicalize(submitted);
        Assert.True(JsonNode.DeepEquals(canonical, FreshEntryRecipeCanonicalization.Canonicalize(explicitDefaults)));
        Assert.False(submitted["fixture"]!["characters"]![0]!.AsObject().ContainsKey("knocked_out"));
        Assert.False(submitted["fixture"]!.AsObject().ContainsKey("pocket_station"));
        Assert.False(canonical["fixture"]!["characters"]![0]!["knocked_out"]!.GetValue<bool>());
        Assert.True(canonical["fixture"]!.AsObject().ContainsKey("pocket_station"));
        Assert.Null(canonical["fixture"]!["pocket_station"]);
    }

    [Fact]
    public void Explicit_knockout_and_pocket_station_preparation_survive_default_completion()
    {
        JsonObject submitted = Recipe();
        submitted["fixture"]!["characters"]![0]!["knocked_out"] = true;
        submitted["fixture"]!["characters"]![0]!["hit_points"] = 0;
        JsonObject pocket = JsonNode.Parse("""{"available":true,"away":false,"level":12,"attack_variant":2}""")!.AsObject();
        submitted["fixture"]!["pocket_station"] = pocket;
        JsonObject canonical = FreshEntryRecipeCanonicalization.Canonicalize(submitted);
        Assert.True(canonical["fixture"]!["characters"]![0]!["knocked_out"]!.GetValue<bool>());
        Assert.Equal(0, canonical["fixture"]!["characters"]![0]!["hit_points"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(pocket, canonical["fixture"]!["pocket_station"]));
    }

    [Fact]
    public void Genuine_knockout_and_pocket_station_differences_remain_distinct_sealed_recipes()
    {
        JsonObject defaults = FreshEntryRecipeCanonicalization.Canonicalize(Recipe());
        JsonObject knockout = Recipe();
        knockout["fixture"]!["characters"]![0]!["knocked_out"] = true;
        Assert.False(JsonNode.DeepEquals(defaults, FreshEntryRecipeCanonicalization.Canonicalize(knockout)));
        JsonObject pocket = Recipe();
        pocket["fixture"]!["pocket_station"] = JsonNode.Parse("""{"available":true,"away":false,"level":12,"attack_variant":2}""");
        Assert.False(JsonNode.DeepEquals(defaults, FreshEntryRecipeCanonicalization.Canonicalize(pocket)));
    }

    [Fact]
    public void Explicit_nested_values_and_unsupported_properties_are_not_erased_by_default_completion()
    {
        JsonObject recipe = Recipe();
        JsonObject driver = recipe["driver"]!.AsObject();
        driver["enemy_ability"] = JsonNode.Parse("""{"model":1,"scene_slot":0,"level_band":0,"ability_slot":0,"command":8,"action_id":82,"sequence":11,"target_selector":0,"use_turn_script":true,"unsupported_selector":7}""");
        driver["presentation"] = JsonNode.Parse("""{"signal_path":"signal.json","receipt_directory":"receipts","run_id":"roundtrip","tail_seconds":0,"fill_selected_actor_atb":false,"visual_signal_path":"visual.json"}""");
        driver["repairs"] = JsonNode.Parse("""[{"actor_id":"party-0","hit_points":1,"revive":true,"remove_statuses0":2,"remove_statuses1":4}]""");
        driver["input"]!["unsupported_input"] = true;
        JsonObject canonical = FreshEntryRecipeCanonicalization.Canonicalize(recipe);
        foreach (string name in new[] { "enemy_ability", "presentation", "repairs" })
        {
            Assert.True(JsonNode.DeepEquals(driver[name], canonical["driver"]![name]));
        }
        Assert.True(canonical["driver"]!["input"]!["unsupported_input"]!.GetValue<bool>());
        Assert.True(canonical["driver"]!["enemy_ability"]!["use_turn_script"]!.GetValue<bool>());
    }

    private static JsonObject Recipe()
    {
        return JsonNode.Parse("""{"driver":{"input":{"kind":"Action"}},"fixture":{"guardian_forces":[],"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[]}],"hold_enemy_commands":true}}""")!.AsObject();
    }
}
