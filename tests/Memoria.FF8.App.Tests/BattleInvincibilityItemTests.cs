using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleInvincibilityItemTests
{
    [Theory]
    [InlineData(18, false)]
    [InlineData(19, false)]
    [InlineData(20, true)]
    [InlineData(21, true)]
    public void Installed_items_consume_stock_only_when_the_selected_target_or_group_can_gain_invincibility(int id, bool group)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition item = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Item && action.Id == id);
        Assert.True(BattleSession.IsInvincibilityItem(item));
        BattleSession battle = Create(game, assets, item);
        BattleCombatant caster = battle.Combatants[0];
        BattleCombatant ally = battle.Combatants[1];
        BattleCombatant defeated = battle.Combatants[2];

        Assert.False(battle.CanReceiveInvincibility(caster, item.NativeAction!));
        Assert.False(battle.CanReceiveInvincibility(defeated, item.NativeAction!));
        Assert.True(battle.CanReceiveInvincibility(ally, item.NativeAction!));
        if (!group)
        {
            Assert.Throws<InvalidOperationException>(() => battle.QueueAction(caster.Id, caster.Id, BattleAbility.Item, id));
            Assert.Equal(2, Assert.Single(battle.Items).Count);
            Assert.DoesNotContain(battle.QueuedActions, command => command.Ability == BattleAbility.Item);
        }
        battle.QueueAction(caster.Id, group ? caster.Id : ally.Id, BattleAbility.Item, id);
        Assert.Equal(1, Assert.Single(battle.Items).Count);

        // A queued action can lose its last eligible target before it starts.
        BattleSession cancelled = battle.ForkForContinuation();
        cancelled.Combatants[1].ReceivedInvincibility = true;
        for (int frame = 0; frame < 1200 && cancelled.QueuedActions.Any(command => command.Ability == BattleAbility.Item); frame++)
        {
            cancelled.Tick();
        }
        Assert.Equal(2, Assert.Single(cancelled.Items).Count);
        Assert.DoesNotContain(cancelled.QueuedActions, command => command.Ability == BattleAbility.Item);
        Assert.Null(cancelled.ActiveAction);
    }

    [Theory]
    [InlineData(19, false)]
    [InlineData(21, true)]
    public void Installed_hero_effects_apply_only_to_eligible_targets_and_keep_history_after_the_status_expires(int id, bool group)
    {
        NativeBattleExecutionStack? execution = NativeExecutionStackSelection.Read(NativeExecutionStackSelection.ConfiguredPath);
        Assert.SkipUnless(execution?.HeroCrtStack is not null, "Select an execution-stack profile with a Hero CRT contract.");
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition item = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Item && action.Id == id);
        BattleSession battle = Create(game, assets, item, execution: execution);
        battle.QueueAction("caster", group ? "caster" : "ally", BattleAbility.Item, id);
        for (int frame = 0; frame < 1200 && (battle.QueuedActions.Count != 0 || battle.ActiveAction is not null); frame++)
        {
            battle.Tick();
        }
        Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete" && entry.ActorId == "caster");
        Assert.Equal("ally", Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == "caster").TargetId);
        BattleCombatant protectedAlly = battle.Combatants[1];
        Assert.True(protectedAlly.ReceivedInvincibility);
        Assert.Equal(0x800u, protectedAlly.NativeStatus1 & 0x800u);

        // Expiring the status or forking the battle preserves the application history.
        protectedAlly.NativeStatus1 &= ~0x800u;
        protectedAlly.NativeStatusTimers[11] = -1111;
        BattleSession restored = battle.ForkForContinuation();
        Assert.False(restored.CanUseInvincibilityItem(restored.Combatants[0], item, restored.Combatants[1]));
        Assert.Equal(1, Assert.Single(restored.Items).Count);
        BattleSession unrestricted = Create(game, assets, item, enabled: false);
        Assert.True(unrestricted.CanReceiveInvincibility(unrestricted.Combatants[0], item.NativeAction!));
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, BattleActionDefinition item, bool enabled = true, NativeBattleExecutionStack? execution = null)
    {
        BattleDefinition definition = new()
        {
            InvincibilityOncePerBattle = enabled,
            StageId = assets.StageIds.First(),
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, ModelId = 0, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Item] },
                new() { Id = "ally", Name = "Ally", IsPlayer = true, ModelId = 1 },
                new() { Id = "defeated", Name = "Defeated", IsPlayer = true, ModelId = 2, CurrentHp = 0, NativeStatus0 = 1 },
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, CurrentHp = 10000, MaximumHp = 10000, Speed = 1 }
            ],
            ActionDefinitions = [item],
            Items = [new(item.Id, 2)],
            InitialRandom = NativeBattleRandomState.ForecastPhase(0)
        };
        BattleSession battle = InstalledBattlePractice.Create(definition, BattleAtbMode.TurnBased, game, assets);
        if (execution?.HeroCrtStack is not null)
        {
            BattleDefinition prepared = battle.ReadDefinition();
            NativeBattleRetainedMemory retained = NativeExecutionStackSelection.Apply(prepared.NativeRetainedMemory!, execution);
            battle = InstalledBattlePresentation.Attach(new(prepared with { NativeRetainedMemory = retained }, BattleAtbMode.TurnBased), game, assets);
        }
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: "caster");
        for (int frame = 0; frame < 1800 && !battle.CanChoosePlayerCommand("caster"); frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand("caster"));
        battle.Combatants[0].ReceivedInvincibility = true;
        return battle;
    }
}
