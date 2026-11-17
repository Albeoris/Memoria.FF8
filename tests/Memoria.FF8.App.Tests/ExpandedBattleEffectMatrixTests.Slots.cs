using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(53)]
    [InlineData(56)]
    public void Installed_destructive_slots_casts_the_owned_roll_without_stock(ushort actionId)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row is { KernelSection: 1, Command: 16 } && row.ActionId == actionId);
        (string status, string detail) = RunExpandedDestructiveSlots(directory, PrepareField(directory), assets, action);
        Assert.True(status == "logical", detail);
    }

    private static (string Status, string Detail) RunExpandedDestructiveSlots(string directory, GameState field, BattleAssets assets, BattleNativeAction action)
    {
        const byte selphie = 5;
        Assert.Contains(action, assets.NativeActions);
        foreach (byte seed in new byte[] { 7, 11, 12, 13 })
        {
            using GameWorld world = StartBattle(directory, field, 22, limit: action with { CharacterId = selphie }, nativeSeed: seed);
            if (TryExpandedDestructiveSlots(world, action, selphie) is { } result)
            {
                return result;
            }
        }
        throw new InvalidOperationException($"The original Slots rolls did not yield {action.Name} for the installed encounter seeds.");
    }

    private static (string Status, string Detail)? TryExpandedDestructiveSlots(GameWorld world, BattleNativeAction action, byte selphie)
    {
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == selphie);
        for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        if (!caster.IsReady || battle.ActiveAction is not null)
        {
            return null;
        }
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        if (!hud.NativeLimitAvailable)
        {
            return null;
        }
        hud.SetNativeCommandButtons(0x2000);
        Assert.Equal(BattleAbility.LimitBreak, hud.Commands[0]);
        hud.Choose(0);

        bool sawUnavailable = false;
        for (int roll = 0; roll < 128 && battle.GetNativeSlotsRoll(caster.Id)?.MagicId != action.ActionId; roll++)
        {
            sawUnavailable |= !Assert.Single(hud.ActionChoices).Available;
            hud.RerollNativeSlots();
        }
        BattleNativeSlotsRoll accepted = Assert.IsType<BattleNativeSlotsRoll>(battle.GetNativeSlotsRoll(caster.Id));
        if (accepted.MagicId != action.ActionId || !sawUnavailable)
        {
            return null;
        }
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
        Assert.IsNotType<NativeBattleTargetRequestAdapter.DeferredOperands>(command.TargetRequest.Selection);
        Assert.Equal(accepted, command.NativeSlots);
        Assert.Equal(0, battle.NativeScriptState.LimitSpecialSetMask);

        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleModelOwner casterModel = battle.ReadPresentationModelOwner(caster.Id);
        NativeBattleActionHeaderSlot? header = null;
        for (int callback = 0; callback < 900 && (caster.IsBusy || process.HasActiveAction); callback++)
        {
            battle.Tick();
            header ??= process.ActionPackets.PublishedHeaders().Select(entry => entry.Slot).FirstOrDefault(slot => slot.Source == casterModel && slot.Command == 16);
        }
        Assert.False(caster.IsBusy, string.Join(", ", battle.Events.Skip(start).TakeLast(15).Select(entry => $"{entry.Kind}/{entry.ActorId}/{entry.TargetId}")));
        Assert.False(process.HasActiveAction);
        NativeBattleActionHeaderSlot published = Assert.IsType<NativeBattleActionHeaderSlot>(header);
        Assert.Equal(action.ActionId, published.Action);
        Assert.Equal(action.MagicId, published.Effect);
        Assert.Equal(targets.Length, published.ReadContactCount());
        Assert.Equal(1 << (action.ActionId - 51), battle.NativeScriptState.LimitSpecialSetMask);
        Assert.Equal(itemsBefore, JsonSerializer.Serialize(battle.Items));
        Assert.Equal(magicBefore, JsonSerializer.Serialize(caster.MagicStocks));
        Assert.Equal(targets, battle.Events.Skip(start).Where(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.Ability == BattleAbility.LimitBreak).Select(entry => entry.TargetId).Order());
        Assert.All(battle.Combatants.Where(actor => targets.Contains(actor.Id)), actor => Assert.True(action.Statuses0 == 1 ? !actor.IsAlive : actor.IsEjected));
        return ("logical", $"Slots cast the installed {action.Name} roll, affected {targets.Length} roster enemies and recorded its special magic flag without consuming stock.");
    }
}
