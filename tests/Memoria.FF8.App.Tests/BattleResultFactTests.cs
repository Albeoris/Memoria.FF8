using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

// Explicit component facts exercise persistent effect/reward application, not an invented research journal or original playthrough.
public sealed class BattleResultFactTests
{
    [Fact]
    public void NativeScriptAcquisitionsCommitGfCardsAndTutorialsWithTheSameReceipt()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        fact = fact with { NativeScriptEffects = new() { AcquiredGuardianForces = [2], Cards = [4, 4], UnlockedTutorials = [127] } };
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy, CardInventory = cards });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        world.ApplyBattleResult(fact.Encounter, fact);
        Assert.Equal(1, session.State.CharacterVitals!.GuardianForces[2].Flags & 1);
        Assert.True((session.State.CardInventory!.Cards[4] & 0x7F) >= 2);
        Assert.Contains(127, world.Events.Scene.UnlockedTutorials);
        Assert.Contains(world.LastBattleRewards!.Cards, id => id == 4);
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void NativeScriptItemRewardsShareTheAtomicOccurrenceReceipt()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        fact = fact with { ScriptedItems = [new BattleStock(178, 1)], PersistentAiVariablesBefore = new uint[8], PersistentAiVariablesAfter = [0, 0, 0, 0, 2, 0, 0, 0] };
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy, BattleVariables = NativeBattleVariableState.CreateNewGame() });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        int before = economy.Items.FirstOrDefault(item => item.ItemId == 178)?.Count ?? 0;
        world.ApplyBattleResult(fact.Encounter, fact);
        Assert.Equal(before + 1, session.State.Economy!.Items.Single(item => item.ItemId == 178).Count);
        Assert.Contains(world.LastBattleRewards!.Items, item => item.Id == 178 && item.Count == 1);
        Assert.Equal(2u, session.State.BattleVariables!.Values[4]);
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
        Assert.Throws<InvalidDataException>(() => (fact with { RewardPolicy = BattleRewardPolicy.DebugNoRewards, DebugVictory = true, EnemyDefeats = [], RewardEligibleGuardianForceIds = [] }).Validate());
    }

    [Fact]
    public void MismatchedPersistentAiVariableOwnerRejectsAllRewardsAndEffects()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        fact = fact with { PersistentAiVariablesBefore = [1, 0, 0, 0, 0, 0, 0, 0], PersistentAiVariablesAfter = new uint[8] };
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy, BattleVariables = NativeBattleVariableState.CreateNewGame() });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidDataException>(() => world.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Null(world.LastBattleRewards);
    }

    [Fact]
    public void FactualEffectsUseCurrentRewardRulesAndConsumeTheirOccurrenceExactlyOnce()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, int learningIndex) = Fixture();
        BattleResultOwners firstRules = Project(fact, vitals, economy, fact.DefeatedEnemySlots.ToDictionary(slot => slot, _ => 1));
        BattleResultOwners changedRules = Project(fact, vitals, economy, fact.DefeatedEnemySlots.Select((slot, index) => (slot, points: index + 3)).ToDictionary(entry => entry.slot, entry => entry.points));
        Assert.Equal(2, firstRules.CharacterVitals.GuardianForces[0].AbilityProgress[learningIndex]);
        Assert.Equal(7, changedRules.CharacterVitals.GuardianForces[0].AbilityProgress[learningIndex]);
        Assert.Equal(fact.Party[0].HitPointsAfter, firstRules.CharacterVitals.Records[0].HitPoints.Minimum);
        Assert.Equal(5, firstRules.CharacterVitals.Records[0].MagicInventory.Single(stock => stock.SpellId == 1).Count);
        Assert.Equal(fact.ItemConsumptions[0].CountBefore - 1, firstRules.Economy.Items.Single(stock => stock.ItemId == 1).Count);
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        world.ApplyBattleResult(fact.Encounter, fact);
        Assert.Equal(2, session.State.CharacterVitals!.GuardianForces[0].AbilityProgress[learningIndex]);
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Equal(fact.Encounter.OccurrenceId, Assert.Single(session.State.AppliedBattleResultIds));
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
        Assert.Null(world.Battle);
    }

    [Fact]
    public void ResourceAndScriptedOwnerMismatchRejectBeforePublishingAnyPersistentEffect()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy });
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        BattleEncounterOccurrence changedResource = fact.Encounter with { FormationHash = new string('F', 64) };
        session.SetPendingBattleEncounter(changedResource);
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidDataException>(() => world.ApplyBattleResult(changedResource, fact with { Encounter = changedResource }));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        BattleEncounterOccurrence wrongOwner = fact.Encounter with { ScriptedOwner = new(74, 1, 2, 3, 0, new string('A', 64)) };
        session.SetPendingBattleEncounter(wrongOwner);
        before = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidDataException>(() => world.ApplyBattleResult(wrongOwner, fact with { Encounter = wrongOwner }));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Empty(session.State.AppliedBattleResultIds);
    }

    [Fact]
    public void ContradictoryEffectOperandOrRewardEligibilityLeavesCanonicalOwnersUnchanged()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        string beforeVitals = JsonSerializer.Serialize(vitals);
        string beforeEconomy = JsonSerializer.Serialize(economy);
        BattlePartyResult[] party = fact.Party.ToArray();
        party[0] = party[0] with { HitPointsBefore = (ushort)(party[0].HitPointsBefore - 1) };
        Assert.Throws<InvalidDataException>(() => Project(fact with { Party = party }, vitals, economy, fact.DefeatedEnemySlots.ToDictionary(slot => slot, _ => 1)));
        Assert.Throws<InvalidDataException>(() => Project(fact with { RewardEligibleGuardianForceIds = [] }, vitals, economy, fact.DefeatedEnemySlots.ToDictionary(slot => slot, _ => 1)));
        Assert.Equal(beforeVitals, JsonSerializer.Serialize(vitals));
        Assert.Equal(beforeEconomy, JsonSerializer.Serialize(economy));
    }

    private static BattleResultOwners Project(BattleResult fact, NativeCharacterVitals vitals, NativeEconomy economy, IReadOnlyDictionary<int, int> points)
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets battles = new(assets.GameDirectory);
        Dictionary<int, BattleEnemyRewardDefinition> definitions = battles.LoadEncounter(fact.Encounter.Formation).Enemies.ToDictionary(enemy => enemy.Slot, enemy => battles.ReadEnemyRewards(enemy.ModelId));
        return BattleResultProjection.Apply(fact, vitals, economy, points, definitions, assets.ReadNativeBattleExperienceDefinition(), null, false);
    }

    private static (NativeCharacterVitals, NativeEconomy, BattleResult, int) Fixture()
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets battles = new(assets.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        NativeGuardianForceAbility learning = vitals.GuardianForceAbilities(0).First(ability => !ability.Learned && ability.RequiredPoints >= 20);
        vitals = vitals.SelectGuardianForceLearning(0, learning.Ability);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        NativeMagicStock[] magic = records[0].MagicInventory.ToArray();
        magic[0] = new NativeMagicStock(1, 2);
        records[0] = records[0] with { MagicInventory = magic };
        vitals = vitals with { Records = records };
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeItemStock potion = economy.Items.Single(stock => stock.ItemId == 1);
        BattleEncounterOccurrence occurrence = new("component-encounter-0", 512, battles.FormationSourceHash(512), SceneKind.Field, 74, 0, new string('A', 64), null);
        BattleResult outcome = new(occurrence, BattleOutcome.Victory, [new(0, records[0].CharacterId, records[0].HitPoints.Minimum, (ushort)(records[0].HitPoints.Minimum - 10), records[0].Status, records[0].Status, [new(1, 2, 3)])], [new(1, potion.Count, 1)], battles.LoadEncounter(512).Enemies.Select(enemy => enemy.Slot).ToArray(), [0], false, BattleRewardPolicy.NativeCombat, battles.LoadEncounter(512).Enemies.Select(enemy => new BattleEnemyDefeat(enemy.Slot, 8, BattleDefeatCredit.Character, 0, null, false, -1)).ToArray());
        return (vitals, economy, outcome, learning.LearningIndex);
    }
}
