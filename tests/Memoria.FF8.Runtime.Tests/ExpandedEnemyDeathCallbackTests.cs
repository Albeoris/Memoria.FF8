using System.Collections.Immutable;
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
    public void QueuedDeathActionFinishesBeforeScriptedSelfRemovalAndOutcome()
    {
        BattleSession battle = CreateBattle([4, 203, 12, 0, 29, 200, 8, 0]);
        battle.QueueAttack("party-10", "enemy-0");
        Until(battle, () => battle.QueuedActions.Any(command => command.NativeAiCallbackStage == 3));
        BattleCombatant enemy = battle.Combatants.Single(actor => actor.Id == "enemy-0");
        Assert.True(enemy.IsActive);
        Assert.False(enemy.IsAlive);
        Assert.True(battle.Capture().Combatants.Single(actor => actor.Id == enemy.Id).NativeParticipant!.ExpandedAiRemovalPending);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        BattleSession queued = RoundTrip(battle);

        battle.Tick();
        Assert.Equal((byte?)3, battle.ActiveAction?.NativeAiCallbackStage);
        BattleSession active = RoundTrip(battle);
        Until(battle, () => battle.Outcome == BattleOutcome.Victory);
        Until(queued, () => queued.Outcome == BattleOutcome.Victory);
        Until(active, () => active.Outcome == BattleOutcome.Victory);

        Assert.False(enemy.IsActive);
        Assert.False(enemy.IsBusy);
        BattleEvent[] lifecycle = battle.Events.Where(entry => entry.ActorId == enemy.Id && entry.Kind is "EffectContact" or "ActionComplete" or "NativeModelRemoved").ToArray();
        Assert.Equal(["EffectContact", "ActionComplete", "NativeModelRemoved"], lifecycle.Select(entry => entry.Kind));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(queued.Capture()));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(active.Capture()));
    }

    [Fact]
    public void UntargetableEnemyKeepsTheBattleActiveUntilItCanBeDefeated()
    {
        BattleSession battle = CreateBattle([0], turn: [47, 0], init: [48, 0], readyEnemy: true);
        BattleCombatant enemy = battle.Combatants.Single(actor => actor.Id == "enemy-0");
        Assert.True(enemy.IsAlive);
        Assert.True(enemy.Untargetable);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        BattleSession restored = RoundTrip(battle);
        Until(battle, () => !enemy.Untargetable);
        Until(restored, () => !restored.Combatants.Single(actor => actor.Id == enemy.Id).Untargetable);
        battle.QueueAttack("party-10", enemy.Id);
        restored.QueueAttack("party-10", enemy.Id);
        Until(battle, () => battle.Outcome == BattleOutcome.Victory);
        Until(restored, () => restored.Outcome == BattleOutcome.Victory);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void QueuedTurnActionAndRemovalFinishBeforeScriptedExit()
    {
        BattleSession battle = CreateBattle([0], turn: [4, 201, 12, 0, 29, 200, 57, 0], readyEnemy: true);
        BattleCombatant enemy = battle.Combatants.Single(actor => actor.Id == "enemy-0");
        Until(battle, () => battle.ActiveAction?.NativeAiCallbackStage == 1);
        Assert.True(enemy.IsActive);
        Assert.True(battle.NativeScriptState.ScriptedExitRequested);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        BattleSession restored = RoundTrip(battle);
        Until(battle, () => battle.Outcome == BattleOutcome.ScriptedExit);
        Until(restored, () => restored.Outcome == BattleOutcome.ScriptedExit);
        Assert.Equal((byte)1, battle.NativeResult);
        Assert.True(battle.CanReturnToField);
        Assert.False(enemy.IsActive);
        Assert.Single(battle.Events, entry => entry.ActorId == enemy.Id && entry.Kind == "EffectContact");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
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

    [Fact]
    public void FirstDefeatRewardsSurviveRevivalLevelChangeAndAnotherDefeat()
    {
        BattleSession battle = CreateBattle([2, 220, 200, 0, 0, 0, 4, 0, 14, 220, 1, 22, 0], rewardsAndLevelCommand: true);
        BattleCombatant enemy = battle.Combatants.Single(actor => actor.Id == "enemy-0");
        BattleCombatant killer = battle.Combatants.Single(actor => actor.Id == "party-10");
        battle.QueueAttack(killer.Id, enemy.Id);
        Until(battle, () => !killer.IsBusy && battle.EnemyDefeats.Count == 1);
        BattleEnemyDefeat first = Assert.Single(battle.EnemyDefeats);
        BattleStock item = Assert.Single(battle.NativeRewardItems);
        Assert.Equal(4, first.Level);
        Assert.Equal(1, killer.NativeKillCount);
        Assert.True(enemy.IsAlive);

        battle.QueueNativePlayerCommand("party-9", enemy.Id, 34, 0);
        Until(battle, () => battle.ActiveAction is null && enemy.Level == 8);
        BattleSession restored = RoundTrip(battle);
        Assert.Equal(8, restored.Combatants.Single(actor => actor.Id == enemy.Id).Level);
        Assert.Equal(first, Assert.Single(restored.EnemyDefeats));
        Assert.Equal(item, Assert.Single(restored.NativeRewardItems));
        BattleSnapshot snapshot = battle.Capture();
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { EnemyDefeats = [first with { AllocationId = killer.Id }] }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { EnemyDefeats = [first, first] }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { EnemyDefeats = [first, first with { AllocationId = null }] }));
        Assert.Null(Assert.Single(BattleSession.Restore(snapshot with { EnemyDefeats = [first with { AllocationId = null }] }).EnemyDefeats).AllocationId);

        battle.QueueAttack("party-8", enemy.Id);
        restored.QueueAttack("party-8", enemy.Id);
        Until(battle, () => battle.Outcome == BattleOutcome.Victory);
        Until(restored, () => restored.Outcome == BattleOutcome.Victory);
        Assert.Equal(first, Assert.Single(battle.EnemyDefeats));
        Assert.Equal(item, Assert.Single(battle.NativeRewardItems));
        Assert.Equal(1, killer.NativeKillCount);
        Assert.Equal(0, battle.Combatants.Single(actor => actor.Id == "party-8").NativeKillCount);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(first, Assert.Single(RoundTrip(restored).EnemyDefeats));
    }

    private static BattleSession CreateBattle(byte[] death, bool rewardsAndLevelCommand = false, byte[]? turn = null, byte[]? init = null, bool readyEnemy = false)
    {
        BattleNativeAction attack = Action(1, 0, 1, 70, 0);
        BattleNativeAction retaliation = Action(8, 1, 0, 0, 3);
        BattleNativeAction levelUp = Action(34, 0, 16, 16, 0) with { KernelSection = 10, TargetFlags = 0x50, StatusAttack = 255 };
        NativeBattleAiDefinition ai = new([init ?? [0], turn ?? [0], [0], death, [0]], Enumerable.Repeat(new NativeBattleAiAbility(8, 0, 1), 48).ToArray(), 0, []);
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"party-{index}", Name = $"Party {index}", IsPlayer = true, ModelId = index, CurrentHp = 1000, MaximumHp = 1000, Attack = 30, Hit = 255, Speed = 1,
            Abilities = [BattleAbility.Attack], InitialAtb = index == 10 || rewardsAndLevelCommand && index >= 8 ? BattleSession.AtbMaximum : 0, NativeAttack = attack,
            NativeCommands = rewardsAndLevelCommand && index == 9 ? [new(34, "Level Up", 0x50, 0)] : []
        }).ToArray();
        BattleCombatantDefinition enemy = new() { Id = "enemy-0", Name = "Enemy", ModelId = 36, CurrentHp = 1, MaximumHp = 100, Speed = 1, NativeAi = ai, NativeCombatantFlags = 0x20, InitialAtb = readyEnemy ? BattleSession.AtbMaximum : 0 };
        if (rewardsAndLevelCommand)
        {
            BattleEnemyRewardDefinition rewards = new(10, 20, 20, 30, 255, false, 255, [.. Enumerable.Repeat(new BattleStock(1, 1), 12)]);
            enemy = enemy with { Level = 4, SceneSlot = 0, RewardSlot = 0, Rewards = rewards, MaximumHp = 1 };
        }
        IEnumerable<BattleCombatantDefinition> otherEnemies = Enumerable.Range(1, 10).Select(index => new BattleCombatantDefinition
        {
            Id = $"enemy-{index}", Name = $"Enemy {index}", ModelId = 36, CurrentHp = 0, NativeStatus0 = 1
        });
        ImmutableArray<NativeBattleDrawSlot> drawBand = [.. Enumerable.Repeat(new NativeBattleDrawSlot(0, 0), 4)];
        ExpandedEnemyLevelProfile profile = new([.. Enumerable.Repeat(new ExpandedEnemyLevel(1, [0, 0, 0, 0, 1, 0], 0), 100)], [drawBand, drawBand, drawBand]);
        string?[] messages = new string?[96];
        messages[11] = " 0123456789";
        messages[88] = " Level ";
        messages[89] = "raised";
        return new(new()
        {
            UsesExpandedRoster = true, Combatants = [.. allies, enemy, .. otherEnemies], NativeActions = rewardsAndLevelCommand ? [retaliation, levelUp] : [retaliation], NativeSeed = 0,
            ExpandedEnemyLevels = rewardsAndLevelCommand ? ImmutableSortedDictionary<int, ExpandedEnemyLevelProfile>.Empty.Add(36, profile) : null, NativeGlobalMessages = messages, NativeMessageSpeed = 0,
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeFieldBattleMode = 0x200
        }, BattleAtbMode.TurnBased);
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
