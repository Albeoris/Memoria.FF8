using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

/// <summary>Explicit serialization defaults for the bounded ordinary fresh-entry request, before recipe hashes are frozen.</summary>
public static class FreshEntryRecipeCanonicalization
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static JsonObject Canonicalize(JsonObject submitted)
    {
        JsonObject recipe = submitted.DeepClone().AsObject();
        Defaults(recipe, """{"driver":null,"battle_music":null,"primary_music_channel":null,"music_field_word":null,"fixture":null,"target":null,"prepare_entry_before_presentation":false,"entry_callback_budget":1024,"instant_guardian_force_charge":true,"atb_mode":"TurnBased","prelude":null}""");
        BattleAtbMode mode = recipe["atb_mode"]!.Deserialize<BattleAtbMode>(Json);
        recipe["atb_mode"] = JsonSerializer.SerializeToNode(mode, Json);
        JsonObject driver = recipe["driver"]?.AsObject() ?? throw new InvalidDataException("Fresh-entry canonicalization requires its ordinary driver.");
        Defaults(driver, """{"baseline_seconds":1,"readiness_timeout_seconds":30,"action_timeout_seconds":120,"dispatch_timeout_seconds":10,"overall_timeout_seconds":300,"tail_frames":2,"progress_path":null,"requires_capability_adapter":false,"requires_combat_repairs":false,"enemy_ability":null,"limit_selection":null,"shot_cycle":null,"slots_admission":null,"fresh_entry":null,"counter_cycle":null,"expected_native_action":null,"expected_native_command":null,"supporting_effects":[],"supporting_invocations":[],"repairs":[],"terminal_hold":null}""");
        BattleInput input = driver["input"]!.Deserialize<BattleInput>(Json) ?? throw new InvalidDataException("Fresh-entry canonicalization requires its semantic input.");
        driver["input"] = JsonSerializer.SerializeToNode(input, Json);
        if (recipe["fixture"] is JsonObject fixture)
        {
            Defaults(fixture, """{"items":[],"limits":null,"combat_party":[]}""");
            foreach (JsonObject guardian in fixture["guardian_forces"]!.AsArray().OfType<JsonObject>())
            {
                Defaults(guardian, """{"learning_ability":null,"ability_points":null}""");
            }
            foreach (JsonObject character in fixture["characters"]!.AsArray().OfType<JsonObject>())
            {
                Defaults(character, """{"magic_command":null,"commands":[],"magic_junctions":[],"hit_points":null,"level":null,"weapon":null}""");
            }
        }
        return recipe;
    }

    private static void Defaults(JsonObject owner, string serialized)
    {
        foreach ((string name, JsonNode? value) in JsonNode.Parse(serialized)!.AsObject())
        {
            if (!owner.ContainsKey(name))
            {
                owner.Add(name, value?.DeepClone());
            }
        }
    }
}
