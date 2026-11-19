using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NewGamePlusRewardTests
{
    [Theory]
    [InlineData(1, 4, 0)]
    [InlineData(1, 5, 1)]
    [InlineData(3, 5, 2)]
    [InlineData(2, 5, 1)]
    [InlineData(101, 15, 152)]
    [InlineData(65535, 0, 0)]
    [InlineData(101, 10, 101)]
    [InlineData(int.MaxValue, 100, 65535)]
    public void ExactTenthsRoundOnceAndClampAtTheAwardBoundary(int subtotal, int tenths, int expected)
    {
        NewGamePlusRewardRules rules = new() { ExperienceTenths = tenths, AbilityPointTenths = tenths };
        NewGamePlusRewardRules restored = JsonSerializer.Deserialize<NewGamePlusRewardRules>(JsonSerializer.Serialize(rules))!;
        Assert.Equal((ushort)expected, restored.ScaleExperience(subtotal));
        Assert.Equal((ushort)expected, restored.ScaleAbilityPoints(subtotal));
    }

    [Theory]
    [InlineData(1, 4, 0)]
    [InlineData(1, 5, 1)]
    [InlineData(3, 5, 2)]
    [InlineData(101, 15, 152)]
    [InlineData(101, 0, 0)]
    [InlineData(101, 10, 101)]
    public void EscapeCharacterAndGuardianExperienceUseIndependentNativeSubtotals(int subtotal, int tenths, int expected)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        BattleEnemyRewardDefinition definition = Definition(0, 100);
        // Level 1 against level 1 yields 400 EXP before the existing partial-escape fraction.
        BattleExperienceAwards awards = NativeBattleRewardDistribution.Experience([], vitals, new Dictionary<int, BattleEnemyRewardDefinition> { [0] = definition }, false,
            [new(0, 1, 400, 400 - subtotal, null)], new GameplayTuning { ExperienceMultiplier = 100, GuardianExperienceMultiplier = 0 }, newGamePlusRewards: new() { ExperienceTenths = tenths });
        Assert.All(awards.Characters.Values, award => Assert.Equal((ushort)expected, award));
        Assert.All(awards.GuardianForces.Values, award => Assert.Equal((ushort)expected, award));
        Assert.Equal(3, awards.Characters.Count);
        Assert.Equal(3, awards.GuardianForces.Count);
    }

    [Fact]
    public void TwoEnemyFractionsAggregateBeforeRoundingAndLargeAwardsDoNotWrap()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        Dictionary<int, BattleEnemyRewardDefinition> definitions = new() { [0] = Definition(0, 1), [1] = Definition(0, 1) };
        BattleExperienceAwards awards = NativeBattleRewardDistribution.Experience([], vitals, definitions, false, [new(0, 1, 4, 3, null), new(1, 1, 4, 3, null)], newGamePlusRewards: new() { ExperienceTenths = 5 });
        Assert.All(awards.Characters.Values, award => Assert.Equal((ushort)1, award));
        Assert.All(awards.GuardianForces.Values, award => Assert.Equal((ushort)1, award));

        definitions[0] = Definition(15000, 15000);
        definitions[1] = definitions[0];
        BattleEnemyDefeat[] defeats = [new(0, 1, BattleDefeatCredit.Character, 0, null, false, -1), new(1, 1, BattleDefeatCredit.GuardianForce, 1, 1, false, -1)];
        awards = NativeBattleRewardDistribution.Experience(defeats, vitals, definitions, false, newGamePlusRewards: new() { ExperienceTenths = 5 });
        Assert.Equal(ushort.MaxValue, awards.Characters[0]);
        Assert.Equal((ushort)60000, awards.Characters[1]);
        Assert.Equal(ushort.MaxValue, awards.GuardianForces[1]);
        Assert.Equal((ushort)60000, awards.GuardianForces[0]);
    }

    [Fact]
    public void LegacyMultipliersAboveTenKeepTheirOriginalSemantics()
    {
        BattleExperienceAwards awards = NativeBattleRewardDistribution.Experience([], MagicSystemFixture.Create(), new Dictionary<int, BattleEnemyRewardDefinition> { [0] = Definition(0, 1) }, false,
            [new(0, 1, 4, 3, null)], new GameplayTuning { ExperienceMultiplier = 16, GuardianExperienceMultiplier = 20 });
        Assert.All(awards.Characters.Values, award => Assert.Equal((ushort)16, award));
        Assert.All(awards.GuardianForces.Values, award => Assert.Equal((ushort)20, award));
    }

    [Theory]
    [InlineData(1, 4, 0)]
    [InlineData(1, 5, 1)]
    [InlineData(3, 5, 2)]
    [InlineData(101, 15, 152)]
    [InlineData(101, 0, 0)]
    [InlineData(101, 10, 101)]
    [InlineData(255, 100, 2550)]
    public void ProjectionDisplaysAndCommitsTheSameResolvedAp(int subtotal, int tenths, int expected)
    {
        NativeCharacterVitals vitals = LearningVitals();
        BattleResult result = Result(vitals, 1) with { Tuning = new() { ApMultiplier = 100 } };
        BattleResultOwners preview = Project(result, vitals, subtotal, new() { AbilityPointTenths = tenths });
        Assert.Equal((ushort)expected, preview.Rewards.GuardianForceAbilityPoints[0]);
        Assert.Equal((ushort)expected, preview.Rewards.AbilityPointsNotice);
        NativeGuardianForceAbility learned = preview.CharacterVitals.GuardianForceAbilities(0).Single(ability => ability.Ability == 100);
        Assert.Equal(expected >= 255, learned.Learned);
        if (expected < 255)
        {
            Assert.Equal(expected, learned.Progress);
        }
        Assert.Equal(0, vitals.GuardianForceAbilities(0).Single(ability => ability.Ability == 100).Progress);
        Assert.Equal(JsonSerializer.Serialize(preview), JsonSerializer.Serialize(Project(result, vitals, subtotal, new() { AbilityPointTenths = tenths })));
    }

    [Fact]
    public void ApAggregatesBeforeRoundingAndRetainsAutomaticAndDebugPolicies()
    {
        NativeCharacterVitals vitals = LearningVitals();
        BattleResult result = Result(vitals, 2);
        Assert.Equal((ushort)1, Project(result, vitals, 1, new() { AbilityPointTenths = 5 }).Rewards.AbilityPointsNotice);
        BattleResult automatic = result with
        {
            Encounter = result.Encounter with { ScriptedOwner = new(1, 0, 0, 0, 0, vitals.KernelSha256) },
            RewardPolicy = BattleRewardPolicy.ResearchAutomaticApOnly,
            EnemyDefeats = []
        };
        BattleResultOwners automaticRewards = Project(automatic, vitals, 1, new() { AbilityPointTenths = 5 });
        Assert.Empty(automaticRewards.Rewards.CharacterExperience);
        Assert.Equal((ushort)1, automaticRewards.Rewards.AbilityPointsNotice);
        BattleResult debug = result with { DebugVictory = true, RewardPolicy = BattleRewardPolicy.DebugNoRewards, EnemyDefeats = [], RewardEligibleGuardianForceIds = [] };
        BattleResultOwners debugRewards = Project(debug, vitals, 255, new() { AbilityPointTenths = 100 });
        Assert.Empty(debugRewards.Rewards.GuardianForceAbilityPoints);
        Assert.Empty(debugRewards.Rewards.CharacterExperience);
    }

    [Fact]
    public void RulesRejectUnsupportedVersionsAndInvalidChoices()
    {
        Assert.Throws<InvalidDataException>(() => (new NewGamePlusRewardRules { SchemaVersion = 2 }).Validate());
        Assert.Throws<InvalidDataException>(() => (new NewGamePlusRewardRules { ExperienceTenths = -1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (new NewGamePlusRewardRules { AbilityPointTenths = 101 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new NewGamePlusRewardRules().ScaleExperience(-1));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NewGamePlusRewardRules>("{}"));
    }

    private static NativeCharacterVitals LearningVitals()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals = vitals with { MenuDefinitions = vitals.MenuDefinitions with { Learning = vitals.MenuDefinitions.Learning.SetItem(0, vitals.MenuDefinitions.Learning[0].SetItem(0, new(1, 255, 100))) } };
        return vitals.ApplyGuardianForceTemplates(vitals.ResolveGuardianForceTemplates().SetItem(0, [new(100, false, 255)]));
    }

    private static BattleEnemyRewardDefinition Definition(ushort personal, ushort shared)
    {
        return new(personal, shared, 20, 30, 255, false, 0, Enumerable.Repeat(new BattleStock(0, 0), 12).ToImmutableArray());
    }

    private static BattleResult Result(NativeCharacterVitals vitals, int enemyCount)
    {
        BattleEncounterOccurrence encounter = new("reward-policy", 1, vitals.KernelSha256, SceneKind.Field, 1, 0, vitals.KernelSha256, null);
        BattlePartyResult[] party = vitals.CombatParty.Select(row => new BattlePartyResult(row, vitals.Records[row].CharacterId, 10, 10, 0, 0, [])).ToArray();
        int[] slots = Enumerable.Range(0, enemyCount).ToArray();
        return new(encounter, BattleOutcome.Victory, party, [], slots, BattleResultProjection.EligibleGuardianForces(vitals), false, BattleRewardPolicy.NativeCombat,
            slots.Select(slot => new BattleEnemyDefeat(slot, 1, BattleDefeatCredit.Unattributed, null, null, false, -1)).ToArray()) { EnemySlotCount = enemyCount };
    }

    private static BattleResultOwners Project(BattleResult result, NativeCharacterVitals vitals, int points, NewGamePlusRewardRules rules)
    {
        NativeEconomy economy = new()
        {
            Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = vitals.InitOutSha256,
            Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0
        };
        return BattleResultProjection.Apply(result, vitals, economy, Enumerable.Range(0, result.EnemySlotCount).ToDictionary(slot => slot, _ => points),
            Enumerable.Range(0, result.EnemySlotCount).ToDictionary(slot => slot, _ => Definition(0, 0)), new(vitals.KernelSha256, ImmutableArray.CreateRange(new uint[20])), null, false, newGamePlusRewards: rules);
    }
}
