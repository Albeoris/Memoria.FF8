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

    private static JsonObject Recipe()
    {
        return JsonNode.Parse("""{"driver":{"input":{"kind":"Action"}},"fixture":{"guardian_forces":[],"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[]}],"hold_enemy_commands":true}}""")!.AsObject();
    }
}
