using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;
using System.Text.Json.Nodes;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioSpecialActionRepeatTests
{
    [Theory]
    [InlineData(17, -1, -1, -1)]
    [InlineData(19, 368, 140, 3)]
    public void Installed_special_recipient_preserves_original_data_and_replaces_the_ineligible_template(byte attackType, int encounter, int stage, int model)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources for special-action recipients.");
        using BattleAssets assets = new(directory);
        JsonObject recipe = JsonNode.Parse("""{"encounter":982,"seed":0,"target":{"model":126},"fixture":{"characters":[{"hit_points":486}]},"driver":{"input":{"target_id":"enemy-6"}}}""")!.AsObject();
        Assert.True(ScenarioDiscovery.TryPrepareSpecialActionRecipient(assets, Action(attackType), recipe, out string? blocker), blocker);
        BattleEncounter actual = assets.LoadEncounter(recipe["encounter"]!.GetValue<int>());
        BattleEnemyPlacement target = Assert.Single(actual.Enemies, enemy => enemy.Enabled);
        Assert.Equal(actual.StageId, recipe["target"]!["stage"]!.GetValue<int>());
        Assert.Equal(target.ModelId, recipe["target"]!["model"]!.GetValue<int>());
        Assert.Equal($"enemy-{target.Slot}", recipe["driver"]!["input"]!["target_id"]!.GetValue<string>());
        if (attackType == 19)
        {
            Assert.Equal(encounter, actual.Id);
            Assert.Equal(stage, actual.StageId);
            Assert.Equal(model, target.ModelId);
        }
        else
        {
            BattleNativeEnemySpecial special = assets.ReadNativeEnemySpecial(target.ModelId);
            Assert.NotEqual(byte.MaxValue, special.CommonCard);
            Assert.NotEqual(byte.MaxValue, special.RareCard);
            Assert.False(ScenarioEnemyDiscovery.InitializationUnconditionallyDisablesTargeting(assets.ReadEnemyAi(target.ModelId, 0).Scripts[0]));
            Assert.StartsWith("CARD_TARGET_PRECONDITION:", ScenarioDiscovery.ResolveSpecialActionRepeatBlocker(Action(17), assets.ReadNativeEnemySpecial(61))!);
        }
        Assert.Null(recipe["target"]!["opening_action"]);
        Assert.Null(recipe["target"]!["opening_effect"]);
        Assert.Equal(attackType == 19 ? 1 : 486, recipe["fixture"]!["characters"]![0]!["hit_points"]!.GetValue<int>());
        Assert.True(recipe["fixture"]!["hold_enemy_commands"]!.GetValue<bool>());
    }

    [Fact]
    public void Card_ineligible_original_target_reports_missing_dispatch_instead_of_a_timeout_recipe()
    {
        string blocker = ScenarioDiscovery.ResolveSpecialActionRepeatBlocker(Action(17), new(255, 255, [1, 1, 1], 0))!;
        Assert.StartsWith("CARD_TARGET_PRECONDITION:", blocker);
        Assert.Contains("never dispatches", blocker);
    }

    [Fact]
    public void Devour_ineligible_original_target_reports_missing_dispatch_instead_of_a_timeout_recipe()
    {
        string blocker = ScenarioDiscovery.ResolveSpecialActionRepeatBlocker(Action(19), new(1, 2, [255, 255, 255], 0))!;
        Assert.StartsWith("DEVOUR_TARGET_PRECONDITION:", blocker);
        Assert.Contains("never dispatches", blocker);
    }

    [Theory]
    [InlineData(17, "CARD_REPEAT_PRECONDITION:")]
    [InlineData(19, "DEVOUR_REPEAT_PRECONDITION:")]
    public void Eligible_target_still_requires_a_proved_surviving_recipient_for_two_invocations(byte attackType, string prefix)
    {
        string blocker = ScenarioDiscovery.ResolveSpecialActionRepeatBlocker(Action(attackType), new(1, 2, [1, 1, 1], 0))!;
        Assert.StartsWith(prefix, blocker);
        Assert.Contains("ejects the recipient", blocker);
    }

    [Fact]
    public void A_single_eligible_Devour_level_band_does_not_certify_the_allocated_band_or_repetition()
    {
        string blocker = ScenarioDiscovery.ResolveSpecialActionRepeatBlocker(Action(19), new(255, 255, [255, 1, 255], 0))!;
        Assert.StartsWith("DEVOUR_REPEAT_PRECONDITION:", blocker);
        Assert.Contains("allocated level-band", blocker);
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ResolveSpecialActionRepeatBlocker(Action(19), new(1, 2, [], 0)));
    }

    [Fact]
    public void Ordinary_attacks_do_not_acquire_special_action_restrictions()
    {
        Assert.Null(ScenarioDiscovery.ResolveSpecialActionRepeatBlocker(Action(0), new(255, 255, [], 0)));
    }

    private static BattleNativeAction Action(byte attackType) => new(7, 9, 10, 9)
    {
        MagicId = 62,
        AttackType = attackType,
        Power = 0,
        HitCount = 1,
        TargetFlags = 0,
        AttackFlags = 0,
        DamagePresentation = 0,
        Element = 0,
        ElementPercent = null,
        StatusAttack = 0,
        Statuses0 = 0,
        Statuses1 = 0,
        Hit = null,
        Critical = null,
        PowerModifier = null,
        LevelModifier = null,
        RawRecord = [],
        SupplementalRecord = []
    };
}
