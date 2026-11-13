using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedEnemyDeathCallbackTests
{
    [Theory]
    [InlineData(203, "party-10")]
    [InlineData(200, "enemy-0")]
    public void LastEnemyDeathActionDrainsBeforeVictoryAndResumesFromBothActionBoundaries(byte selector, string recipient)
    {
        BattleSession battle = CreateBattle([4, selector, 12, 0, 8, 0]);
        battle.QueueAttack("party-10", "enemy-0");
        Until(battle, () => battle.QueuedActions.Any(command => command.NativeAiCallbackStage == 3));
        BattleCombatant enemy = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-0");
        Assert.False(enemy.IsAlive);
        Assert.True(enemy.IsBusy);
        Assert.Equal(0, enemy.Atb);
        Assert.Equal("party-10", enemy.LastAttackerId);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        BattleAttackCommand command = Assert.Single(battle.QueuedActions);
        Assert.Equal(recipient, Assert.Single(command.AiRecipients!.ActorIds));
        Assert.Equal(selector == 200 ? enemy.Id : null, command.AiRecipients.DeathSelfTargetId);
        Assert.Equal(0, command.NativeTargetMask);
        Assert.True(battle.Capture().Combatants.Single(actor => actor.Id == enemy.Id).NativeParticipant!.DeathHandled);
        BattleSession queuedRestore = RoundTrip(battle);

        battle.Tick();
        queuedRestore.Tick();
        Assert.Equal((byte?)3, battle.ActiveAction?.NativeAiCallbackStage);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(queuedRestore.Capture()));
        BattleSession activeRestore = RoundTrip(battle);
        for (int frame = 0; frame < BattleSession.AttackWindupFrames + 2; frame++)
        {
            battle.Tick();
            queuedRestore.Tick();
            activeRestore.Tick();
        }

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Single(battle.Events, entry => entry.Kind == "Defeated" && entry.TargetId == enemy.Id);
        Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == enemy.Id && entry.TargetId == recipient);
        Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete" && entry.ActorId == enemy.Id);
        Assert.False(enemy.IsBusy);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(queuedRestore.Capture()));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(activeRestore.Capture()));
    }

    [Fact]
    public void DeathProgramCanRestoreHpWithoutGrantingAnotherTurnOrReplayingCallback()
    {
        BattleSession battle = CreateBattle([2, 220, 200, 0, 0, 0, 4, 0, 14, 220, 1, 22, 0]);
        BattleCombatant enemy = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-0");
        battle.QueueAttack("party-10", enemy.Id);
        Until(battle, () => battle.Events.Any(entry => entry.Kind == "Defeated"));

        Assert.True(enemy.IsAlive);
        Assert.Equal(enemy.MaximumHp, enemy.CurrentHp);
        Assert.Equal(0, enemy.Atb);
        Assert.False(enemy.IsBusy);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.False(battle.Capture().Combatants.Single(actor => actor.Id == enemy.Id).NativeParticipant!.DeathHandled);
        BattleSession restored = RoundTrip(battle);
        restored.Tick();
        battle.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    private static BattleSession CreateBattle(byte[] death)
    {
        BattleNativeAction attack = Action(1, 0, 1, 70, 0);
        BattleNativeAction retaliation = Action(8, 1, 0, 0, 3);
        NativeBattleAiDefinition ai = new([[0], [0], [0], death, [0]], Enumerable.Repeat(new NativeBattleAiAbility(8, 0, 1), 48).ToArray(), 0, []);
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"party-{index}", Name = $"Party {index}", IsPlayer = true, ModelId = index, CurrentHp = 1000, MaximumHp = 1000, Attack = 30, Hit = 255, Speed = 1,
            Abilities = [BattleAbility.Attack], InitialAtb = index == 10 ? BattleSession.AtbMaximum : 0, NativeAttack = attack
        }).ToArray();
        BattleCombatantDefinition enemy = new() { Id = "enemy-0", Name = "Enemy", ModelId = 36, CurrentHp = 1, MaximumHp = 100, Speed = 1, NativeAi = ai, NativeCombatantFlags = 0x20 };
        IEnumerable<BattleCombatantDefinition> otherEnemies = Enumerable.Range(1, 10).Select(index => new BattleCombatantDefinition
        {
            Id = $"enemy-{index}", Name = $"Enemy {index}", CurrentHp = 0, NativeStatus0 = 1
        });
        return new(new() { UsesExpandedRoster = true, Combatants = [.. allies, enemy, .. otherEnemies], NativeActions = [retaliation], NativeSeed = 0 }, BattleAtbMode.TurnBased);
    }

    private static BattleNativeAction Action(byte command, ushort id, byte attackType, byte power, byte attackFlags)
    {
        return new(command, id, 3, 0)
        {
            Name = "Action", MagicId = 0, AttackType = attackType, Power = power, HitCount = 1, TargetFlags = null, AttackFlags = attackFlags, DamagePresentation = 4, Element = 0, ElementPercent = null,
            StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
    }

    private static BattleSession RoundTrip(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }

    private static void Until(BattleSession battle, Func<bool> condition)
    {
        for (int frame = 0; frame < 100 && !condition(); frame++)
        {
            battle.Tick();
        }
        Assert.True(condition());
    }
}
