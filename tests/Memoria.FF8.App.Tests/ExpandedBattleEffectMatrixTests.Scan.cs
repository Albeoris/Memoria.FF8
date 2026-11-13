using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Fact]
    public void InstalledScanShowsTargetInformationAndPreservesHistoryAcrossJsonRestore()
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleActionDefinition scan = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.NativeAction is { AttackType: 12, MagicId: 40 });
        using GameWorld world = StartBattle(directory, PrepareField(directory), 0, scan);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        BattleCombatant target = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-10");
        int targetHp = target.CurrentHp;
        BattleSnapshot before = battle.Capture();
        Assert.NotNull(before.NativeScannedModels);
        Assert.False(before.NativeScannedModels.Contains(target.ModelId));
        Assert.True(battle.SupportsExpandedAction(scan));
        caster.Atb = BattleSession.AtbMaximum;
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Magic));
        hud.Choose(hud.ActionChoices.ToList().FindIndex(action => action.Id == scan.Id));
        Assert.Contains(target, hud.Targets);
        hud.SelectTarget(target.Id, false);
        hud.Attack();
        Assert.True(caster.IsBusy);
        for (int frame = 0; frame < 120 && caster.IsBusy; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }

        Assert.False(caster.IsBusy);
        Assert.Null(battle.ExpandedEffect);
        Assert.Equal(InitialStock - 1, Assert.Single(caster.MagicStocks, stock => stock.Id == scan.Id).Count);
        Assert.Equal(targetHp, target.CurrentHp);
        BattleEvent notification = Assert.Single(battle.Events, entry => entry.Kind == "Scan" && entry.ActorId == caster.Id);
        Assert.Equal(target.Id, notification.TargetId);
        Assert.Contains(target.Name, notification.Message);
        Assert.Contains($"{targetHp}/{target.MaximumHp}", notification.Message);
        Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.TargetId == target.Id && entry.Amount == 0);
        hud.BuildView();
        Assert.Equal(notification.Message, hud.Message);

        BattleScanObservation known = Assert.Single(battle.ScanKnowledge.Observations);
        Assert.Equal((BattleObserverSide.Party, target.Id, caster.Id), (known.Observer, known.TargetId, known.ScannerId));
        Assert.Equal(target.ElementDefenses.Select(BattleKnownAffinity.FromNativeDefense), known.Affinities);
        BattleSnapshot after = battle.Capture();
        Assert.True(after.NativeScannedModels!.Contains(target.ModelId));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(after))!);
        Assert.True(restored.Capture().NativeScannedModels!.SameValue(after.NativeScannedModels));
        Assert.Equal(known.Affinities, Assert.Single(restored.ScanKnowledge.Observations).Affinities);
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
