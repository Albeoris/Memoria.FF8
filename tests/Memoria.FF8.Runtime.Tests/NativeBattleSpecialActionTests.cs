using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSpecialActionTests
{
    [Fact]
    public void CardAcquiresAtInvocationAndRetainsItsPreparedResultAcrossWindupRestore()
    {
        BattleSession battle = Create(17);
        battle.QueueNativePlayerCommand("party-0", "enemy", 29, 10);
        Assert.Equal(-1, battle.Capture().NativeCards!.AvailableCount(4));
        battle.Tick();
        Assert.Equal(1, battle.Capture().NativeCards!.AvailableCount(4));
        Assert.False(battle.ActiveAction!.NativeSpecial!.Failed);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Complete(battle);
        Complete(restored);
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(1, battle.Capture().NativeCards!.AvailableCount(4));
        Assert.Contains(battle.Events, item => item.Kind == "NativeText" && item.Message.Contains("Card4", StringComparison.Ordinal));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void AFullCardStockFailsConversionWithoutRemovingTheEnemy()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        for (int count = 0; count < 100; count++)
        {
            cards = cards.Acquire(4, out _);
        }
        BattleSession battle = Create(17, cards);
        battle.QueueNativePlayerCommand("party-0", "enemy", 29, 10);
        Complete(battle);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(100, battle.Capture().NativeCards!.AvailableCount(4));
        Assert.True(battle.Combatants.Single(actor => actor.Id == "enemy").IsAlive);
    }

    [Fact]
    public void DevourUsesOwnedGrowthBytesAndRollsOnceAtItsSynchronousFormula()
    {
        BattleSession battle = Create(19);
        NativeBattleRandomState before = battle.Capture().Random!;
        NativeBattleRandomState expected = before.Draw(out _);
        battle.QueueNativePlayerCommand("party-0", "enemy", 7, 9);
        Assert.Equal(before, battle.Capture().Random);
        battle.Tick();
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Complete(battle);
        Complete(restored);
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        BattleNativeCharacterGrowth growth = battle.Combatants[0].NativeGrowth!;
        Assert.Equal(9999, growth.HitPointBonus);
        Assert.Equal(new byte[] { 255, 2, 3, 4, 5, 6 }, growth.StatBonuses.ToArray());
        Assert.Contains(battle.Events, item => item.Kind == "NativeText" && item.Message == "Devour effect");
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void AngeloSearchAdvancesTwoSeparateRandomOwnersAndAddsTheFoundItemImmediately()
    {
        BattleSession battle = Create(23);
        NativeCrtRandomState expectedCrt = NativeCrtRandomState.FromColdProcess().Draw(out ushort category);
        Assert.Equal((ushort)41, category);
        NativeBattleRandomState expectedBattle = battle.Capture().Random!.Draw(out byte roll);
        int item = (roll & 7) + 1;
        battle.QueueNativePlayerCommand("party-0", "party-0", 240, 0);
        battle.Tick();
        Assert.Equal((byte)5, battle.ActiveAction!.IndependentGuardian!.Level);
        Complete(battle);
        Assert.Equal(expectedCrt, battle.Capture().NativeCrtRandom);
        Assert.Equal(expectedBattle.Indexes.ToArray(), battle.Capture().Random!.Indexes.ToArray());
        Assert.Equal(1, Assert.Single(battle.Items).Count);
        Assert.Equal(item, Assert.Single(battle.Items).Id);
    }

    [Theory]
    [InlineData(32, "friendship")]
    [InlineData(65, "mogs_amulet")]
    [InlineData(100, "ribbon")]
    [InlineData(165, "chocobos_tag")]
    public void ExtendedSearchRestoresLedgerAndDoesNotRerollAcquiredRewards(int expectedItem, string rewardId)
    {
        NativeCrtRandomState initial = Enumerable.Range(0, 100000).Select(seed => new NativeCrtRandomState((uint)seed)).First(seed =>
        {
            NativeCrtRandomState next = seed.Draw(out ushort category);
            next.Draw(out ushort item);
            return ChocoboAngeloSearch.Select(category, item, 258) == expectedItem;
        });
        NativeCrtRandomState expectedCrt = initial.Draw(out _).Draw(out _);
        BattleSession battle = Create(23, mode: ChocoboWorldMode.AngeloSearch, crt: initial);
        NativeBattleRandomState expectedBattle = battle.Capture().Random!;
        battle.QueueNativePlayerCommand("party-0", "party-0", 240, 0);
        battle.Tick();
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        BattleSession restored = BattleSession.Restore(snapshot);
        Complete(battle);
        Complete(restored);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(expectedCrt, battle.Capture().NativeCrtRandom);
        Assert.Equal(expectedBattle.Indexes.ToArray(), battle.Capture().Random!.Indexes.ToArray());
        Assert.Contains(rewardId, battle.Capture().ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.Equal(expectedItem, Assert.Single(battle.Items).Id);
        Assert.Equal(1, Assert.Single(battle.Items).Count);

        BattleSession already = Create(23, mode: ChocoboWorldMode.AngeloSearch, crt: initial, ledger: battle.Capture().ChocoboRewards);
        already.QueueNativePlayerCommand("party-0", "party-0", 240, 0);
        Complete(already);
        Assert.Empty(already.Items);
        Assert.Equal(expectedCrt, already.Capture().NativeCrtRandom);
        Assert.Contains(already.Events, item => item.Kind == "NativeText" && item.Message.Contains("already obtained", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(ChocoboWorldMode.NativeImport)]
    [InlineData(ChocoboWorldMode.AngeloSearch)]
    public void LegacyBattleMigrationAlignsInitialReceiptWithoutLosingCurrentAcquisitions(ChocoboWorldMode mode)
    {
        BattleSnapshot legacy = Create(23).Capture();
        NativeEconomy economy = new()
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
        economy = economy.AddItem(100, 1, out _);
        ChocoboImportReceipt receipt = new("world", 2, 1, 0, [1, 0, 0, 0], "batch");
        BattleEncounterOccurrence encounter = new("legacy-chocobo-ledger", 1, new string('a', 64), SceneKind.Field, 74, 0, new string('b', 64), null);
        GameState source = new()
        {
            Scene = SceneKind.Field,
            FieldId = 74,
            CharacterVitals = MagicSystemFixture.Create(),
            PendingBattleEncounter = encounter,
            Economy = economy,
            ChocoboRewards = new() { NativeImportSources = ImmutableDictionary<string, ChocoboImportReceipt>.Empty.Add(receipt.SourceId, receipt) },
            Battle = legacy with { Definition = legacy.Definition with { EncounterId = 1 }, ChocoboRewards = new() { AcquiredUniqueRewardIds = ImmutableHashSet.Create("friendship") } }
        };
        GameState migrated = ChocoboRewardTransactions.MigrateLegacyBattle(source, mode);
        Assert.Contains("ribbon", migrated.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.DoesNotContain("friendship", migrated.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.True(migrated.ChocoboRewards.AcquiredUniqueRewardIds.SetEquals(migrated.Battle!.Definition.ChocoboRewards.AcquiredUniqueRewardIds));
        Assert.Contains("friendship", migrated.Battle.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.Contains("ribbon", migrated.Battle.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(migrated.Battle.ChocoboRewards.NativeImportSources[receipt.SourceId]));
        Assert.Equal(mode, migrated.Battle.Definition.ChocoboWorldMode);
        Assert.Equal(JsonSerializer.Serialize(legacy.Random), JsonSerializer.Serialize(migrated.Battle.Random));
        Assert.Same(source.Economy, migrated.Economy);
        Assert.Empty(source.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.Same(migrated, ChocoboRewardTransactions.MigrateLegacyBattle(migrated, mode == ChocoboWorldMode.NativeImport ? ChocoboWorldMode.AngeloSearch : ChocoboWorldMode.NativeImport));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated.Battle))!);
        Assert.Contains("friendship", restored.Capture().ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.Equal(mode, restored.Capture().Definition.ChocoboWorldMode);
        GameSession session = new(migrated);
        session.CommitBattleResult(encounter, session.State.CharacterVitals!, session.State.Economy!, null, chocoboRewardsBefore: restored.Capture().Definition.ChocoboRewards, chocoboRewardsAfter: restored.Capture().ChocoboRewards);
        Assert.Contains("friendship", session.State.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(session.State.ChocoboRewards.NativeImportSources[receipt.SourceId]));
    }

    private static void Complete(BattleSession battle)
    {
        for (int frame = 0; frame < 40 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
        }
    }

    private static BattleSession Create(byte type, NativeCardInventory? cards = null, ChocoboWorldMode? mode = null, NativeCrtRandomState? crt = null, ChocoboRewardLedger? ledger = null)
    {
        byte command = type == 17 ? (byte)29 : type == 19 ? (byte)7 : (byte)240;
        ushort id = type == 17 ? (ushort)10 : type == 19 ? (ushort)9 : (ushort)0;
        byte flags = type == 23 ? (byte)0x10 : (byte)0x50;
        BattleNativeAction action = new(command, id, 10, id) { MagicId = 1, AttackType = type, Power = 0, HitCount = 1, TargetFlags = flags, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [1], SupplementalRecord = [] };
        string?[] messages = new string?[119];
        messages[9] = "Converted";
        messages[11] = " ";
        messages[16] = "card";
        messages[50] = "Card cannot be used";
        messages[64] = " found";
        messages[71] = "";
        messages[112] = "Cannot devour";
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        // Explicit component source records exercise the native algorithms without declaring an installed encounter.
        //48FE20 LABEL83 prepares240's level from acquired GF rows before dispatch, even for Search; this fixture owns one acquired level5 GF.
        BattleSession battle = new(new() { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, GuardianForces = type == 23 ? [new(0, 100, 100, true)] : [], ActionDefinitions = type == 23 ? [new(BattleAbility.GuardianForce, 0, "Explicit level5 GF", BattleActionEffect.Native, 0, false, Level: 5)] : [], NativeActions = [action], NativeCards = cards ?? NativeCardInventory.CreateNewGame(), ChocoboWorldMode = mode, ChocoboRewards = ledger ?? new(), NativeCrtRandom = crt ?? NativeCrtRandomState.FromColdProcess(), NativeMessageSpeed = 1, NativeGlobalMessages = messages, NativeCardNames = Enumerable.Range(0, 110).Select(index => (string?)$"Card{index}").ToArray(), NativeItemNames = Enumerable.Range(0, 256).Select(index => (string?)$"Item{index}").ToArray(), NativeDevourEffects = [new(30, 0, 0, 0, 1, 10, "Devour effect")], Combatants = [new() { Id = "party-0", Name = "Squall", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, InitialAtb = BattleSession.AtbMaximum, NativeGrowth = new(9995, ImmutableArray.Create<byte>(255, 2, 3, 4, 5, 6)), NativeCommands = [new(command, "Special", flags, (byte)id)] }, new() { Id = "enemy", Name = "Enemy", ModelId = 22, MaximumHp = 1000, CurrentHp = 1, Speed = 1, NativeAi = ai, NativeSpecial = new(4, 4, [0, 0, 0], 10) }] }, BattleAtbMode.Normal);
        battle.Tick();
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
        return battle;
    }
}
