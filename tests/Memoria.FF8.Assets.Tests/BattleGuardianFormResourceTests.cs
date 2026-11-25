using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleGuardianFormResourceTests
{
    [Fact]
    public void InstalledIfritFormUsesTheCavernPrototypeDefensesAndRoundTripsThem()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using BattleAssets assets = new(directory!);
        BattleGuardianFormResources resources = assets.ReadIfritGuardianFormResources();
        BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.Find(resources.GuardianForceId)!;
        Assert.Equal(61, profile.ModelId);
        Assert.Equal(assets.ReadEnemyNativeStats(profile.ModelId).ElementDefenses.Select(value => (int)value), resources.ElementDefenses);
        Assert.Equal(new[] { 1000, 700, 800, 800, 800, 800, 800, 800 }, resources.ElementDefenses);
        Assert.Equal(assets.ReadEnemyNativeStatusData(profile.ModelId).Resistances, resources.StatusResistances);
        Assert.Equal((byte)255, resources.StatusResistances[1]);
        BattleGuardianFormResources restored = JsonSerializer.Deserialize<BattleGuardianFormResources>(JsonSerializer.Serialize(resources))!;
        Assert.Equal(resources.ElementDefenses, restored.ElementDefenses);
        Assert.Equal(resources.StatusResistances, restored.StatusResistances);
        Assert.NotNull(restored.AbilityBands);
        Assert.Equal(resources.AbilityBands!.Bands.SelectMany(band => band), restored.AbilityBands.Bands.SelectMany(band => band));
    }

    [Fact]
    public void InstalledIfritFormCopiesOffensiveAbilitiesFromEachBossLevelBand()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using BattleAssets assets = new(directory!);
        BattleGuardianFormAbilityBands abilities = assets.ReadIfritGuardianFormAbilityBands();
        Assert.Equal(20, abilities.MediumLevel);
        Assert.Equal(30, abilities.HighLevel);
        foreach (int level in new[] { 1, 19, 20, 29, 30, 100 })
        {
            NativeBattleAiDefinition boss = assets.ReadEnemyAi(61, level);
            var expected = new List<(BattleGuardianFormAction Action, ushort Id, byte Sequence)>();
            int start = boss.LevelBand * 16;
            NativeBattleAiAbility attack = boss.Abilities[start];
            NativeBattleAiAbility fire = boss.Abilities[start + 1];
            Assert.Equal((8, 79, 12), ((int)attack.Command, (int)attack.Id, (int)attack.Sequence));
            Assert.Equal((2, 1, 11), ((int)fire.Command, (int)fire.Id, (int)fire.Sequence));
            expected.Add((BattleGuardianFormAction.Attack, attack.Id, attack.Sequence));
            expected.Add((BattleGuardianFormAction.Fire, fire.Id, fire.Sequence));
            NativeBattleAiAbility heavy = boss.Abilities[start + 6];
            if (level < 20)
            {
                Assert.Equal((8, 59, 12, 20), ((int)heavy.Command, (int)heavy.Id, (int)heavy.Sequence, heavy.Power));
                expected.Add((BattleGuardianFormAction.HeavyStrike, heavy.Id, heavy.Sequence));
            }
            else
            {
                Assert.Equal((0, 0, 0), ((int)heavy.Command, (int)heavy.Id, (int)heavy.Sequence));
            }
            int band = NativeBattleEnemyLevelRules.SelectBand(level, abilities.MediumLevel, abilities.HighLevel);
            Assert.Equal(boss.LevelBand, band);
            Assert.Equal(expected, abilities.Bands[band].Where(action => !action.Terminal).Select(action => (action.Action, (ushort)action.ResourceActionId, action.Sequence)));
            Assert.Equal(BattleGuardianFormAction.HellFire, Assert.Single(abilities.Bands[band].Where(action => action.Terminal)).Action);
        }
    }
}
