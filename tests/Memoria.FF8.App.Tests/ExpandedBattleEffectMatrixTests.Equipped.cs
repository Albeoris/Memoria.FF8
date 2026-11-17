using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(7)]
    [InlineData(23)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(26)]
    [InlineData(27)]
    [InlineData(29)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(34)]
    public void Installed_equipped_command_reaches_its_original_recipients_once(byte command)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.KernelSection == 10 && action.Command == command);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), effect => effect.Effect == action.MagicId);
        (string status, string detail) = RunEquippedCommand(directory, PrepareField(directory), effect, action);
        Assert.True(status is "native" or "logical", detail);
    }

    private static (string Status, string Detail) RunEquippedCommand(string directory, GameState field, NativeBattleEffectBinding effect, BattleNativeAction action)
    {
        bool special = action.Command is 7 or 29;
        ushort formation = special ? (ushort)22 : action.Command is 33 or 34 ? (ushort)89 : (ushort)0;
        if (special)
        {
            field = field with { GameplayPreferences = field.GameplayPreferences with { Tuning = field.GameplayPreferences.Tuning with { MaximumDamage = 4 } } };
        }
        using GameWorld world = StartBattle(directory, field, formation, equipped: action);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);
        Assert.Null(battle.ActiveAction);

        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        int commandIndex = hud.Commands.ToList().IndexOf(BattleAbility.NativeCommand);
        Assert.InRange(commandIndex, 0, hud.Commands.Count - 1);
        hud.Choose(commandIndex);
        Assert.True(hud.SelectingTarget);
        BattleCombatant target = hud.Targets.Last();
        hud.SelectTarget(target.Id, false);
        target = Assert.IsType<BattleCombatant>(hud.SelectedTarget);
        if (special)
        {
            WeakenSpecialTarget(world, battle, caster, target);
        }
        int level = target.Level;
        string[] expected = hud.SelectingTargetGroup ? hud.Targets.Select(actor => actor.Id).Order().ToArray() : [target.Id];
        int start = battle.Events.Count;
        hud.Attack();
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal(BattleAbility.NativeCommand, queued.Ability);
        Assert.Equal((action.Command << 16) | action.ActionId, queued.ActionId);
        Assert.Equal(action.Command, queued.NativeQueuedCommand);
        Assert.IsNotType<NativeBattleTargetRequestAdapter.DeferredOperands>(queued.TargetRequest.Selection);

        BattlePresentationProcess? presentation = null;
        for (int frame = 0; frame < 1000 && caster.IsBusy; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            if (battle.Presentation is { ActiveActionToken: not null, Graphics.OriginalEffect.ReturnedDescriptor: > 0 } live)
            {
                presentation ??= live;
                Assert.Same(presentation, live);
                Assert.Equal(effect.Effect, live.Graphics.OriginalEffect!.RequestedEffect);
            }
        }
        Assert.False(caster.IsBusy);
        BattleEvent[] events = battle.Events.Skip(start).Where(entry => entry.ActorId == caster.Id).ToArray();
        Assert.Single(events, entry => entry.Kind == "Windup" && entry.Ability == BattleAbility.NativeCommand);
        Assert.Single(events, entry => entry.Kind == "ActionComplete");
        Assert.Equal(expected, events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.TargetId).Order());
        if (action.AttackType is 5 or 6)
        {
            Assert.True(target.IsAlive);
        }
        if (action.Command is 33 or 34)
        {
            Assert.Equal(Math.Clamp(action.Power * level / 8, 1, 100), target.Level);
            Assert.Single(events, entry => entry.Kind == "LevelChanged" && entry.TargetId == target.Id);
        }
        if (action.Command == 31)
        {
            Assert.False(caster.IsActive);
            Assert.False(caster.IsAlive);
            Assert.All(battle.Combatants.Where(actor => actor.IsPlayer && actor != caster), ally => Assert.True(ally.IsActive));
        }
        if (special)
        {
            Assert.True(target.IsEjected);
            Assert.False(target.IsAlive);
            Assert.True(target.IsActive);
            Assert.Single(battle.EnemyDefeats, entry => entry.AllocationId == target.Id && entry.NativeCommand == action.Command);
        }
        Assert.True(presentation is null || presentation.ActiveActionToken is null);
        return (presentation is null ? "logical" : "native", $"Equipped {action.Name} used the original command {action.Command}/{action.ActionId} and completed with {expected.Length} roster contact(s).");
    }

    private static void WeakenSpecialTarget(GameWorld world, BattleSession battle, BattleCombatant caster, BattleCombatant target)
    {
        // The fixture's damage cap allows genuine Attack to approach the native Card or Devour threshold without KO.
        for (int attempt = 0; attempt < 40 && target.CurrentHp > 4; attempt++)
        {
            BattleCombatant? attacker = battle.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor != caster && actor.IsReady && actor.Abilities.Contains(BattleAbility.Attack));
            if (attacker is null)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
                continue;
            }
            world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: attacker.Id, TargetId: target.Id));
            for (int frame = 0; frame < 120 && attacker.IsBusy; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.True(target.IsAlive);
        }
        Assert.InRange(target.CurrentHp, 1, 4);
        Assert.True(caster.IsReady);
    }
}
