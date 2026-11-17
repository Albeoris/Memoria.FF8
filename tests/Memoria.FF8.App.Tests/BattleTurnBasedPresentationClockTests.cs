using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTurnBasedPresentationClockTests
{
    [Fact]
    public void Sixty_host_updates_per_second_deliver_original_callbacks_without_advancing_an_idle_turn()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        ExpandedBattleInstalledEntry.Prepare(world, session, _ => { }, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: ExpandedBattleInstalledEntry.Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleScenePresentation scene = new(world.BattleResources, battle.ReadDefinition());
        byte[] initialPixels = scene.Render(battle, 320, 224).ToArray();
        long initialCallback = process.Graphics.Callback;
        long initialFrame = battle.Frame;
        GameplayClock clock = new();

        AdvanceTwoSeconds();

        Assert.Equal(initialCallback + 31, process.Graphics.Callback);
        Assert.Equal(initialFrame, battle.Frame);
        Assert.False(initialPixels.SequenceEqual(scene.Render(battle, 320, 224)));
        Assert.Same(process.PreparedPresentation, scene.PresentedNativeFrame);

        clock.Configure(fastForward: 2, battlePercent: 100);
        clock.ToggleSpeed();
        AdvanceTwoSeconds();

        Assert.Equal(initialCallback + 93, process.Graphics.Callback);
        Assert.Equal(initialFrame, battle.Frame);
        Assert.Equal(TimeSpan.FromSeconds(6), clock.Total);

        void AdvanceTwoSeconds()
        {
            const int updatesPerSecond = 60;
            for (int update = 0; update < 2 * updatesPerSecond; update++)
            {
                long before = update * TimeSpan.TicksPerSecond / updatesPerSecond;
                long after = (update + 1) * TimeSpan.TicksPerSecond / updatesPerSecond;
                TimeSpan elapsed = clock.Advance(TimeSpan.FromTicks(after - before), SceneKind.Field, battle: true);
                battle.AdvanceNativeGraphicsPresentation(elapsed);
            }
        }
    }
}
