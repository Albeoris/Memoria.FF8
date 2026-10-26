using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioDeclaredTargetTemplateTests
{
    [Theory]
    [InlineData(12, 8, 17, 2, 5)]
    [InlineData(190, 50, 63, 1, 4)]
    public void Template_accepts_a_structurally_declared_opponent(int encounter, int stage, int model, int sceneSlot, int physicalIndex)
    {
        JsonObject template = Template();
        template["encounter"] = encounter;
        template["target"]!["stage"] = stage;
        template["target"]!["model"] = model;
        template["target"]!["scene_slot"] = sceneSlot;
        template["target"]!["actor_id"] = $"enemy-{sceneSlot}";
        template["target"]!["physical_index"] = physicalIndex;
        ScenarioDiscovery.ValidateCandidateTemplate(JsonSerializer.SerializeToElement(template), 1, 1);
        Assert.Equal(($"enemy-{sceneSlot}", physicalIndex), ScenarioDiscovery.ResolveRecipeRecipient(template, false, "party-0"));
        Assert.Equal(("party-4", 0), ScenarioDiscovery.ResolveRecipeRecipient(template, true, "party-4"));
    }

    [Fact]
    public void Template_accepts_a_different_paired_original_opening()
    {
        JsonObject template = Template();
        template["target"]!["opening_action"] = 123;
        template["target"]!["opening_effect"] = 42;
        ScenarioDiscovery.ValidateCandidateTemplate(JsonSerializer.SerializeToElement(template), 1, 1);
    }

    [Theory]
    [InlineData("model", -1)]
    [InlineData("model", 239)]
    [InlineData("stage", 256)]
    [InlineData("scene_slot", 8)]
    [InlineData("physical_index", 2)]
    [InlineData("physical_index", 7)]
    [InlineData("flags_mask", 1)]
    [InlineData("flags_value", 0)]
    [InlineData("opening_action", 123)]
    public void Template_rejects_malformed_participant_contracts(string property, int value)
    {
        JsonObject template = Template();
        template["target"]![property] = value;
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCandidateTemplate(JsonSerializer.SerializeToElement(template), 1, 1));
    }

    [Fact]
    public void Template_rejects_a_scene_identity_mismatch()
    {
        JsonObject template = Template();
        template["target"]!["actor_id"] = "enemy-7";
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCandidateTemplate(JsonSerializer.SerializeToElement(template), 1, 1));
    }

    [Fact]
    public void Template_preserves_the_bounded_stock_adapter()
    {
        JsonObject template = Template();
        template["fixture"]!["characters"]![0]!["stocks"]![0]!["count"] = 1;
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCandidateTemplate(JsonSerializer.SerializeToElement(template), 1, 1));
    }

    private static JsonObject Template()
    {
        return JsonNode.Parse("""
            {
              "encounter":12,"seed":0,"flags":128,"field_battle_mode":0,"elapsed_counter":0,"battle_music":5,"primary_music_channel":-1,"prepare_entry_before_presentation":true,
              "target":{"actor_id":"enemy-2","stage":8,"model":17,"scene_slot":2,"physical_index":5,"flags_mask":195,"flags_value":1,"opening_action":null,"opening_effect":null},
              "fixture":{"hold_enemy_commands":true,"guardian_forces":[{"id":0,"acquire":true}],"characters":[{"row":0,"character_id":0,"hit_points":100,"stocks":[{"action":1,"count":2}],"guardian_forces":[0],"magic_command":{"ability":20,"slot":0}}]},
              "driver":{"input":{"kind":"action","actor_id":"party-0","ability":"magic","action_id":1,"target_id":"enemy-2"},"expected_effect":1,"tail_frames":2}
            }
            """)!.AsObject();
    }
}
