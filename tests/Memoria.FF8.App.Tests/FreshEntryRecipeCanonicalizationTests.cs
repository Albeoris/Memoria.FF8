using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.Tests;

public sealed class FreshEntryRecipeCanonicalizationTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 0, 0)]
    [InlineData(false, 1, 0)]
    [InlineData(true, 1, 0)]
    [InlineData(false, 2, 0)]
    [InlineData(true, 2, 0)]
    [InlineData(false, 0, 1)]
    [InlineData(false, 0, 2)]
    [InlineData(true, 0, 3)]
    public void GeneratedRecipeExactlyEqualsActualHostTypedSerializationBeforeItIsPinned(bool explicitValues, int presentationForm, int limitsForm)
    {
        JsonObject submitted = JsonNode.Parse("""{"encounter":982,"seed":0,"flags":128,"field_battle_mode":0,"elapsed_counter":0,"battle_music":5,"primary_music_channel":-1,"fixture":{"guardian_forces":[{"id":15,"acquire":true,"learning_ability":27,"ability_points":60},{"id":3,"acquire":true}],"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[15],"hit_points":486,"commands":[{"slot":0,"ability":27}]}],"hold_enemy_commands":true},"target":{"actor_id":"enemy-6","stage":142,"model":126,"scene_slot":6,"physical_index":3,"flags_mask":195,"flags_value":1,"opening_action":355,"opening_effect":76},"driver":{"input":{"kind":"action","actor_id":"party-0","target_id":"party-0","ability":"nativecommand","action_id":1572867},"expected_effect":273,"expected_native_action":3,"expected_native_command":24,"fresh_entry":{"kind":"single-use-fresh-entry-v1","source_files":[{"relative_path":"FF8_EN.exe","sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}]}},"prepare_entry_before_presentation":true}""")!.AsObject();
        if (explicitValues)
        {
            submitted["entry_callback_budget"] = 2048;
            submitted["instant_guardian_force_charge"] = false;
            submitted["atb_mode"] = "turnbased";
            submitted["driver"]!["dispatch_timeout_seconds"] = 12;
            submitted["driver"]!["input"]!["frames"] = 3;
            submitted["fixture"]!["characters"]![0]!["level"] = 33;
        }
        DiagnosticLimitPreparation? limits = null;
        if (limitsForm == 1)
        {
            submitted["fixture"]!["limits"] = new JsonObject();
            limits = new();
        }
        else if (limitsForm == 2)
        {
            submitted["fixture"]!["limits"] = new JsonObject { ["learn_special_magic"] = 1 };
            limits = new() { LearnSpecialMagic = 1 };
        }
        else if (limitsForm == 3)
        {
            limits = new() { BorrowBlueMagicForSquall = true, LearnBlueMagic = 0x1234, LearnDuel = 0x3210, LearnAngelo = 0xA5, LearnSpecialMagic = 0x21, Options978 = 0x4567 };
            submitted["fixture"]!["limits"] = JsonSerializer.SerializeToNode(limits, Json);
        }
        DiagnosticPresentationDeclaration? presentation = null;
        if (presentationForm == 1)
        {
            submitted["driver"]!["presentation"] = null;
        }
        else if (presentationForm == 2)
        {
            presentation = new("release.json", "receipts", "presentation-run") { TailSeconds = 1.25, FillSelectedActorAtb = false };
            submitted["driver"]!["presentation"] = JsonSerializer.SerializeToNode(presentation, Json);
            submitted["driver"]!["fresh_entry"] = null;
        }
        string original = submitted.ToJsonString();
        JsonObject canonical = FreshEntryRecipeCanonicalization.Canonicalize(submitted);
        DiagnosticBattleRequest actual = JsonSerializer.Deserialize<DiagnosticBattleRequest>(canonical.ToJsonString(), Json)!;
        actual.Validate();
        JsonNode observed = JsonSerializer.SerializeToNode(actual, Json)!;
        Assert.True(JsonNode.DeepEquals(canonical, observed), "Frozen recipe must exactly equal the actual host request serialization, including every default and enum.");
        Assert.True(JsonNode.DeepEquals(canonical, FreshEntryRecipeCanonicalization.Canonicalize(canonical)));
        Assert.Equal(presentation, actual.Driver!.Presentation);
        Assert.Equal(limits, actual.Fixture!.Limits);
        if (limitsForm != 0)
        {
            JsonObject changed = canonical.DeepClone().AsObject();
            changed["fixture"]!["limits"]!["learn_special_magic"] = limits!.LearnSpecialMagic ^ 1;
            Assert.False(JsonNode.DeepEquals(canonical, changed));
        }
        Assert.Equal(original, submitted.ToJsonString());
        Assert.Equal("action", submitted["driver"]!["input"]!["kind"]!.GetValue<string>());
    }
}
