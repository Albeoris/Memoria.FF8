using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

// Explicit reward components use installed initial owners; they do not invent a playthrough journal.
public sealed class NativeBattleRewardDistributionTests
{
    [Fact]
    public void NativeSharedAndKillerExperienceUseActualLevelsAndGfSharesWhileApRemainsSeparate()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).AcquireGuardianForce(1).ToggleGuardianForceJunction(0, 0).ToggleGuardianForceJunction(0, 1).RebuildParty().Rest();
        byte level = NativeStatDefinitions.Level(vitals.Records[0].Experience, vitals.Definitions.Characters[vitals.Records[0].CharacterId]);
        Dictionary<int, BattleEnemyRewardDefinition> definitions = new() { [0] = Definition() };
        BattleExperienceAwards normal = NativeBattleRewardDistribution.Experience([new(0, level, BattleDefeatCredit.Character, 0, null, false, -1)], vitals, definitions, false);
        Assert.Equal((ushort)120, normal.Characters[0]);
        Assert.Equal((ushort)40, normal.GuardianForces[0]);
        Assert.Equal((ushort)40, normal.GuardianForces[1]);
        BattleExperienceAwards gf = NativeBattleRewardDistribution.Experience([new(0, level, BattleDefeatCredit.GuardianForce, 0, 0, false, -1)], vitals, definitions, false);
        int gfLevel = NativeStatDefinitions.Level(vitals.GuardianForces[0].Experience, vitals.Definitions.GuardianForces[0]);
        Assert.Equal((ushort)80, gf.Characters[0]);
        Assert.Equal(40 + 5 * 10 * level / gfLevel - 10, gf.GuardianForces[0]);
        Assert.Equal((ushort)40, gf.GuardianForces[1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(245)]
    [InlineData(246)]
    public void AutomaticAndInternalCommandsRetainSharedExperienceWithoutPersonalKillerExperience(byte command)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        byte level = NativeStatDefinitions.Level(vitals.Records[0].Experience, vitals.Definitions.Characters[vitals.Records[0].CharacterId]);
        BattleEnemyDefeat defeat = new(0, level, BattleDefeatCredit.Character, 0, null, false, -1) { NativeCommand = command };
        BattleExperienceAwards awards = NativeBattleRewardDistribution.Experience([defeat], vitals, new Dictionary<int, BattleEnemyRewardDefinition> { [0] = Definition() }, false);
        Assert.Equal((ushort)80, awards.Characters[0]);
        Assert.Equal((ushort)80, awards.GuardianForces[0]);
        BattleExperienceAwards normal = NativeBattleRewardDistribution.Experience([defeat with { NativeCommand = 1 }], vitals, new Dictionary<int, BattleEnemyRewardDefinition> { [0] = Definition() }, false);
        Assert.Equal((ushort)120, normal.Characters[0]);
    }

    [Fact]
    public void CharacterAndGfExperienceHaveDifferentKoPetrifyRulesAndNoExperienceClearsBoth()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        byte level = NativeStatDefinitions.Level(vitals.Records[0].Experience, vitals.Definitions.Characters[vitals.Records[0].CharacterId]);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        records[0] = records[0] with { Status = 1, HitPoints = new(0, 0) };
        vitals = vitals with { Records = records };
        BattleEnemyDefeat[] defeats = [new(0, level, BattleDefeatCredit.Unattributed, null, null, false, -1)];
        Dictionary<int, BattleEnemyRewardDefinition> definitions = new() { [0] = Definition() };
        BattleExperienceAwards ko = NativeBattleRewardDistribution.Experience(defeats, vitals, definitions, false);
        Assert.Empty(ko.Characters);
        Assert.Equal((ushort)80, ko.GuardianForces[0]);
        records[0] = records[0] with { Status = 5 };
        BattleExperienceAwards both = NativeBattleRewardDistribution.Experience(defeats, vitals with { Records = records }, definitions, false);
        Assert.Empty(both.Characters);
        Assert.Empty(both.GuardianForces);
        BattleExperienceAwards disabled = NativeBattleRewardDistribution.Experience(defeats, vitals, definitions, true);
        Assert.Empty(disabled.Characters);
        Assert.Empty(disabled.GuardianForces);
    }

    [Fact]
    public void DefeatedGuardianForceLosesItsKillerExperienceAndStillCountsInTheSharedDivisor()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).AcquireGuardianForce(1).ToggleGuardianForceJunction(0, 0).ToggleGuardianForceJunction(0, 1).RebuildParty().Rest();
        byte level = NativeStatDefinitions.Level(vitals.Records[0].Experience, vitals.Definitions.Characters[vitals.Records[0].CharacterId]);
        NativeGuardianForceVital[] guardianForces = vitals.GuardianForces.ToArray();
        guardianForces[0] = guardianForces[0] with { HitPoints = 0 };
        vitals = vitals with { GuardianForces = guardianForces };
        uint beforeExperience = vitals.GuardianForces[0].Experience;
        BattleEnemyDefeat defeat = new(0, level, BattleDefeatCredit.GuardianForce, 0, 0, false, -1) { NativeCommand = 254 };

        BattleExperienceAwards awards = NativeBattleRewardDistribution.Experience([defeat], vitals, new Dictionary<int, BattleEnemyRewardDefinition> { [0] = Definition() }, false);

        Assert.Equal((ushort)80, awards.Characters[0]);
        Assert.False(awards.GuardianForces.ContainsKey(0));
        Assert.Equal((ushort)40, awards.GuardianForces[1]);
        Assert.Equal(new[] { 1 }, BattleResultProjection.EligibleGuardianForces(vitals));
        Assert.Equal(beforeExperience, vitals.GuardianForces[0].Experience);
    }

    [Fact]
    public void SelectedCurrentItemAndCardRewardsUseCanonicalCapsAndLeaveSourceOwnersUntouched()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeItemStock[] stocks = economy.Items.ToArray();
        int potion = Array.FindIndex(stocks, stock => stock.ItemId == 1);
        stocks[potion] = stocks[potion] with { Count = 98 };
        economy = economy with { Items = stocks };
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        cards.Cards[0] = 0x80 | 99;
        string beforeEconomy = JsonSerializer.Serialize(economy);
        string beforeCards = JsonSerializer.Serialize(cards);
        BattleEnemyDefeat[] defeats = [new(0, 8, BattleDefeatCredit.Character, 0, null, true, 0)];
        Dictionary<int, BattleEnemyRewardDefinition> definitions = new() { [0] = Definition() };
        BattleLootOwners rewards = NativeBattleRewardDistribution.Loot(defeats, definitions, economy, cards);
        Assert.Equal(100, rewards.Economy.Items[potion].Count);
        Assert.Equal(new BattleStock(1, 2), Assert.Single(rewards.ItemsGranted));
        Assert.Equal(100, rewards.Cards!.AvailableCount(0));
        Assert.Equal((byte)0, Assert.Single(rewards.CardsGranted));
        Assert.Equal(beforeEconomy, JsonSerializer.Serialize(economy));
        Assert.Equal(beforeCards, JsonSerializer.Serialize(cards));
        Assert.Throws<InvalidDataException>(() => NativeBattleRewardDistribution.Loot(defeats, definitions, economy, null));
        Assert.Equal(beforeEconomy, JsonSerializer.Serialize(economy));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 1)]
    [InlineData(100, 0)]
    [InlineData(100, 100)]
    public void CharacterAndGuardianMultipliersApplyIndependentlyWithExplicitRecipientCaps(int characterMultiplier, int guardianMultiplier)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        byte level = NativeStatDefinitions.Level(vitals.Records[0].Experience, vitals.Definitions.Characters[vitals.Records[0].CharacterId]);
        BattleEnemyDefeat[] defeats = [new(0, level, BattleDefeatCredit.Character, 0, null, false, -1)];
        Dictionary<int, BattleEnemyRewardDefinition> definitions = new() { [0] = Definition() with { KillExperience = 1000, SharedExperience = 1000 } };
        BattleExperienceAwards baseline = NativeBattleRewardDistribution.Experience(defeats, vitals, definitions, false);
        GameplayTuning tuning = new() { ExperienceMultiplier = characterMultiplier, GuardianExperienceMultiplier = guardianMultiplier };
        tuning = JsonSerializer.Deserialize<GameplayTuning>(JsonSerializer.Serialize(tuning))!;
        BattleExperienceAwards awards = NativeBattleRewardDistribution.Experience(defeats, vitals, definitions, false, tuning: tuning);
        Assert.Equal((ushort)Math.Min(ushort.MaxValue, (long)baseline.Characters[0] * characterMultiplier), awards.Characters[0]);
        Assert.Equal((ushort)Math.Min(ushort.MaxValue, (long)baseline.GuardianForces[0] * guardianMultiplier), awards.GuardianForces[0]);
        BattleExperienceAwards suppressed = NativeBattleRewardDistribution.Experience(defeats, vitals, definitions, true, tuning: tuning);
        Assert.Empty(suppressed.Characters);
        Assert.Empty(suppressed.GuardianForces);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void ApMultiplierUsesGeneralMultiplicationAndPreviewStaysPure(int multiplier)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        BattlePartyResult[] party = vitals.CombatParty.Where(row => row != 255).Select(row =>
        {
            NativeCharacterVital member = vitals.Records[row];
            return new BattlePartyResult(row, member.CharacterId, member.HitPoints.Minimum, member.HitPoints.Minimum, member.Status, member.Status, []);
        }).ToArray();
        byte level = NativeStatDefinitions.Level(vitals.Records[0].Experience, vitals.Definitions.Characters[vitals.Records[0].CharacterId]);
        BattleEncounterOccurrence encounter = new("numeric-reward", 0, new string('A', 64), SceneKind.Field, 0, 0, new string('B', 64), null);
        BattleResult result = new(encounter, BattleOutcome.Victory, party, [], [0], BattleResultProjection.EligibleGuardianForces(vitals), false, BattleRewardPolicy.NativeCombat, [new(0, level, BattleDefeatCredit.Character, 0, null, false, -1)]) { Tuning = new() { ApMultiplier = multiplier } };
        result = JsonSerializer.Deserialize<BattleResult>(JsonSerializer.Serialize(result))!;
        string before = JsonSerializer.Serialize(vitals);
        BattleResultOwners Project() => BattleResultProjection.Apply(result, vitals, assets.ReadNewGameEconomy(), new Dictionary<int, int> { [0] = 4 }, new Dictionary<int, BattleEnemyRewardDefinition> { [0] = Definition() }, assets.ReadNativeBattleExperienceDefinition(), null, false);
        BattleResultOwners projected = Project();
        Assert.Equal((ushort)(4 * multiplier), projected.Rewards.GuardianForceAbilityPoints[0]);
        Assert.Equal(JsonSerializer.Serialize(projected), JsonSerializer.Serialize(Project()));
        Assert.Equal(before, JsonSerializer.Serialize(vitals));
    }

    private static BattleEnemyRewardDefinition Definition()
    {
        return new BattleEnemyRewardDefinition(10, 20, 20, 30, 0, false, 255, Enumerable.Repeat(new BattleStock(1, 5), 12).ToImmutableArray());
    }

    [Fact]
    public void SharedNativeRewardBankMergesBeforeInventoryClampAndStopsAtZeroSentinel()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeEconomy economy = assets.ReadNewGameEconomy();
        economy.Items[0] = new NativeItemStock(1, 0);
        BattleLootOwners merged = NativeBattleRewardDistribution.Loot([], new Dictionary<int, BattleEnemyRewardDefinition>(), economy, null, [new BattleStock(1, 150), new BattleStock(1, 150)]);
        Assert.Equal(44, merged.Economy.Items[0].Count);
        Assert.Equal(new BattleStock(1, 44), Assert.Single(merged.ItemsGranted));
        BattleLootOwners sentinel = NativeBattleRewardDistribution.Loot([], new Dictionary<int, BattleEnemyRewardDefinition>(), economy, null, [new BattleStock(1, 2), new BattleStock(0, 1), new BattleStock(7, 2)]);
        Assert.Equal(2, sentinel.Economy.Items[0].Count);
        Assert.Equal(economy.Items[1], sentinel.Economy.Items[1]);
        Assert.Throws<InvalidDataException>(() => NativeBattleRewardDistribution.ValidateItemBank(Enumerable.Repeat(new BattleStock(1, 1), 25).ToArray()));
        BattleEnemyDefeat[] cards = Enumerable.Range(0, 9).Select(index => new BattleEnemyDefeat(0, 8, BattleDefeatCredit.Unattributed, null, null, true, -1) { AllocationId = $"reward-{index}" }).ToArray();
        BattleLootOwners cappedCards = NativeBattleRewardDistribution.Loot(cards, new Dictionary<int, BattleEnemyRewardDefinition> { [0] = Definition() }, economy, NativeCardInventory.CreateNewGame());
        Assert.Equal(8, cappedCards.CardsGranted.Length);
    }
}
