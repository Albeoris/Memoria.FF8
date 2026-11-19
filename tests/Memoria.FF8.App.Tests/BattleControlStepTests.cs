using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleControlStepTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Wall_time_advances_native_battle_only_outside_control_step_and_explicit_ticks_always_advance(bool controlStep)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        using BattleAssets battleAssets = new(assets.GameDirectory);
        BattleSession battle = InstalledBattlePractice.Create(new BattleDefinition
        {
            StageId = battleAssets.StageIds.First(),
            Combatants =
            [
                new() { Id = "player", Name = "Player", IsPlayer = true, ModelId = 0 },
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, MaximumHp = 1000, CurrentHp = 1000 }
            ]
        }, BattleAtbMode.TurnBased, assets, battleAssets);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: "player");
        string temporary = Path.Combine(Path.GetTempPath(), "memoria-battle-control-" + Guid.NewGuid().ToString("N"));
        try
        {
            using RecreateGame game = new(new LaunchOptions { GameDirectory = assets.GameDirectory, ControlStep = controlStep, ControlExclusive = true, MuteAudio = true, AchievementsLocalOnly = true, SettingsDirectory = temporary, SavePath = Path.Combine(temporary, "unused.ff8save") }, new(new GameState()));
            typeof(RecreateGame).GetField("debugBattle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, battle);
            MethodInfo wallTime = typeof(RecreateGame).GetMethod("AdvanceMeasuredBattlePresentation", BindingFlags.Instance | BindingFlags.NonPublic)!;
            MethodInfo command = typeof(RecreateGame).GetMethod("ApplyBattleCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
            long initialCallback = battle.Presentation!.NativeGraphics!.Callback;
            wallTime.Invoke(game, [battle, TimeSpan.FromSeconds(0.2)]);
            long afterWallTime = battle.Presentation!.NativeGraphics!.Callback;
            if (controlStep)
            {
                Assert.Equal(initialCallback, afterWallTime);
            }
            else
            {
                Assert.True(afterWallTime > initialCallback);
            }
            for (int callback = 0; callback < 1800 && !battle.NativeEntryReady; callback++)
            {
                command.Invoke(game, [new BattleInput(BattleInputKind.Tick)]);
            }
            Assert.True(battle.NativeEntryReady);
            BattleScenePresentation scene = new(battleAssets, battle.Capture().Definition);
            typeof(RecreateGame).GetField("uiPreferences", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, new Memoria.FF8.GameUi.UiPreferences { BattleFps = 60 });
            MethodInfo renderPhase = typeof(RecreateGame).GetMethod("ReadBattleRenderPhase", BindingFlags.Instance | BindingFlags.NonPublic)!;
            NativeBattlePreparedPresentation earlier = battle.PreparedPresentation!;
            float phase = (float)renderPhase.Invoke(game, [battle, scene])!;
            scene.Render(battle, 320, 216, triangleRenderer: null, presentationPhase: phase);
            command.Invoke(game, [new BattleInput(BattleInputKind.Tick)]);
            NativeBattlePreparedPresentation current = battle.PreparedPresentation!;
            Assert.Equal(earlier.Callback + 1, current.Callback);
            phase = (float)renderPhase.Invoke(game, [battle, scene])!;
            scene.Render(battle, 320, 216, triangleRenderer: null, presentationPhase: phase);
            Assert.Equal(!controlStep, scene.SmoothCompatibleMotion);
            Assert.Same(controlStep ? current : earlier, scene.PresentedNativeFrame);
            Assert.Equal(controlStep ? null : (long?)current.Callback, scene.PresentedNextNativeCallback);
            Assert.Equal(0, scene.PresentedInterpolationAlpha);
            afterWallTime = battle.Presentation!.NativeGraphics!.Callback;
            command.Invoke(game, [new BattleInput(BattleInputKind.Tick)]);
            long afterExplicitTick = battle.Presentation!.NativeGraphics!.Callback;
            Assert.True(afterExplicitTick > afterWallTime);
            wallTime.Invoke(game, [battle, TimeSpan.FromSeconds(0.2)]);
            if (controlStep)
            {
                Assert.Equal(afterExplicitTick, battle.Presentation!.NativeGraphics!.Callback);
            }
            else
            {
                Assert.True(battle.Presentation!.NativeGraphics!.Callback > afterExplicitTick);
            }
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
    }
}
