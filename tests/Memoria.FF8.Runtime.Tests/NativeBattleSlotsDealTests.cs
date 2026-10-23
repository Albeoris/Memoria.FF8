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

    [Fact]
    public void Failed_command_admission_releases_choice_without_publishing_a_partial_queue()
    {
        BattleSession battle = CreateBattle(BattleAtbMode.TurnBased);
        NativeBattleCommandBanks banks = battle.NativeCommandBanks;
        for (int index = 0; index < 11; index++)
        {
            (banks, _) = banks.Allocate(1);
        }
        battle = BattleSession.Restore(battle.Capture() with { NativeCommandBanks = banks });
        BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal("selphie");
        Assert.True(battle.SelectNativeSlotsCard(deal.Id, 0));
        // The limit bank has eleven reserved physical entries and cannot admit another command.
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => battle.SubmitNativeSlotsCard(deal.Id));
        Assert.Contains("exhausted", error.Message);
        Assert.Null(battle.PendingSlotsDeal);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(JsonSerializer.Serialize(banks), JsonSerializer.Serialize(battle.NativeCommandBanks));
        Assert.False(battle.SubmitNativeSlotsCard(deal.Id));
        battle.CloseNativeActionMenu();
    }

    private static BattleSession CreateBattle(BattleAtbMode mode)
    {
        BattleNativeLimitDefinition rules = new(5, 16, 0, 0, Enumerable.Repeat((byte)0, 32).ToImmutableArray())
        {
            SlotsSelectionBytes = new byte[80].ToImmutableArray(),
            SlotsSpellBytes = Enumerable.Range(0, 16).Select(index => (byte)1).ToImmutableArray()
        };
        BattleNativeAction action = new(16, 1, 0, 0)
        {
            Name = "Spell", MagicId = 1, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = 2, AttackFlags = 0, DamagePresentation = 0,
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
