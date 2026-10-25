using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ChocoboRewardTests
{
    [Fact]
    public void ClaimIsAtomicAndHistorySurvivesConsumptionAndSaveRollback()
    {
        GameState before = State();
        ChocoboRewardGrantResult grant = ChocoboRewardTransactions.ClaimRemaining(before);
        Assert.Equal(ChocoboRewardGrantStatus.Granted, grant.Status);
        Assert.Equal(4, grant.Delivered.Length);
        Assert.Empty(before.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.All(ChocoboRewardCatalog.Entries, reward => Assert.Single(grant.State.Economy!.Items, stock => stock.ItemId == reward.ItemId && stock.Count == 1));
        GameState consumed = grant.State with { Economy = Economy() };
        ChocoboRewardLedger restored = JsonSerializer.Deserialize<ChocoboRewardLedger>(JsonSerializer.Serialize(consumed.ChocoboRewards))!;
        Assert.Equal(ChocoboRewardGrantStatus.AlreadyAcquired, ChocoboRewardTransactions.ClaimRemaining(consumed with { ChocoboRewards = restored }).Status);
        Assert.Equal(ChocoboRewardGrantStatus.Granted, ChocoboRewardTransactions.ClaimRemaining(before).Status);
    }

    [Fact]
    public void FullInventoryLeavesInventoryAndLedgerUnchanged()
    {
        NativeItemStock[] items = Enumerable.Range(1, 198).Where(id => ChocoboRewardCatalog.FindItem(id) is null).Select(id => new NativeItemStock((byte)id, 1)).ToArray();
        Array.Resize(ref items, NativeEconomy.ItemSlotCount);
        for (int i = 194; i < items.Length; i++)
        {
            items[i] = new NativeItemStock((byte)(199 + i - 194), 1);
        }
        GameState source = State() with { Economy = Economy() with { Items = items } };
        ChocoboRewardGrantResult result = ChocoboRewardTransactions.ClaimRemaining(source);
        Assert.Equal(ChocoboRewardGrantStatus.InventoryFull, result.Status);
        Assert.Same(source, result.State);
        Assert.Empty(source.ChocoboRewards.AcquiredUniqueRewardIds);
    }

    [Fact]
    public void ExistingStockIsInferredAndDuplicateLegacyCopiesArePreserved()
    {
        NativeEconomy economy = Economy().AddItem(100, 3, out _);
        GameState state = ChocoboRewardTransactions.Reconcile(State() with { Economy = economy });
        Assert.Contains("ribbon", state.ChocoboRewards.AcquiredUniqueRewardIds);
        ChocoboRewardGrantResult result = ChocoboRewardTransactions.ClaimRemaining(state);
        Assert.Equal(3, result.Delivered.Length);
        Assert.Equal(3, result.State.Economy!.Items.Single(stock => stock.ItemId == 100).Count);
        Assert.Equal(0, result.State.PocketStation?.Raw.Length ?? 0);
    }

    [Fact]
    public void LegacyLearnedAbilitiesInferConsumedAmuletAndRibbon()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        NativeGuardianForceVital[] guardians = vitals.GuardianForces.ToArray();
        guardians[0] = guardians[0] with { LearnedBank = new(1UL << 38, 1UL << (77 - 64)), Progression = null };
        GameState migrated = ChocoboRewardTransactions.Reconcile(State() with { GameplayPreferences = new(), CharacterVitals = vitals with { GuardianForces = guardians } });
        Assert.Contains("ribbon", migrated.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.Contains("mogs_amulet", migrated.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.DoesNotContain("friendship", migrated.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.DoesNotContain("chocobos_tag", migrated.ChocoboRewards.AcquiredUniqueRewardIds);
    }

    [Fact]
    public void ExplicitModeDoesNotTreatAbilitiesLearnedWithoutItemsAsAcquisitions()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        NativeGuardianForceVital[] guardians = vitals.GuardianForces.ToArray();
        guardians[0] = guardians[0] with { LearnedBank = new(1UL << 38, 1UL << (77 - 64)), Progression = null };
        GameState state = State() with { CharacterVitals = vitals with { GuardianForces = guardians } };
        GameState reconciled = ChocoboRewardTransactions.Reconcile(state);
        Assert.Empty(reconciled.ChocoboRewards.AcquiredUniqueRewardIds);
        ChocoboRewardGrantResult claim = ChocoboRewardTransactions.ClaimRemaining(reconciled);
        Assert.Equal(ChocoboRewardGrantStatus.Granted, claim.Status);
        Assert.Contains(claim.Delivered, stock => stock.ItemId == 65);
        Assert.Contains(claim.Delivered, stock => stock.ItemId == 100);
    }

    [Theory]
    [InlineData(65, 38)]
    [InlineData(100, 77)]
    public void GrantedMedicinesTeachNativeAbilitiesAndRemainAcquiredAfterConsumption(int itemId, int ability)
    {
        GameState source = State() with { CharacterVitals = MagicSystemFixture.Create() };
        GameState granted = ChocoboRewardTransactions.ClaimRemaining(source).State;
        NativeFieldItem item = new(17, 5, (byte)ability, 0);
        var used = item.Use(granted.CharacterVitals!, granted.Economy!, itemId, 16);
        Assert.True(used.Changed);
        Assert.True(used.Vitals.GuardianForces[0].HasLearnedAbility(ability));
        Assert.DoesNotContain(used.Economy.Items, stock => stock.ItemId == itemId);
        GameState consumed = ChocoboRewardTransactions.Reconcile(granted with { CharacterVitals = used.Vitals, Economy = used.Economy });
        Assert.Equal(ChocoboRewardGrantStatus.AlreadyAcquired, ChocoboRewardTransactions.ClaimRemaining(consumed).Status);
    }

    [Fact]
    public void GrantedTagUsesNativeBokoNamingSubject()
    {
        GameState granted = ChocoboRewardTransactions.ClaimRemaining(State() with { CharacterVitals = MagicSystemFixture.Create() }).State;
        var naming = new NativeFieldItem(13, 1, 0, 0).BeginNaming(granted.CharacterVitals!, granted.Economy!, 165, 0);
        Assert.Equal(GameNames.BokoSubject, naming.Subject);
        Assert.DoesNotContain(naming.Economy.Items, stock => stock.ItemId == 165);
        Assert.Contains("chocobos_tag", granted.ChocoboRewards.AcquiredUniqueRewardIds);
    }

    [Fact]
    public void ModeAndBattleGateClaimsAndHardcorePreservesEnum()
    {
        GameState state = State();
        Assert.Equal(ChocoboRewardGrantStatus.WrongMode, ChocoboRewardTransactions.ClaimRemaining(state with { GameplayPreferences = new() { ChocoboWorldMode = ChocoboWorldMode.AngeloSearch } }).Status);
        Assert.Equal(ChocoboWorldMode.Memoria, new GameplayPreferences { Difficulty = BattleDifficulty.Hardcore, ChocoboWorldMode = ChocoboWorldMode.Memoria }.NormalizeForDifficulty().ChocoboWorldMode);
        Assert.DoesNotContain(GameplayPreferences.NewGameOptions, option => option.PropertyName == nameof(GameplayPreferences.ChocoboWorldMode));
    }

    [Fact]
    public void BattleCommitRetainsConsumedUniqueRewardAndOccurrenceRejectsReplayWhileOlderSaveRestoresAvailability()
    {
        BattleEncounterOccurrence encounter = new("chocobo-ledger-battle", 1, new string('a', 64), SceneKind.Field, 74, 0, new string('b', 64), null);
        GameSession session = new(State() with { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = MagicSystemFixture.Create(), PendingBattleEncounter = encounter });
        GameState earlier = session.State;
        ChocoboRewardLedger awarded = earlier.ChocoboRewards with { AcquiredUniqueRewardIds = earlier.ChocoboRewards.AcquiredUniqueRewardIds.Add("friendship") };
        // The battle has already consumed Friendship: only its acquisition receipt remains at result commit.
        session.CommitBattleResult(encounter, earlier.CharacterVitals!, earlier.Economy!, null, chocoboRewardsBefore: earlier.ChocoboRewards, chocoboRewardsAfter: awarded);
        Assert.Contains("friendship", session.State.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.DoesNotContain(session.State.Economy!.Items, stock => stock.ItemId == 32);
        Assert.Throws<InvalidDataException>(() => session.CommitBattleResult(encounter, earlier.CharacterVitals!, earlier.Economy!, null, chocoboRewardsBefore: earlier.ChocoboRewards, chocoboRewardsAfter: awarded));
        Assert.Contains(encounter.OccurrenceId, session.State.AppliedBattleResultIds);
        GameSession reloaded = new(session.State);
        Assert.Contains("friendship", reloaded.State.ChocoboRewards.AcquiredUniqueRewardIds);
        session.Restore(earlier);
        Assert.DoesNotContain("friendship", session.State.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.DoesNotContain(encounter.OccurrenceId, session.State.AppliedBattleResultIds);
    }

    [Theory]
    [InlineData(258)]
    [InlineData(259)]
    public void RemasteredRareRowIncludesFourRewardsWithoutRerolls(int modulus)
    {
        int[] expected = [32, 100, 65, 165];
        for (int category = 256; category < modulus; category++)
        {
            Assert.Equal(expected.Concat(expected).Concat(expected), Enumerable.Range(0, 12).Select(item => ChocoboAngeloSearch.Select((ushort)category, (ushort)item, modulus)));
        }
        Assert.Equal(1, ChocoboAngeloSearch.Select(0, 0, modulus));
        Assert.Equal(145, ChocoboAngeloSearch.Select(128, 0, modulus));
        Assert.Equal(19, ChocoboAngeloSearch.Select(160, 0, modulus));
        Assert.Equal(68, ChocoboAngeloSearch.Select(176, 0, modulus));
        Assert.Equal(49, ChocoboAngeloSearch.Select(192, 0, modulus));
        Assert.Equal(35, ChocoboAngeloSearch.Select(200, 0, modulus));
    }

    [Fact]
    public void AddedTagSharesRareSelectorMassEquallyWithoutChangingCategoryThresholds()
    {
        Dictionary<int, int> counts = Enumerable.Range(0, 32768).Select(roll => ChocoboAngeloSearch.Select(256, (ushort)roll, 258)).GroupBy(item => item).ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(4, counts.Count);
        Assert.All(counts, pair => Assert.Equal(8192, pair.Value));
        Assert.Equal(254, Enumerable.Range(0, 32768).Count(roll => ChocoboRewardCatalog.FindItem(ChocoboAngeloSearch.Select((ushort)roll, 3, 258)) is not null));
        Assert.Equal(378, Enumerable.Range(0, 32768).Count(roll => ChocoboRewardCatalog.FindItem(ChocoboAngeloSearch.Select((ushort)roll, 3, 259)) is not null));
    }

    private static GameState State() => new() { Economy = Economy(), GameplayPreferences = new() { ChocoboWorldMode = ChocoboWorldMode.Memoria } };

    private static NativeEconomy Economy() => new()
    {
        Version = 1,
        Rule = NativeEconomy.InitializationRule,
        InitOutSha256 = new string('0', 64),
        Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(),
        Gil = 0,
        PastGil = 0,
        EventBankGil72 = 0,
        EventBankPastGil76 = 0
    };
}
