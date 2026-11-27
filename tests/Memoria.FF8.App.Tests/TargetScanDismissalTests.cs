using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
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
        BattleSession battle = OpenScan(world, scan);
        TargetScanDismissal dismissal = new();
        InputFrame input = new() { Confirm = confirm, Cancel = !confirm };
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

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

    [Fact]
    public void F1_toggles_speed_while_the_scan_viewer_stays_open()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_GRAPHICS_HOST_TEST") == "1", "An installed-game graphics host is required.");
        string gameDirectory = TestResources.GameDirectory;
        using BattleAssets assets = new(gameDirectory);
        BattleActionDefinition scan = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.NativeAction is { AttackType: 12, MagicId: 40 });
        GameState field = ExpandedBattleEffectMatrixTests.PrepareField(gameDirectory, GameWorld.CreateColdProcess(gameDirectory).State.NativeBattleMemory);
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(gameDirectory, field, 338, scan, additionalEnemies: 0);
        BattleSession battle = OpenScan(world, scan);
        DirectoryInfo directory = Directory.CreateTempSubdirectory("memoria-scan-speed-");
        try
        {
            LaunchOptions options = new()
            {
                GameDirectory = gameDirectory,
                MuteAudio = true,
                AchievementsLocalOnly = true,
                Width = 640,
                Height = 480,
                ResolutionSpecified = true,
                SettingsDirectory = directory.FullName,
                SavePath = Path.Combine(directory.FullName, "unused.json")
            };
            using RecreateGame game = new(options, GameSession.CreateColdProcess());
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            KeyboardState keys = default;
            typeof(RecreateGame).GetField("physicalInput", flags)!.SetValue(game, new HostPhysicalInput(true, () => true, () => keys, () => default, () => default));
            game.RunOneFrame();
            typeof(RecreateGame).GetMethod("EndStartup", flags)!.Invoke(game, null);
            typeof(RecreateGame).GetField("debugBattle", flags)!.SetValue(game, battle);
            game.RunOneFrame();
            Assert.True(ReferenceEquals(battle, typeof(RecreateGame).GetField("presentedBattle", flags)!.GetValue(game)));
            Assert.NotNull(Ff8TargetScan.VisibleActor(battle.PreparedPresentation?.Scan));
            GameplayClock clock = (GameplayClock)typeof(RecreateGame).GetField("gameplayClock", flags)!.GetValue(game)!;

            Assert.Equal(1, clock.SpeedMultiplier);
            keys = new(Keys.F1);
            game.RunOneFrame();
            Assert.Equal(3, clock.SpeedMultiplier);
            game.RunOneFrame();
            Assert.Equal(3, clock.SpeedMultiplier);
            keys = default;
            game.RunOneFrame();
            keys = new(Keys.F1);
            game.RunOneFrame();
            Assert.Equal(1, clock.SpeedMultiplier);
            Assert.NotNull(Ff8TargetScan.VisibleActor(battle.PreparedPresentation?.Scan));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static BattleSession OpenScan(GameWorld world, BattleActionDefinition scan)
    {
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
        for (int frame = 0; frame < 480 && Ff8TargetScan.VisibleActor(battle.PreparedPresentation?.Scan) is null; frame++)
        {
            battle.Tick();
        }
        Assert.True(Ff8TargetScan.VisibleActor(battle.PreparedPresentation?.Scan) is not null, $"Scan reached callback {battle.NativeHostCallbacks}, actor {battle.PreparedPresentation?.Scan?.Actor?.Name}, input ready {battle.CanQueueNativeScanInput}.");
        return battle;
    }
}
