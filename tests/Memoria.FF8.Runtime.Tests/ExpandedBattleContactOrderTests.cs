using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleContactOrderTests
{
    [Theory]
    [InlineData(BattleAbility.Magic, false, 1, false)]
    [InlineData(BattleAbility.Magic, true, 2, false)]
    [InlineData(BattleAbility.Item, false, 2, false)]
    [InlineData(BattleAbility.Item, true, 1, false)]
    [InlineData(BattleAbility.Magic, true, 1, true)]
    public void OneCommandPublishesOrderedContactsAndSpendsOneResource(BattleAbility ability, bool allTargets, byte hitCount, bool bothSides)
    {
        const int actionId = 1;
        BattleNativeAction native = new(ability == BattleAbility.Magic ? (byte)2 : (byte)4, actionId, -1, -1)
        {
            AttackType = 27, Power = 1, Hit = 0, HitCount = hitCount, TargetFlags = bothSides ? (byte)0x68 : allTargets ? (byte)0x48 : (byte)0x50, AttackFlags = 3,
            MagicId = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0,
            Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleActionDefinition action = new(ability, actionId, "Contact test", BattleActionEffect.Native, 1, false, allTargets) { NativeAction = native };
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"ally-{index}", Name = $"Ally {index}", ModelId = index, IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, Speed = 1
        }).ToArray();
        allies[^1] = allies[^1] with { Abilities = [ability], InitialAtb = BattleSession.AtbMaximum, MagicStocks = [new(actionId, 2)] };
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"enemy-{index}", Name = $"Enemy {index}", MaximumHp = 1000, CurrentHp = 1000, Speed = 1
        }).ToArray();
        BattleSession battle = new(new() { UsesExpandedRoster = true, Combatants = [.. allies, .. enemies], ActionDefinitions = [action], NativeActions = [native], Items = [new(actionId, 2)] }, BattleAtbMode.Normal);
        Assert.True(battle.SupportsExpandedAction(action));
        battle.QueueAction(allies[^1].Id, enemies[^1].Id, ability, actionId);
        BattleSession restored = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(battle.Capture()));
        for (int tick = 0; tick <= BattleSession.AttackWindupFrames; tick++)
        {
            battle.Tick();
            restored.Tick();
        }
        string[] recipients = bothSides ? allies.Concat(enemies).Select(actor => actor.Id).ToArray() : allTargets ? enemies.Select(enemy => enemy.Id).ToArray() : [enemies[^1].Id];
        Assert.Equal(Enumerable.Range(0, hitCount).SelectMany(_ => recipients), battle.Events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.TargetId));
        Assert.All(battle.Combatants, actor => Assert.Equal(recipients.Contains(actor.Id) ? 1000 - 100 * hitCount : 1000, actor.CurrentHp));
        Assert.Equal(ability == BattleAbility.Item ? 1 : 2, Assert.Single(battle.Items).Count);
        Assert.Equal(ability == BattleAbility.Magic ? 1 : 2, Assert.Single(battle.Combatants.Single(actor => actor.Id == allies[^1].Id).MagicStocks).Count);
        Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void RegenOnTheEleventhAllyUsesTheOriginalFivePercentTick()
    {
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"ally-{index}", Name = $"Ally {index}", ModelId = index, IsPlayer = true, MaximumHp = 1000, CurrentHp = 500, Speed = 1
        }).ToArray();
        allies[^1] = allies[^1] with { NativeStatus1 = 0x10 };
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"enemy-{index}", Name = $"Enemy {index}", MaximumHp = 1000, CurrentHp = 1000, Speed = 1
        }).ToArray();
        BattleSession battle = new(new() { UsesExpandedRoster = true, Combatants = [.. allies, .. enemies], NativeStatusRules = new(0, [.. Enumerable.Repeat((byte)30, 14)]) }, BattleAtbMode.Normal);
        BattleSnapshot snapshot = battle.Capture();
        BattleCombatantSnapshot[] combatants = snapshot.Combatants.ToArray();
        int index = Array.FindIndex(combatants, actor => actor.Id == "ally-10");
        NativeBattleStatusSnapshot status = combatants[index].NativeStatus!;
        short[] timers = status.Timers.ToArray();
        timers[4] = 120;
        combatants[index] = combatants[index] with { NativeStatus = status with { Timers = [.. timers] } };
        battle = BattleSession.Restore(snapshot with { Combatants = combatants });
        battle.Tick();
        Assert.Equal(550, battle.Combatants.Single(actor => actor.Id == "ally-10").CurrentHp);
        Assert.All(battle.Combatants.Where(actor => actor.IsPlayer && actor.Id != "ally-10"), actor => Assert.Equal(500, actor.CurrentHp));
    }
}
