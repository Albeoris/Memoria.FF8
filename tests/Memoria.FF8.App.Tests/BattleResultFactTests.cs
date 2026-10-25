using System.Text.Json;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

// Explicit component facts exercise persistent effect/reward application, not an invented research journal or original playthrough.
public sealed class BattleResultFactTests
{
    [Fact]
    public void NoKillCompletionObservesTheSavedTotalWithoutFabricatingDefeatEvents()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        records[0] = records[0] with { KillCount = 100 };
        vitals = vitals with { Records = records };
        fact = fact with { Outcome = BattleOutcome.Escaped, EnemyDefeats = [], DefeatedEnemySlots = [] };
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy });
        session.SetPendingBattleEncounter(fact.Encounter);
        GameState before = session.State;
        using GameWorld world = new(session, TestResources.GameDirectory);

        world.ApplyBattleResult(fact.Encounter, fact);

        Assert.Empty(session.State.PendingAchievementFacts);
        BattleKillTotalObservedEvent observed = Assert.Single(session.State.PendingAchievementObservations);
        Assert.Equal(100, observed.NativeKillTotal);
        Assert.Equal(BattleOutcome.Escaped, observed.Outcome);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        Assert.Equal(observed, Assert.Single(restored.State.PendingAchievementObservations));
        session.Restore(before);
        world.ApplyBattleResult(fact.Encounter, fact);
        Assert.Equal(observed, Assert.Single(session.State.PendingAchievementObservations));
        _ = new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
    }

    [Fact]
    public void AchievementFactsPublishOnlyWithAcceptedResultAndRetainTheNativeCounterObservation()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        fact = fact with { EnemyDefeats = fact.EnemyDefeats.Select(defeat => defeat with { AllocationId = "enemy-" + defeat.Slot }).ToArray() };
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, TestResources.GameDirectory);
        _ = Project(fact, vitals, economy, fact.DefeatedEnemySlots.ToDictionary(slot => slot, _ => 1));
        Assert.Empty(session.State.PendingAchievementFacts);
        BattleResult rejected = fact with { NativeCrtRandomBefore = new(123), NativeCrtRandomAfter = new(456) };
        Assert.Throws<InvalidDataException>(() => world.ApplyBattleResult(fact.Encounter, rejected));
        Assert.Empty(session.State.PendingAchievementFacts);
        world.ApplyBattleResult(fact.Encounter, fact);
        Assert.Equal(fact.EnemyDefeats.Length, session.State.PendingAchievementFacts.Length);
        long total = session.State.CharacterVitals!.Records.Sum(record => (long)record.KillCount) + session.State.CharacterVitals.GuardianForces.Sum(gf => (long)gf.KillCount!.Value);
        Assert.All(session.State.PendingAchievementFacts, accepted => Assert.Equal(total, accepted.Facts.NativeKillTotal));
        Assert.All(session.State.PendingAchievementFacts, accepted => Assert.Equal(BattleOutcome.Victory, accepted.Facts.AcceptedOutcome));
        BattleKillTotalObservedEvent observation = Assert.Single(session.State.PendingAchievementObservations);
        Assert.Equal(total, observation.NativeKillTotal);
        Assert.Equal(BattleOutcome.Victory, observation.Outcome);
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
        Assert.Null(session.State.AchievementCursor);
    }

    [Fact]
    public void AcceptedLiveDefeatsAreNotPublishedAgainAtBattleCompletion()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        fact = fact with { EnemyDefeats = fact.EnemyDefeats.Select(defeat => defeat with { AllocationId = "enemy-" + defeat.Slot }).ToArray() };
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy });
        session.SetPendingBattleEncounter(fact.Encounter);
        session.AcceptBattleAchievementFacts(fact.EnemyDefeats);
        EnemyDefeatedEvent[] accepted = session.State.PendingAchievementFacts.ToArray();
        foreach (EnemyDefeatedEvent receipt in accepted)
        {
            session.AcknowledgeAchievementFact(receipt);
        }
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.ApplyBattleResult(fact.Encounter, fact);
        Assert.Empty(session.State.PendingAchievementFacts);
        Assert.Single(session.State.PendingAchievementObservations);
    }

    [Fact]
    public void SavedGlobalRandomReceiptRejectsAStaleOwnerAndPublishesOnceWithTheBattleResult()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        NativeGlobalRandomState before = new(0);
        NativeGlobalRandomState after = before.Advance(3);
        fact = fact with { NativeGlobalRandomChange = new(before, after, 3) };
        GameSession session = new(new GameState { Scene = fact.Encounter.SourceScene, FieldId = fact.Encounter.SourceFieldId, CharacterVitals = vitals, Economy = economy, GlobalRandom = before });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, TestResources.GameDirectory);
        string original = JsonSerializer.Serialize(session.State);
        NativeGlobalRandomState stale = new(1);
        Assert.Throws<InvalidDataException>(() => world.ApplyBattleResult(fact.Encounter, fact with { NativeGlobalRandomChange = new(stale, stale.Advance(3), 3) }));
        Assert.Equal(original, JsonSerializer.Serialize(session.State));
        world.ApplyBattleResult(fact.Encounter, fact);
        Assert.Equal(after, session.State.GlobalRandom);
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void NativeDeathCounterReceiptPublishesTheExactWordAndRejectsStaleStateAtomically()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        byte row = fact.Party[0].Row;
        ushort before = vitals.Records[row].DeathCount!.Value;
        ushort after = unchecked((ushort)(before + 1));
        fact = fact with { NativeDeathCountChanges = [new(row, before, after)] };
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, TestResources.GameDirectory);
        string initial = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidDataException>(() => world.ApplyBattleResult(fact.Encounter, fact with { NativeDeathCountChanges = [new(row, after, unchecked((ushort)(after + 1)))] }));
        Assert.Equal(initial, JsonSerializer.Serialize(session.State));

        world.ApplyBattleResult(fact.Encounter, fact);

        Assert.Equal(after, session.State.CharacterVitals!.Records[row].DeathCount);
    }

    [Fact]
    public void RepeatedSceneAllocationsAwardTheirOwnApWithoutDuplicatingOneAllocationReceipt()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        BattleEnemyDefeat original = fact.EnemyDefeats[0] with { AllocationId = "enemy-0" };
        BattleEnemyDefeat summoned = original with { AllocationId = "native-allocation-1" };
        fact = fact with { EnemyDefeats = [original, summoned], DefeatedEnemySlots = [original.Slot] };
        BattleResultOwners projected = Project(fact, vitals, economy, new Dictionary<int, int> { [original.Slot] = 5 });
        Assert.Equal((ushort)10, projected.Rewards.GuardianForceAbilityPoints[0]);
        Assert.Throws<InvalidDataException>(() => (fact with { EnemyDefeats = [original, original] }).Validate());
    }

    [Fact]
    public void DirectCardGrowthAndCrtEffectsShareOneAtomicNativeReceipt()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        byte row = fact.Party[0].Row;
        NativeCharacterVital character = vitals.Records[row];
        BattleNativeCharacterGrowth beforeGrowth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray());
        byte[] bonuses = character.StatBonuses.ToArray();
        bonuses[0] = (byte)Math.Min(255, bonuses[0] + 1);
        BattleNativeCharacterGrowth afterGrowth = new((ushort)Math.Min(9999, character.HitPointBonus + 10), bonuses.ToImmutableArray());
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        NativeCardInventory afterCards = cards.Acquire(4, out _);
        NativeCrtRandomState crt = NativeCrtRandomState.FromColdProcess();
        NativeCrtRandomState afterCrt = crt.Draw(out _);
        fact = fact with { NativeGrowthChanges = [new(row, beforeGrowth, afterGrowth)], NativeCardChange = new(cards, afterCards), NativeCrtRandomBefore = crt, NativeCrtRandomAfter = afterCrt, EnemyDefeats = fact.EnemyDefeats.Select(defeat => defeat with { NativeCommand = 29 }).ToArray() };
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy, CardInventory = cards, CrtRandom = crt });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.ApplyBattleResult(fact.Encounter, fact);
        Assert.Equal(afterGrowth.HitPointBonus, session.State.CharacterVitals!.Records[row].HitPointBonus);
        Assert.Equal(bonuses, session.State.CharacterVitals.Records[row].StatBonuses);
        Assert.Equal(1, session.State.CardInventory!.AvailableCount(4));
        Assert.Equal(afterCrt, session.State.CrtRandom);
        Assert.Equal(character.Experience, session.State.CharacterVitals.Records[row].Experience);
        Assert.All(world.LastBattleRewards!.CharacterExperience.Values, amount => Assert.Equal(0, amount));
        Assert.NotEmpty(world.LastBattleRewards.GuardianForceAbilityPoints);
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void StaleCrtOwnerRejectsTheWholeNativeSpecialReceipt()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        NativeCrtRandomState before = NativeCrtRandomState.FromColdProcess();
        fact = fact with { NativeCrtRandomBefore = before, NativeCrtRandomAfter = before.Draw(out _) };
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy, CrtRandom = new(5) });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, TestResources.GameDirectory);
        string original = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidDataException>(() => world.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(original, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void NativeScriptAcquisitionsCommitGfCardsAndTutorialsWithTheSameReceipt()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        fact = fact with { NativeScriptEffects = new() { AcquiredGuardianForces = [2], Cards = [4, 4], UnlockedTutorials = [127] } };
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy, CardInventory = cards });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.ApplyBattleResult(fact.Encounter, fact);
        Assert.Equal(1, session.State.CharacterVitals!.GuardianForces[2].Flags & 1);
        Assert.True((session.State.CardInventory!.Cards[4] & 0x7F) >= 2);
        Assert.Contains(127, world.Events.Scene.UnlockedTutorials);
        Assert.Contains(world.LastBattleRewards!.Cards, id => id == 4);
        PendingEntityName naming = Assert.Single(session.State.PendingEntityNames!);
        Assert.Equal(GameNames.GuardianForceSubject(2), naming.Subject);
        Assert.Equal("Ifrit", naming.DefaultName);
        Assert.Equal("battle:" + fact.Encounter.OccurrenceId, naming.Source);
        session.SetEntityNameDraft("Ифрит界");
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        Assert.Equal("Ифрит界", Assert.Single(restored.State.PendingEntityNames!).Draft);
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void NativeScriptItemRewardsShareTheAtomicOccurrenceReceipt()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        fact = fact with { ScriptedItems = [new BattleStock(178, 1)], NativeRewardItems = [new BattleStock(178, 1)], PersistentAiVariablesBefore = new uint[8], PersistentAiVariablesAfter = [0, 0, 0, 0, 2, 0, 0, 0] };
        Assert.Throws<InvalidDataException>(() => (fact with { NativeRewardItems = null }).Validate());
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy, BattleVariables = NativeBattleVariableState.CreateNewGame() });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, TestResources.GameDirectory);
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
        using GameWorld world = new(session, TestResources.GameDirectory);
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidDataException>(() => world.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Null(world.LastBattleRewards);
    }

    [Fact]
    public void FactualEffectsUseCurrentRewardRulesAndConsumeTheirOccurrenceExactlyOnce()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, int learningAbility) = Fixture();
        AssertLearningProgress(vitals, learningAbility, 0);
        BattleResultOwners firstRules = Project(fact, vitals, economy, fact.DefeatedEnemySlots.ToDictionary(slot => slot, _ => 1));
        BattleResultOwners changedRules = Project(fact, vitals, economy, fact.DefeatedEnemySlots.Select((slot, index) => (slot, points: index + 3)).ToDictionary(entry => entry.slot, entry => entry.points));
        AssertLearningProgress(firstRules.CharacterVitals, learningAbility, 2);
        AssertLearningProgress(changedRules.CharacterVitals, learningAbility, 7);
        AssertLearningProgress(vitals, learningAbility, 0);
        Assert.Equal(fact.Party[0].HitPointsAfter, firstRules.CharacterVitals.Records[0].HitPoints.Minimum);
        Assert.Equal(5, firstRules.CharacterVitals.Records[0].MagicInventory.Single(stock => stock.SpellId == 1).Count);
        Assert.Equal(fact.ItemConsumptions[0].CountBefore - 1, firstRules.Economy.Items.Single(stock => stock.ItemId == 1).Count);
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.ApplyBattleResult(fact.Encounter, fact);
        AssertLearningProgress(session.State.CharacterVitals!, learningAbility, 2);
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
        using GameWorld world = new(session, TestResources.GameDirectory);
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

    [Fact]
    public void PhysicalMagicReceiptPreservesAStockMovedIntoAnEarlierEmptySlot()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        NativeMagicStock[] before = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
        before[5] = new(1, 2);
        before[10] = new(2, 9);
        records[0] = records[0] with { MagicInventory = before };
        vitals = vitals with { Records = records };
        NativeMagicStock[] after = before.ToArray();
        after[0] = new(1, 5);
        after[5] = new(0, 0);
        fact = fact with { Party = [fact.Party[0] with { MagicInventoryAfter = after }] };

        BattleResultOwners projected = Project(fact, vitals, economy, fact.DefeatedEnemySlots.ToDictionary(slot => slot, _ => 1));

        Assert.Equal(after, projected.CharacterVitals.Records[0].MagicInventory);
        after[0] = new(1, 6);
        Assert.Throws<InvalidDataException>(() => Project(fact, vitals, economy, fact.DefeatedEnemySlots.ToDictionary(slot => slot, _ => 1)));
    }

    [Fact]
    public void ExhaustedMagicClearsItsPhysicalSlotAndAllReferencesInTheTwentyJunctions()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        byte[] junctions = new byte[20];
        junctions[0] = 1;
        junctions[19] = 1;
        records[0] = records[0] with { Junction = records[0].Junction with { MagicJunctions = junctions } };
        vitals = vitals with { Records = records };
        fact = fact with { Party = [fact.Party[0] with { MagicChanges = [new(1, 2, -2)] }] };

        BattleResultOwners projected = Project(fact, vitals, economy, fact.DefeatedEnemySlots.ToDictionary(slot => slot, _ => 1));

        Assert.Equal(new NativeMagicStock(0, 0), projected.CharacterVitals.Records[0].MagicInventory[0]);
        Assert.Equal(new byte[20], projected.CharacterVitals.Records[0].Junction.MagicJunctions);
    }

    [Fact]
    public void PhysicalItemReceiptPreservesDirectGainsBeforeTheSeparateRewardPolicy()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        NativeEconomy gained = economy.AddItem(178, 7, out _);
        NativeItemStock[] after = gained.Items.ToArray();
        BattleItemConsumption consumed = fact.ItemConsumptions.Single();
        int potion = Array.FindIndex(after, stock => stock.ItemId == consumed.ItemId);
        after[potion] = after[potion] with { Count = (byte)(after[potion].Count - consumed.Consumed) };
        fact = fact with { DebugVictory = true, RewardPolicy = BattleRewardPolicy.DebugNoRewards, DefeatedEnemySlots = [], EnemyDefeats = [], RewardEligibleGuardianForceIds = [], NativeItemInventoryAfter = after };

        BattleResultOwners projected = Project(fact, vitals, economy, new Dictionary<int, int>());

        Assert.Equal(after, projected.Economy.Items);
        NativeItemStock[] invalid = after.ToArray();
        invalid[potion] = invalid[potion] with { Count = (byte)(invalid[potion].Count - 1) };
        Assert.Throws<InvalidDataException>(() => Project(fact with { NativeItemInventoryAfter = invalid }, vitals, economy, new Dictionary<int, int>()));
    }

    [Fact]
    public void ScanHistoryPublishesOnlyWithItsMatchingCurrentOccurrenceAndBeforeBank()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        NativeScannedModels before = new(NativeInputConfiguration.OriginalInitHash, System.Collections.Immutable.ImmutableArray.Create<uint>(0, 0, 0, 0, 0));
        NativeScannedModels after = before.Add(159);
        fact = fact with { NativeScannedModelsBefore = before, NativeScannedModelsAfter = after };
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy, ScannedModels = before });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, TestResources.GameDirectory);

        world.ApplyBattleResult(fact.Encounter, fact);

        Assert.True(session.State.ScannedModels!.SameValue(after));
        GameSession stale = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy, ScannedModels = before.Add(3) });
        stale.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld staleWorld = new(stale, TestResources.GameDirectory);
        string unchanged = JsonSerializer.Serialize(stale.State);
        Assert.Throws<InvalidDataException>(() => staleWorld.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(unchanged, JsonSerializer.Serialize(stale.State));
    }

    [Fact]
    public void ScanHistoryHasAnAuthenticatedNewGameClearWithoutInferringOldState()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeScannedModels clear = assets.ReadNewGameScannedModels();
        Assert.All(clear.Words, word => Assert.Equal(0u, word));
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74 });
        using GameWorld world = new(session, TestResources.GameDirectory);
        Assert.Null(session.State.ScannedModels);
        Assert.Throws<NotSupportedException>(() => session.InitializeNewGameScannedModels(clear));
    }

    [Fact]
    public void KnownMagicPublishesOnlyWithItsMatchingCurrentOccurrenceAndBeforeBank()
    {
        (NativeCharacterVitals vitals, NativeEconomy economy, BattleResult fact, _) = Fixture();
        NativeKnownMagic before = new(NativeInputConfiguration.OriginalInitHash, System.Collections.Immutable.ImmutableArray.Create<uint>(0, 0));
        NativeKnownMagic after = before.Add(63);
        fact = fact with { NativeKnownMagicBefore = before, NativeKnownMagicAfter = after };
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy, KnownMagic = before });
        session.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld world = new(session, TestResources.GameDirectory);

        world.ApplyBattleResult(fact.Encounter, fact);

        Assert.True(session.State.KnownMagic!.SameValue(after));
        GameSession stale = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy, KnownMagic = before.Add(3) });
        stale.SetPendingBattleEncounter(fact.Encounter);
        using GameWorld staleWorld = new(stale, TestResources.GameDirectory);
        string unchanged = JsonSerializer.Serialize(stale.State);
        Assert.Throws<InvalidDataException>(() => staleWorld.ApplyBattleResult(fact.Encounter, fact));
        Assert.Equal(unchanged, JsonSerializer.Serialize(stale.State));
    }

    [Fact]
    public void KnownMagicHasAnAuthenticatedNewGameClearWithoutInferringOldState()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeKnownMagic clear = assets.ReadNewGameKnownMagic();
        Assert.All(clear.Words, word => Assert.Equal(0u, word));
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74 });
        using GameWorld world = new(session, TestResources.GameDirectory);
        Assert.Null(session.State.KnownMagic);
        Assert.Throws<NotSupportedException>(() => session.InitializeNewGameKnownMagic(clear));
    }

    private static void AssertLearningProgress(NativeCharacterVitals vitals, int ability, int expected)
    {
        Assert.Equal(expected, vitals.GuardianForceAbilities(0).Single(entry => entry.Ability == ability).Progress);
        Assert.Equal(expected, vitals.GuardianForces[0].Progression!.Abilities.Single(entry => entry.Ability == ability).EarnedPoints);
        int nativeIndex = Array.FindIndex(vitals.MenuDefinitions.Learning[0].ToArray(), rule => rule.Ability == ability);
        Assert.True(nativeIndex >= 0);
        Assert.Equal(expected, vitals.GuardianForces[0].AbilityProgress[nativeIndex]);
    }

    private static BattleResultOwners Project(BattleResult fact, NativeCharacterVitals vitals, NativeEconomy economy, IReadOnlyDictionary<int, int> points)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        using BattleAssets battles = new(assets.GameDirectory);
        Dictionary<int, BattleEnemyRewardDefinition> definitions = battles.LoadEncounter(fact.Encounter.Formation).Enemies.ToDictionary(enemy => enemy.Slot, enemy => battles.ReadEnemyRewards(enemy.ModelId));
        return BattleResultProjection.Apply(fact, vitals, economy, points, definitions, assets.ReadNativeBattleExperienceDefinition(), null, false);
    }

    private static (NativeCharacterVitals, NativeEconomy, BattleResult, int) Fixture()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
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
        return (vitals, economy, outcome, learning.Ability);
    }
}
