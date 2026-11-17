using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayStopBoundaryTests
{
    [Fact]
    public void Deferred_stop_returns_once_and_saves_before_another_exploration_step()
    {
        using StopFixture fixture = new();
        BattleSession battle = fixture.StartBattle();
        for (int callback = 0; callback < 1200 && !battle.CanAdmitDebugOutcome; callback++)
        {
            fixture.World.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(battle.CanAdmitDebugOutcome);
        fixture.World.ApplyBattleInput(new(BattleInputKind.InstantVictory));
        Assert.False(battle.CanReturnToField);
        fixture.Controller.SaveAndStop();
        Assert.True(fixture.Controller.Running);
        Assert.True(fixture.Controller.WaitingToStopAfterBattle);
        Assert.Contains("Finishing current battle", fixture.Controller.Status);

        for (int update = 0; update < 1200 && fixture.Controller.Running; update++)
        {
            fixture.Controller.Advance(256);
        }
        Assert.Null(fixture.Controller.Error);
        Assert.False(fixture.Controller.Running);
        Assert.False(fixture.Controller.WaitingToStopAfterBattle);
        Assert.Null(fixture.World.Battle);
        Assert.Single(fixture.Session.State.AppliedBattleResultIds);
        Assert.Null(fixture.Session.State.PendingBattleEncounter);
        Assert.StartsWith("stopped-", Path.GetFileName(fixture.Controller.ReproductionPath!));
        (GameState saved, long savedSteps) = LoadManifest(fixture.Controller.ReproductionPath!);
        Assert.Null(saved.Battle);
        Assert.Null(saved.PendingBattleEncounter);
        Assert.Equal(fixture.Session.State.AppliedBattleResultIds, saved.AppliedBattleResultIds);
        Assert.Equal(fixture.Controller.State.Steps, savedSteps);

        long expected = fixture.Controller.State.Steps;
        fixture.Controller.Advance(256);
        Assert.Equal(expected, fixture.Controller.State.Steps);
        string currentRun = Path.GetDirectoryName(fixture.Controller.ReproductionPath!)!;
        Assert.Single(Directory.GetFiles(currentRun, "stopped-*.run.json"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Immediate_stop_or_battle_failure_keeps_the_last_durable_boundary(bool failure)
    {
        using StopFixture fixture = new();
        BattleSession battle = fixture.StartBattle();
        long frame = battle.Frame;
        fixture.Controller.State.Steps = 9;
        Assert.Throws<InvalidOperationException>(fixture.Controller.StartFromCurrentState);
        Assert.Throws<InvalidOperationException>(() => fixture.Controller.CaptureState());

        if (failure)
        {
            fixture.Controller.ReportHostFailure(new InvalidOperationException("Battle host failed."));
            Assert.Contains("Battle host failed.", fixture.Controller.Error);
        }
        else
        {
            fixture.Controller.SaveAndStop(AutoPlayStopBoundary.Immediately);
            Assert.Null(fixture.Controller.Error);
        }
        Assert.False(fixture.Controller.Running);
        Assert.Equal(frame, battle.Frame);
        Assert.Equal(9, fixture.Controller.State.Steps);
        Assert.Contains("last saved checkpoint before the battle", fixture.Controller.Status);
        Assert.Equal("start.run.json", Path.GetFileName(fixture.Controller.ReproductionPath));
        (GameState saved, long savedSteps) = LoadManifest(fixture.Controller.ReproductionPath!);
        Assert.Null(saved.Battle);
        Assert.Equal(0, savedSteps);
    }

    [Fact]
    public void Stop_from_field_saves_the_current_boundary_immediately()
    {
        using StopFixture fixture = new();
        fixture.Controller.State.Steps = 3;
        fixture.Controller.SaveAndStop();

        Assert.False(fixture.Controller.Running);
        Assert.False(fixture.Controller.WaitingToStopAfterBattle);
        (GameState saved, long savedSteps) = LoadManifest(fixture.Controller.ReproductionPath!);
        Assert.Equal(SceneKind.Field, saved.Scene);
        Assert.Null(saved.Battle);
        Assert.Equal(3, savedSteps);
    }

    private static (GameState Saved, long Steps) LoadManifest(string path)
    {
        AutoPlayRunState manifest = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(path), AutoPlayController.JsonOptions)!;
        return (GameSession.Load(Path.Combine(Path.GetDirectoryName(path)!, manifest.Snapshot!)), manifest.Steps);
    }

    private sealed class StopFixture : IDisposable
    {
        private readonly GameAssets assets;

        public StopFixture()
        {
            string directory = TestResources.GameDirectory;
            assets = new(directory);
            Session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
            Session.SetExamTimer(Session.ExamTimer!.SetRemaining(600));
            World = new(Session, directory, sharedAssets: assets);
            ExpandedBattleInstalledEntry.Prepare(World, Session, _ => { }, allies: 3, additionalEnemies: 0);
            Output = TestResources.ArtifactFile("autoplay-stop");
            Controller = new(Session, () => World, _ => throw new InvalidOperationException("Stopping must keep the current world."), Output,
                deadlineTime: new PausedClock(), fullBattlePlaythroughOverride: true);
            Controller.StartFromCurrentState();
        }

        public GameSession Session { get; }
        public GameWorld World { get; }
        public AutoPlayController Controller { get; }
        public string Output { get; }

        public BattleSession StartBattle() => ExpandedBattleInstalledEntry.Start(World);

        public void Dispose()
        {
            World.Dispose();
            assets.Dispose();
        }
    }

    private sealed class PausedClock : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
