using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleComponentEscapeResultTests
{
    [Theory]
    [InlineData(100, 0)]
    [InlineData(99, 1)]
    [InlineData(50, 40)]
    [InlineData(0, 80)]
    public void RemainingHpScalesSharedExperienceBeforeItsMinimumAndGfSplit(int currentHp, int expected)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = Vitals(assets).AcquireGuardianForce(1).ToggleGuardianForceJunction(0, 1).RebuildParty().Rest();
        byte level = Level(vitals);
        BattleEnemyRemainingExperience[] remaining = [new(0, level, 100, currentHp, 3)];
        Dictionary<int, BattleEnemyRewardDefinition> definitions = new() { [0] = Definition() };

        BattleExperienceAwards awards = NativeBattleRewardDistribution.Experience([], vitals, definitions, false, remaining);

        Assert.Equal(expected, awards.Characters[0]);
        Assert.Equal(expected / 2, awards.GuardianForces[0]);
        Assert.Equal(expected / 2, awards.GuardianForces[1]);
        BattleExperienceAwards disabled = NativeBattleRewardDistribution.Experience([], vitals, definitions, true, remaining);
        Assert.Empty(disabled.Characters);
        Assert.Empty(disabled.GuardianForces);
        BattleExperienceAwards zeroBasis = NativeBattleRewardDistribution.Experience([], vitals, new Dictionary<int, BattleEnemyRewardDefinition> { [0] = Definition() with { SharedExperience = 0 } }, false, remaining);
        Assert.Equal((ushort)0, zeroBasis.Characters[0]);
    }

    [Fact]
    public void EscapePreservesPartyConsumptionAndAccruedDefeatRewardsAlongsideRemainingEnemyExperience()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = Vitals(assets);
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeCharacterVital player = vitals.Records[0];
        byte potionCount = economy.Items.Single(item => item.ItemId == 1).Count;
        BattleEncounterOccurrence occurrence = new("escape-result-component", 512, new string('A', 64), SceneKind.Field, 74, 0, new string('B', 64), null);
        BattleResult result = new(occurrence, BattleOutcome.Escaped, [new(0, player.CharacterId, player.HitPoints.Minimum, (ushort)(player.HitPoints.Minimum - 10), player.Status, player.Status, [])], [new(1, potionCount, 1)], [0], [0], false, BattleRewardPolicy.NativeCombat, [new(0, Level(vitals), BattleDefeatCredit.Character, 0, null, false, 0)])
        {
            RemainingEnemyExperience = [new(1, Level(vitals), 100, 50, 4)]
        };
        string beforeVitals = JsonSerializer.Serialize(vitals);
        string beforeEconomy = JsonSerializer.Serialize(economy);
        result = JsonSerializer.Deserialize<BattleResult>(JsonSerializer.Serialize(result))!;
        result.Validate();
        Dictionary<int, BattleEnemyRewardDefinition> definitions = new() { [0] = Definition(), [1] = Definition() };

        BattleResultOwners projected = BattleResultProjection.Apply(result, vitals, economy, new Dictionary<int, int> { [0] = 7 }, definitions, assets.ReadNativeBattleExperienceDefinition(), null, false);

        Assert.Equal((ushort)160, projected.Rewards.CharacterExperience[0]);
        Assert.Equal((ushort)120, projected.Rewards.GuardianForceExperience[0]);
        Assert.Equal((ushort)7, projected.Rewards.GuardianForceAbilityPoints[0]);
        Assert.Equal(new BattleStock(1, 2), Assert.Single(projected.Rewards.Items));
        Assert.Equal(potionCount + 1, projected.Economy.Items.Single(item => item.ItemId == 1).Count);
        Assert.Equal(player.HitPoints.Minimum - 10, projected.CharacterVitals.Records[0].HitPoints.Minimum);
        Assert.Equal(beforeVitals, JsonSerializer.Serialize(vitals));
        Assert.Equal(beforeEconomy, JsonSerializer.Serialize(economy));
        Assert.Throws<InvalidDataException>(() => (result with { Outcome = BattleOutcome.Victory }).Validate());
        Assert.Throws<InvalidDataException>(() => (result with { RemainingEnemyExperience = [result.RemainingEnemyExperience[0], result.RemainingEnemyExperience[0]] }).Validate());
        BattleResult copy = result.DeepCopy();
        copy.RemainingEnemyExperience[0] = copy.RemainingEnemyExperience[0] with { CurrentHp = 25 };
        Assert.Equal(50, result.RemainingEnemyExperience[0].CurrentHp);
    }

    private static NativeCharacterVitals Vitals(GameAssets assets)
    {
        return assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
    }

    private static byte Level(NativeCharacterVitals vitals)
    {
        return NativeStatDefinitions.Level(vitals.Records[0].Experience, vitals.Definitions.Characters[vitals.Records[0].CharacterId]);
    }

    private static BattleEnemyRewardDefinition Definition()
    {
        return new(10, 20, 20, 30, 255, false, 255, Enumerable.Repeat(new BattleStock(1, 2), 12).ToImmutableArray());
    }
}
