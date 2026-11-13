using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    private static void PrepareDrawCommand(NativeCharacterVital[] records, uint draw)
    {
        NativeCharacterVital caster = records[10];
        records[10] = caster with { Junction = caster.Junction with { Commands = draw } };
    }

    private static void PrepareIguionDrawAndPartners(GameWorld world, BattleSession battle, BattleCombatant target)
    {
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        NativeBattleDrawSlot carbuncle = Assert.Single(target.NativeDrawSlots, slot => slot.Id >= 64);
        Assert.Contains(BattleAbility.Draw, caster.Abilities);
        Assert.False(Assert.Single(battle.GuardianForces, gf => gf.Id == carbuncle.Id - 64).Acquired);
        caster.Atb = BattleSession.AtbMaximum;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id, Ability: BattleAbility.Draw, ActionId: carbuncle.Id));
        Assert.Contains(battle.QueuedActions, command => command.ActorId == caster.Id && command.TargetId == target.Id && command.Ability == BattleAbility.Draw);
        FinishTurnStimulus(world, battle, caster);
        Assert.True(Assert.Single(battle.GuardianForces, gf => gf.Id == carbuncle.Id - 64).Acquired);
        Assert.All(battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive), actor => Assert.DoesNotContain(actor.NativeDrawSlots, slot => slot.Id == carbuncle.Id));
        string checkpoint = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(checkpoint)!);
        Assert.Equal(checkpoint, JsonSerializer.Serialize(restored.Capture()));
        DefeatOtherEnemies(world, battle, enemy => enemy.ModelId != target.ModelId);
        Assert.Equal(2, battle.Combatants.Count(actor => !actor.IsPlayer && actor.IsAlive && actor.IsActive));
    }

    private static void PrepareSoleEnemyByLethalAttacks(GameWorld world, BattleSession battle, BattleCombatant target)
    {
        DefeatOtherEnemies(world, battle, enemy => enemy.Id != target.Id);
        Assert.Same(target, Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsAlive && actor.IsActive));
    }

    private static void DefeatOtherEnemies(GameWorld world, BattleSession battle, Func<BattleCombatant, bool> shouldDefeat)
    {
        BattleCombatant attacker = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        foreach (BattleCombatant enemy in battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive && shouldDefeat(actor)).ToArray())
        {
            // Isolate the ordinary lethal contact and its original KO callback.
            enemy.CurrentHp = 1;
            for (int attempt = 0; attempt < 6 && enemy.IsAlive; attempt++)
            {
                attacker.Atb = BattleSession.AtbMaximum;
                world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: attacker.Id, TargetId: enemy.Id));
                FinishTurnStimulus(world, battle, attacker);
            }
            Assert.False(enemy.IsAlive);
        }
    }

    private static void FinishTurnStimulus(GameWorld world, BattleSession battle, BattleCombatant actor)
    {
        for (int frame = 0; frame < 320 && (actor.IsBusy || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.False(actor.IsBusy);
    }
}
