using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Installed_shot_consumes_one_round_through_the_timed_limit(ushort id)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleNativeAction shot = Assert.Single(assets.NativeActions, action => action is { KernelSection: 21, Command: 237 } && action.ActionId == id);
        (string status, string detail) = RunExpandedShot(directory, PrepareField(directory), assets, shot);
        Assert.True(status is "native" or "logical", detail);
    }

    private static (string Status, string Detail) RunExpandedShot(string directory, GameState field, BattleAssets assets, BattleNativeAction shot)
    {
        const byte irvine = 2;
        int ammoId = shot.ActionId + 101;
        NativeEconomy economy = field.Economy!;
        NativeItemStock[] items = economy.Items.ToArray();
        int existing = Array.FindIndex(items, stock => stock.ItemId == ammoId);
        items[existing >= 0 ? existing : Array.FindIndex(items, stock => stock.ItemId == 0)] = new(checked((byte)ammoId), InitialStock);
        field = field with { Economy = economy with { Items = items } };
        BattleNativeAction selection = Assert.Single(assets.NativeActions, action => action.Command == 14 && action.ActionId == shot.ActionId);
        using GameWorld world = StartBattle(directory, field, 89, limit: selection with { CharacterId = irvine });
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == irvine);
        for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        Assert.True(hud.NativeLimitAvailable);
        hud.SetNativeCommandButtons(0x2000);
        Assert.Equal(BattleAbility.LimitBreak, hud.Commands[0]);
        hud.Choose(0);
        int choice = hud.ActionChoices.ToList().FindIndex(action => action.NativeAction?.ActionId == shot.ActionId);
        Assert.InRange(choice, 0, hud.ActionChoices.Count - 1);
        hud.Choose(choice);
        BattleCombatant target = hud.Targets.Last();
        hud.SelectTarget(target.Id, false);
        hud.Attack();
        Until(() => battle.GetNativeShot(caster.Id)?.Phase == BattleNativeShotPhase.Ready);
        Assert.Equal(InitialStock, Stock());
        int firstEvent = battle.Events.Count;
        bool singleTarget = (shot.TargetFlags!.Value & 0x10) != 0;
        string[] expected = singleTarget ? [target.Id] : battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsAlive && actor.IsActive && !actor.Untargetable).Select(actor => actor.Id).Order().ToArray();
        Assert.True(battle.TriggerNativeShot(caster.Id));
        Assert.False(battle.TriggerNativeShot(caster.Id));
        Until(() => battle.GetNativeShot(caster.Id)?.Phase is BattleNativeShotPhase.Ready or null);
        Assert.Equal(InitialStock - 1, Stock());
        Assert.Equal(expected, battle.Events.Skip(firstEvent).Where(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.Ability == BattleAbility.LimitBreak).Select(entry => entry.TargetId).Order());
        battle.EndNativeShot(caster.Id);
        Until(() => battle.GetNativeShot(caster.Id) is null);
        Assert.Equal(InitialStock - 1, Stock());
        return ("logical", $"Shot 237/{shot.ActionId} used its original opening, one trigger and {expected.Length} roster contact(s), ammo {InitialStock}->{Stock()}.");

        int Stock() => battle.Items.Single(stock => stock.Id == ammoId).Count;

        void Until(Func<bool> done)
        {
            for (int frame = 0; frame < 1000 && !done(); frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.True(done());
        }
    }
}
