using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TargetScanDismissalTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void A_single_press_survives_multiple_callbacks_and_is_cleared_on_exit(bool confirm, bool cancel, bool pointer)
    {
        TargetScanDismissal dismissal = new();
        InputFrame press = new() { Confirm = confirm, Cancel = cancel };

        Assert.Equal(0x2040, dismissal.Sample(0x2000, visible: true, exiting: false, press, pointer));
        Assert.Equal(0x2040, dismissal.Sample(0x2000, visible: true, exiting: false, default, false));
        Assert.Equal(0x2000, dismissal.Sample(0x2040, visible: true, exiting: true, default, false));
        Assert.Equal(0x2040, dismissal.Sample(0x2040, visible: false, exiting: false, default, false));
        Assert.Equal(0x2000, dismissal.Sample(0x2040, visible: true, exiting: false, default, false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void One_button_press_closes_the_installed_scan_viewer(bool confirm)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the original FFVIII installation.");
        using BattleAssets assets = new(directory!);
        BattleActionDefinition scan = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.NativeAction is { AttackType: 12, MagicId: 40 });
        GameState field = ExpandedBattleEffectMatrixTests.PrepareField(directory!, GameWorld.CreateColdProcess(directory!).State.NativeBattleMemory);
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory!, field, 338, scan, additionalEnemies: 0);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == "party-10");
        BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.ModelId == 53);
        for (int frame = 0; frame < 480 && !battle.NativeEntryReady; frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        caster.Atb = BattleSession.AtbMaximum;
        battle.QueueAction(caster.Id, target.Id, BattleAbility.Magic, scan.Id);
        TargetScanDismissal dismissal = new();
        InputFrame input = new() { Confirm = confirm, Cancel = !confirm };
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        for (int frame = 0; frame < 480 && Ff8TargetScan.VisibleActor(battle.PreparedPresentation?.Scan) is null; frame++)
        {
            battle.Tick();
        }
        Assert.True(Ff8TargetScan.VisibleActor(battle.PreparedPresentation?.Scan) is not null, $"Scan reached callback {battle.NativeHostCallbacks}, actor {battle.PreparedPresentation?.Scan?.Actor?.Name}, input ready {battle.CanQueueNativeScanInput}.");

        bool exitObserved = false;
        for (int frame = 0; frame < 60; frame++)
        {
            bool visible = Ff8TargetScan.VisibleActor(battle.PreparedPresentation?.Scan) is not null;
            bool exiting = battle.Presentation?.Memory?.Scan?.Presentation.Exit269CE2C is > 0;
            exitObserved |= exiting;
            if (!visible && exitObserved)
            {
                break;
            }
            battle.PublishNativeScanMappedButtons(dismissal.Sample(0, visible, exiting, input, false));
            battle.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            input = default;
        }

        Assert.True(exitObserved);
        Assert.Null(Ff8TargetScan.VisibleActor(battle.PreparedPresentation?.Scan));
    }
}
