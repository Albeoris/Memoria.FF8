using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Fact]
    public void Installed_rapture_casts_the_owned_slots_roll_without_stock()
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row is { KernelSection: 1, Command: 16, ActionId: 53 });
        (string status, string detail) = RunExpandedRapture(directory, PrepareField(directory), assets, action);
        Assert.True(status == "logical", detail);
    }

    private static (string Status, string Detail) RunExpandedRapture(string directory, GameState field, BattleAssets assets, BattleNativeAction action)
    {
        const byte selphie = 5;
        Assert.Contains(action, assets.NativeActions);
        using GameWorld world = StartBattle(directory, field, 22, limit: action with { CharacterId = selphie });
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == selphie);
        for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);
        Assert.Null(battle.ActiveAction);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        Assert.True(hud.NativeLimitAvailable);
        hud.SetNativeCommandButtons(0x2000);
        Assert.Equal(BattleAbility.LimitBreak, hud.Commands[0]);
        hud.Choose(0);

        bool sawUnavailable = false;
        for (int roll = 0; roll < 128 && battle.GetNativeSlotsRoll(caster.Id)?.MagicId != action.ActionId; roll++)
        {
            Assert.False(Assert.Single(hud.ActionChoices).Available);
            sawUnavailable = true;
            hud.RerollNativeSlots();
        }
        Assert.True(sawUnavailable);
        BattleNativeSlotsRoll accepted = Assert.IsType<BattleNativeSlotsRoll>(battle.GetNativeSlotsRoll(caster.Id));
        Assert.Equal(action.ActionId, accepted.MagicId);
        Assert.Equal(1, accepted.Repetitions);
        Assert.True(Assert.Single(hud.ActionChoices).Available);

        string[] targets = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsAlive && !actor.Untargetable).Select(actor => actor.Id).Order().ToArray();
        Assert.Equal(11, targets.Length);
        string itemsBefore = JsonSerializer.Serialize(battle.Items);
        string magicBefore = JsonSerializer.Serialize(caster.MagicStocks);
        int start = battle.Events.Count;
        hud.Navigate(MenuInput.Down);
        hud.Attack();
        BattleAttackCommand command = Assert.Single(battle.QueuedActions);
        Assert.Equal(BattleAbility.LimitBreak, command.Ability);
        Assert.Equal(action.ActionId, command.NativeAction?.ActionId);
        Assert.Equal(action.MagicId, command.NativeAction?.MagicId);
        Assert.Equal(0, command.NativeTargetMask);
        Assert.Equal(accepted, command.NativeSlots);
        Assert.Equal(0, battle.NativeScriptState.LimitSpecialSetMask);

        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 300 && caster.IsBusy; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.False(caster.IsBusy);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(4, battle.NativeScriptState.LimitSpecialSetMask);
        Assert.Equal(itemsBefore, JsonSerializer.Serialize(battle.Items));
        Assert.Equal(magicBefore, JsonSerializer.Serialize(caster.MagicStocks));
        Assert.Equal(targets, battle.Events.Skip(start).Where(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.Ability == BattleAbility.LimitBreak).Select(entry => entry.TargetId).Order());
        Assert.All(battle.Combatants.Where(actor => targets.Contains(actor.Id)), actor => Assert.True(actor.IsEjected));
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        return ("logical", $"Slots cast the installed owned Rapture roll, ejected {targets.Length} roster enemies, and recorded its special magic flag without consuming stock.");
    }
}
