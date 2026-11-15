using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Fact]
    public void Installed_expanded_MiniMog_heals_junctioned_guardians_and_charges_the_party_once()
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleNativeAction dance = Assert.Single(assets.NativeActions, action => action is { KernelSection: 9, Command: 240, ActionId: 6 });
        (string status, string detail) = RunExpandedMiniMog(directory, PrepareField(directory), dance);
        Assert.True(status == "logical", detail);
    }

    private static (string Status, string Detail) RunExpandedMiniMog(string directory, GameState field, BattleNativeAction dance)
    {
        const int commandKey = (38 << 16) | 255;
        const byte pocketLevel = 20;
        NativePocketStationState pocket = Assert.IsType<NativePocketStationState>(field.PocketStation);
        NativeEconomy wallet = Assert.IsType<NativeEconomy>(field.Economy);
        field = field with
        {
            PocketStation = pocket with { Raw = pocket.Raw.SetItem(0, 0x0D).SetItem(1, pocketLevel) },
            Economy = wallet with { Gil = 10000 }
        };
        using GameWorld world = StartBattle(directory, field, 71, prepareVitals: vitals =>
        {
            vitals = vitals.AcquireGuardianForce(0).AcquireGuardianForce(1).ToggleGuardianForceJunction(0, 0).ToggleGuardianForceJunction(1, 1);
            vitals = vitals.TeachGuardianForceAbility(0, 38).SetJunctionAbility(0, 0, 38, true);
            NativeGuardianForceVital[] forces = vitals.GuardianForces.ToArray();
            forces[0] = forces[0] with { HitPoints = 1 };
            forces[1] = forces[1] with { HitPoints = 1 };
            return vitals with { GuardianForces = forces };
        });
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == "party-0");
        for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);
        Assert.Null(battle.ActiveAction);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        int choice = hud.Commands.ToList().IndexOf(BattleAbility.NativeCommand);
        Assert.InRange(choice, 0, hud.Commands.Count - 1);
        hud.Choose(choice);
        Assert.True(hud.SelectingTargetGroup);
        Assert.Equal(11, hud.Targets.Count);
        string[] expected = battle.Combatants.Where(actor => actor.IsAlive && !actor.Untargetable).Select(actor => actor.Id).ToArray();
        Assert.Equal(22, expected.Length);
        int first = battle.Events.Count;
        hud.Attack();
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions, action => action.ActorId == caster.Id);
        Assert.Equal(commandKey, queued.ActionId);
        Assert.Equal(dance.MagicId, queued.NativeAction!.MagicId);
        Assert.IsNotType<NativeBattleTargetRequestAdapter.DeferredOperands>(queued.TargetRequest.Selection);
        BattleSnapshot before = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);
        for (int frame = 0; frame < 300 && caster.IsBusy; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            restored.Tick();
        }
        Assert.False(caster.IsBusy);
        BattleEvent[] receipts = battle.Events.Skip(first).Where(entry => entry.ActorId == caster.Id).ToArray();
        Assert.Single(receipts, entry => entry.Kind == "ActionComplete");
        Assert.Equal(expected, receipts.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.TargetId));
        Assert.Equal(new[] { "party-0", "party-1" }, receipts.Where(entry => entry.Kind == "GuardianForceHealing").Select(entry => entry.TargetId));
        foreach (BattleGuardianForceVital force in battle.GuardianForces.Where(force => force.Id is 0 or 1))
        {
            Assert.Equal(1 + pocketLevel * force.MaximumHitPoints / 100, (int)force.HitPoints);
        }
        uint cost = checked((uint)(100 * (battle.Combatants.Where(actor => actor.IsPlayer).Sum(actor => actor.Level) / 11)));
        Assert.Equal(new NativeBattleGil(before.NativeGil!.Gil - cost, before.NativeGil.PastGil), battle.Capture().NativeGil);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        return ("logical", $"Equipped MiniMog contacted {expected.Length} combatants, healed two junctioned GFs and spent {cost} gil once.");
    }
}
