using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeSlotsInvocationTests
{
    [Fact]
    public void Slots_roll_consumes_four_native_draws_and_render_queries_preserve_the_owned_result()
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession battle = CreateBattle(assets, 0);
        NativeBattleRandomState expectedRandom = battle.Capture().Random!;
        expectedRandom = expectedRandom.Draw(out byte levelRoll).Draw(out byte bucketRoll).Draw(out byte spellRoll).Draw(out byte repeatRoll);
        BattleNativeSlotsRoll expected = NativeBattleSlotsRules.Roll(assets.ReadNativeLimitDefinition(5), 20, 4, levelRoll, bucketRoll, spellRoll, repeatRoll);

        BattleNativeSlotsRoll roll = battle.RollNativeSlots("selphie");

        Assert.Equal(expected, roll);
        Assert.Equal(JsonSerializer.Serialize(expectedRandom), JsonSerializer.Serialize(battle.Capture().Random));
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        Assert.Single(battle.GetNativePlayerActionChoices("selphie", true));
        Assert.Equal(snapshot, JsonSerializer.Serialize(battle.Capture()));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);
        Assert.Equal(roll, restored.GetNativeSlotsRoll("selphie"));
        Assert.Equal(snapshot, JsonSerializer.Serialize(restored.Capture()));
        battle.QueueNativePlayerCommand("selphie", "enemy", 16, roll.MagicId);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal(roll, queued.NativeSlots);
        Assert.Equal((byte)16, queued.NativeAction!.Command);
        Assert.Equal(1, battle.NativeCommandBanks.CountActor(0));
        NativeBattleCommandBank bank = battle.NativeCommandBanks.Banks[1];
        Assert.Equal(roll.Repetitions, Enumerable.Range(0, 3).Count(index => bank.Packets[0].ReadWord(6 + 2 * index) != 0));
        string accepted = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(accepted, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(accepted)!).Capture()));
    }

    [Fact]
    public void Special_magic_learns_at_dispatch_and_unknown_saved_learning_fails_before_queue_mutation()
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession battle = CreateBattle(assets, 0x80);
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("selphie", 4) { Slots = new(53, 2) }] });
        battle.QueueNativePlayerCommand("selphie", "enemy", 16, 53);
        Assert.Null(battle.NativeScriptState.LimitSpecialFlagsBefore);

        battle.Tick();

        Assert.Equal((byte)0x80, battle.NativeScriptState.LimitSpecialFlagsBefore);
        Assert.Equal((byte)0x84, battle.NativeScriptState.LimitSpecialFlagsAfter);
        Assert.Equal((byte)4, battle.NativeScriptState.LimitSpecialSetMask);
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(snapshot, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!).Capture()));
        BattleSession unknown = CreateBattle(assets, null);
        unknown = BattleSession.Restore(unknown.Capture() with { NativeLimits = [new("selphie", 4) { Slots = new(53, 2) }] });
        string before = JsonSerializer.Serialize(unknown.Capture());
        Assert.Throws<NotSupportedException>(() => unknown.QueueNativePlayerCommand("selphie", "enemy", 16, 53));
        Assert.Equal(before, JsonSerializer.Serialize(unknown.Capture()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Card_deal_preserves_consecutive_rolls_and_snapshot_selection(int crisis)
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession battle = CreateBattle(assets, 0, true, (byte)crisis);
        NativeBattleRandomState expectedRandom = battle.Capture().Random!;
        List<BattleNativeSlotsRoll> expected = [];
        for (int index = 0; index < 2 * crisis + 1; index++)
        {
            expectedRandom = expectedRandom.Draw(out byte a).Draw(out byte b).Draw(out byte c).Draw(out byte d);
            expected.Add(NativeBattleSlotsRules.Roll(assets.ReadNativeLimitDefinition(5), 20, (byte)crisis, a, b, c, d));
        }
        BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal("selphie");
        Assert.Equal(expected, deal.Candidates.ToArray());
        Assert.Equal(JsonSerializer.Serialize(expectedRandom), JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Equal((ulong)1, battle.NativeGlobalRandomChange!.Calls);
        Assert.Equal(deal, battle.BeginNativeSlotsDeal("selphie"));
        Assert.Throws<InvalidOperationException>(() => battle.RollNativeSlots("selphie"));
        Assert.Throws<InvalidOperationException>(() => battle.RefreshNativeLimit("selphie"));
        Assert.Throws<InvalidOperationException>(() => battle.QueueNativePlayerCommand("selphie", "enemy", 16, deal.Candidates[0].MagicId));
        Assert.Throws<InvalidOperationException>(() => battle.AdvanceTurn());
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        Assert.False(battle.SelectNativeSlotsCard(Guid.NewGuid(), 0));
        Assert.False(battle.SelectNativeSlotsCard(deal.Id, -1));
        Assert.True(battle.SelectNativeSlotsCard(deal.Id, 0));
        Assert.False(battle.SelectNativeSlotsCard(deal.Id, 1));
        battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Assert.Equal(0, battle.PendingSlotsDeal!.SelectedIndex);
        Assert.True(battle.SubmitNativeSlotsCard(deal.Id));
        Assert.False(battle.SubmitNativeSlotsCard(deal.Id));
        Assert.Null(battle.PendingSlotsDeal);
        Assert.Equal(deal.Candidates[0], Assert.Single(battle.QueuedActions).NativeSlots);
        Assert.Equal((ulong)1, battle.NativeGlobalRandomChange!.Calls);
        BattleSession restoredPending = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);
        Assert.Equal(snapshot, JsonSerializer.Serialize(restoredPending.Capture()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Card_masks_and_duplicate_spell_counts_are_retained(int crisis)
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession battle = CreateBattle(assets, 0, true, (byte)crisis);
        BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal("selphie");
        foreach (int mask in new[] { 0, (1 << deal.Candidates.Length) - 1, 0x55 & ((1 << deal.Candidates.Length) - 1) })
        {
            BattleNativeSlotsRoll roll = deal.Candidates[0] with { Repetitions = 1 };
            BattleNativeSlotsDeal duplicate = deal with { RevealMask = mask, Candidates = deal.Candidates.SetItem(0, roll).SetItem(deal.Candidates.Length - 1, roll with { Repetitions = 3 }) };
            BattleSession restored = BattleSession.Restore(battle.Capture() with { PendingSlotsDeal = duplicate });
            Assert.Equal(mask, restored.PendingSlotsDeal!.RevealMask);
            Assert.True(restored.SelectNativeSlotsCard(deal.Id, 0));
            Assert.True(restored.SubmitNativeSlotsCard(deal.Id));
            Assert.Equal(roll, Assert.Single(restored.QueuedActions).NativeSlots);
            Assert.Null(restored.NativeScriptState.LimitSpecialFlagsBefore);
        }
    }

    private static BattleSession CreateBattle(BattleAssets assets, byte? specialFlags, bool cardSlots = false, byte crisis = 4)
    {
        NativeLimitProgress progress = new(new string('A', 64), 0, 0, 0, 0, Enumerable.Repeat((byte)0, 8).ToImmutableArray()) { SpecialMagicFlags = specialFlags };
        BattleSession battle = new(new BattleDefinition { SelphieCardSlots = cardSlots, NativeGlobalRandom = new(12345), NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeActions = assets.NativeActions.ToArray(), LimitProgress = progress, NativeLimitsDisabled = false, Combatants = [new() { Id = "selphie", Name = "Selphie", NativeStatusResistances = new byte[40], IsPlayer = true, ModelId = 5, Level = 20, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(5) }, new() { Id = "enemy", Name = "Enemy", NativeStatusResistances = new byte[40], Speed = 1 }] }, BattleAtbMode.TurnBased);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("selphie", crisis)] });
    }

    private static BattleAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return new BattleAssets(directory);
    }
}
