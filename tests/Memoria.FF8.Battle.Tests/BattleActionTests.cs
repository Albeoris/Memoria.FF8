using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class BattleActionTests
{
    [Fact]
    public void MagicConsumesOwnedStockAndRestoresAcrossWindup()
    {
        BattleSession session = Create();
        session.QueueAction("party", "enemy", BattleAbility.Magic, 1);
        Assert.Equal(2, session.Combatants[0].MagicStocks.Single(stock => stock.Id == 1).Count);
        session.Tick();
        BattleSession restored = BattleSession.Restore(session.Capture());
        Resolve(session);
        Resolve(restored);
        Assert.Equal(1, session.Combatants[0].MagicStocks.Single(stock => stock.Id == 1).Count);
        Assert.Equal(252, session.Combatants[1].CurrentHp);
        Assert.Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void ItemHealingUsesInstalledPowerTimesFiftyAndCannotOverspend()
    {
        BattleSession session = Create();
        session.QueueAction("party", "party", BattleAbility.Item, 1);
        Assert.Empty(session.Items);
        Resolve(session);
        Assert.Equal(250, session.Combatants[0].CurrentHp);
        Assert.Contains(session.Events, entry => entry.Kind == "Healing" && entry.Amount == 200);
        Assert.Throws<InvalidOperationException>(() => session.QueueAction("party", "party", BattleAbility.Item, 1));
    }

    [Fact]
    public void DrawStockUsesEnemyPairAndPersistsItsRandomStream()
    {
        BattleSession session = Create();
        session.QueueAction("party", "enemy", BattleAbility.Draw, 1);
        session.Tick();
        BattleSession restored = BattleSession.Restore(session.Capture());
        Resolve(session);
        Resolve(restored);
        Assert.InRange(session.Combatants[0].MagicStocks.Single(stock => stock.Id == 1).Count, 3, 11);
        Assert.Contains(session.Events, entry => entry.Kind == "Draw" && entry.Amount > 0);
        Assert.Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void DrawCastDoesNotConsumeOrStockMagic()
    {
        BattleSession session = Create();
        session.QueueAction("party", "enemy", BattleAbility.Draw, 1, true, drawCastTargetId: "enemy");
        Resolve(session);
        Assert.Equal(2, session.Combatants[0].MagicStocks.Single(stock => stock.Id == 1).Count);
        Assert.Equal(105, session.Combatants[1].CurrentHp);
    }

    [Fact]
    public void SummonRequiresJunctionAndHitsAllLivingOpponents()
    {
        BattleSession session = Create();
        Assert.Throws<InvalidOperationException>(() => session.QueueAction("party", "enemy", BattleAbility.GuardianForce, 2));
        session.QueueAction("party", "enemy", BattleAbility.GuardianForce, 1);
        Resolve(session);
        Assert.Equal(301, session.Combatants[1].CurrentHp);
        Assert.Equal(291, session.Combatants[2].CurrentHp);
    }

    [Fact]
    public void UnsupportedActionAndInvalidTargetLeaveAllOwnedStateUnchanged()
    {
        BattleSession session = Create();
        string before = JsonSerializer.Serialize(session.Capture());
        Assert.Throws<NotSupportedException>(() => session.QueueAction("party", "enemy", BattleAbility.Magic, 99));
        Assert.Throws<InvalidOperationException>(() => session.QueueAction("party", "fallen", BattleAbility.Item, 1));
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
    }

    [Fact]
    public void SingleTargetItemRetainsTheSelectedLivingOpponent()
    {
        BattleSession session = Create();
        session.QueueAction("party", "enemy", BattleAbility.Item, 1);
        BattleAttackCommand command = Assert.Single(session.QueuedActions);
        Assert.Equal("enemy", command.TargetId);
        Assert.Equal(BattleAbility.Item, command.Ability);
        Assert.Equal(0, session.Combatants[0].Atb);
        Assert.True(session.Combatants[0].IsBusy);
    }

    [Fact]
    public void RevivalAcceptsDefeatedAllyThroughQueueSnapshotAndDispatch()
    {
        BattleSession session = Create();
        session.QueueAction("party", "fallen", BattleAbility.Magic, 24);
        BattleSession restored = BattleSession.Restore(session.Capture());
        Resolve(restored);
        Assert.Equal(12, restored.Combatants[3].CurrentHp);
        Assert.Contains(restored.Events, entry => entry.Kind == "Healing" && entry.TargetId == "fallen");
    }

    [Fact]
    public void CastStatusUsesSharedTimersAndSurvivesRestore()
    {
        BattleSession session = Create();
        session.QueueAction("party", "party", BattleAbility.Magic, 35);
        Resolve(session);
        Assert.Contains(session.Combatants[0].StatusEffects, effect => effect.Kind == BattleStatusKind.Haste);
        Assert.Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(BattleSession.Restore(session.Capture()).Capture()));
    }

    private static BattleSession Create()
    {
        return new BattleSession(new BattleDefinition
        {
            Name = "Owned action stocks",
            Combatants =
            [
                new() { Id = "party", Name = "Squall", IsPlayer = true, MaximumHp = 500, CurrentHp = 50, InitialAtb = BattleSession.AtbMaximum, Speed = 1, Magic = 30, Level = 10, EquipmentIds = [1], Abilities = Enum.GetValues<BattleAbility>(), MagicStocks = [new(1, 2), new(24, 1), new(35, 1)] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 500, CurrentHp = 500, Speed = 1, Defense = 10, Level = 5, DrawSpells = [new(1, 0)] },
                new() { Id = "enemy2", Name = "Enemy 2", MaximumHp = 500, CurrentHp = 500, Speed = 1, Defense = 10 },
                new() { Id = "fallen", Name = "Ally", IsPlayer = true, MaximumHp = 100, CurrentHp = 0, NativeStatus0 = 1 }
            ],
            Items = [new(1, 1)],
            ActionDefinitions =
            [
                new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Damage, 20, false),
                new(BattleAbility.Magic, 24, "Life", BattleActionEffect.Revive, 0, true),
                new(BattleAbility.Magic, 35, "Haste", BattleActionEffect.Status, 0, true, Statuses: [BattleStatusKind.Haste]),
                new(BattleAbility.Item, 1, "Potion", BattleActionEffect.Heal, 4, true),
                new(BattleAbility.GuardianForce, 1, "Diamond Dust", BattleActionEffect.Damage, 40, false, true),
                new(BattleAbility.GuardianForce, 2, "Hell Fire", BattleActionEffect.Damage, 45, false, true)
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static void Resolve(BattleSession session)
    {
        for (int frame = 0; frame < 25; frame++)
        {
            session.Tick();
        }
    }
}
