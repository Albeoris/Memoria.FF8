using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSlotsDealTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Engine_owner_invalidation_releases_deal_and_rejects_stale_callbacks(bool stopped)
    {
        BattleSession battle = CreateBattle(BattleAtbMode.TurnBased);
        BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal("selphie");
        BattleCombatant actor = battle.Combatants[0];
        if (stopped)
        {
            actor.NativeStatus1 |= 1;
        }
        else
        {
            actor.CurrentHp = 0;
            actor.NativeStatus0 |= 1;
        }
        battle.RefreshNativeSlotsDeal();
        Assert.Null(battle.PendingSlotsDeal);
        Assert.False(battle.SelectNativeSlotsCard(deal.Id, 0));
        Assert.False(battle.SubmitNativeSlotsCard(deal.Id));
        Assert.Empty(battle.QueuedActions);
    }

    [Theory]
    [InlineData(BattleAtbMode.Normal)]
    [InlineData(BattleAtbMode.TurnBased)]
    public void Pending_choice_retains_normal_time_policy_and_read_queries_are_pure(BattleAtbMode mode)
    {
        BattleSession battle = CreateBattle(mode);
        BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal("selphie");
        string before = JsonSerializer.Serialize(battle.Capture());
        for (int count = 0; count < 10; count++)
        {
            Assert.Same(deal, battle.PendingSlotsDeal);
            Assert.Equal("Spell", battle.GetNativeSlotsCardName(1));
        }
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        int gauge = battle.Combatants[1].Atb;
        battle.Tick();
        Assert.Equal(mode == BattleAtbMode.Normal, battle.Combatants[1].Atb > gauge);
        Assert.Same(deal, battle.PendingSlotsDeal);
        Assert.Throws<InvalidOperationException>(() => battle.CloseNativeActionMenu());
        Assert.Throws<InvalidOperationException>(() => battle.InstantVictory());
        Assert.Throws<InvalidOperationException>(() => battle.InstantDefeat());
        battle.PublishNativeEscapeButtons(3);
        Assert.False(battle.Escape.Held);
    }

    [Fact]
    public void Missing_reveal_owner_fails_before_consuming_native_outcomes()
    {
        BattleSession battle = CreateBattle(BattleAtbMode.Normal);
        BattleSnapshot state = battle.Capture();
        battle = BattleSession.Restore(state with { Definition = state.Definition with { NativeGlobalRandom = null }, NativeGlobalRandom = null });
        string before = JsonSerializer.Serialize(battle.Capture());
        Assert.Throws<NotSupportedException>(() => battle.BeginNativeSlotsDeal("selphie"));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0x10)]
    public void Selected_card_uses_ordinary_command_owners_without_resetting_the_battle(byte targetFlags)
    {
        BattleSession battle = CreateBattle(BattleAtbMode.TurnBased, targetFlags);
        BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal("selphie");
        Assert.True(battle.SelectNativeSlotsCard(deal.Id, 1));
        BattleNativeSlotsRoll roll = deal.Candidates[1];
        BattleSnapshot selected = battle.Capture();
        BattleSession ordinary = BattleSession.Restore(selected with { PendingSlotsDeal = null, NativeLimits = [new("selphie", deal.CrisisLevel) { Slots = roll }] });
        ordinary.QueueNativePlayerCommand("selphie", "selphie", 16, roll.MagicId, crisis: deal.CrisisLevel);
        ordinary.CloseNativeActionMenu();
        long resets = battle.DiagnosticResetCount;
        BattleCombatant actor = battle.Combatants[0];

        Assert.True(battle.SubmitNativeSlotsCard(deal.Id));

        Assert.Equal(resets, battle.DiagnosticResetCount);
        Assert.Same(actor, battle.Combatants[0]);
        Assert.Equal(JsonSerializer.Serialize(ordinary.Capture()), JsonSerializer.Serialize(battle.Capture()));
        Assert.False(battle.SubmitNativeSlotsCard(deal.Id));
        Assert.Equal(roll, Assert.Single(battle.QueuedActions).NativeSlots);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0x10)]
    public void Failed_command_admission_releases_choice_without_publishing_a_partial_queue(byte targetFlags)
    {
        BattleSession battle = CreateBattle(BattleAtbMode.TurnBased, targetFlags);
        NativeBattleCommandBanks banks = battle.NativeCommandBanks;
        for (int index = 0; index < 11; index++)
        {
            (banks, _) = banks.Allocate(1);
        }
        battle = BattleSession.Restore(battle.Capture() with { NativeCommandBanks = banks });
        BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal("selphie");
        Assert.True(battle.SelectNativeSlotsCard(deal.Id, 0));
        BattleCombatant actor = battle.Combatants[0];
        actor.NativeStatus1 |= 0x880000;
        BattleSnapshot before = battle.Capture();
        // The limit bank has eleven reserved physical entries and cannot admit another command.
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => battle.SubmitNativeSlotsCard(deal.Id));
        Assert.Contains("exhausted", error.Message);
        Assert.Null(battle.PendingSlotsDeal);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(JsonSerializer.Serialize(banks), JsonSerializer.Serialize(battle.NativeCommandBanks));
        BattleSnapshot after = battle.Capture();
        Assert.Equal(JsonSerializer.Serialize(before.Random), JsonSerializer.Serialize(after.Random));
        Assert.Equal(JsonSerializer.Serialize(before.Combatants), JsonSerializer.Serialize(after.Combatants));
        Assert.Equal(JsonSerializer.Serialize(before.NativeGraphics), JsonSerializer.Serialize(after.NativeGraphics));
        Assert.Equal(before.Events, after.Events);
        Assert.False(battle.SubmitNativeSlotsCard(deal.Id));
        battle.CloseNativeActionMenu();
    }

    private static BattleSession CreateBattle(BattleAtbMode mode, byte targetFlags = 2)
    {
        BattleNativeLimitDefinition rules = new(5, 16, 0, 0, Enumerable.Repeat((byte)0, 32).ToImmutableArray())
        {
            SlotsSelectionBytes = new byte[80].ToImmutableArray(),
            SlotsSpellBytes = Enumerable.Range(0, 16).Select(index => (byte)1).ToImmutableArray()
        };
        BattleNativeAction action = new(16, 1, 0, 0)
        {
            Name = "Spell", MagicId = 1, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = targetFlags, AttackFlags = 0, DamagePresentation = 0,
            Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 0, Critical = 0, PowerModifier = 0, LevelModifier = 0, RawRecord = [], SupplementalRecord = []
        };
        BattleSession battle = new(new BattleDefinition
        {
            SelphieCardSlots = true, NativeGlobalRandom = new(12345), NativeLimitsDisabled = false, NativeActions = [action],
            Combatants = [new() { Id = "selphie", Name = "Selphie", IsPlayer = true, NativeStatusResistances = new byte[40], Level = 20, ModelId = 5, InitialAtb = BattleSession.AtbMaximum, NativeLimit = rules }, new() { Id = "enemy", Name = "Enemy", NativeStatusResistances = new byte[40], Speed = 1 }]
        }, mode);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("selphie", 4)] });
    }
}
