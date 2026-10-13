using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Selected original-world component fixtures; these do not advance or establish a natural player exit.</summary>
public sealed class WorldMapSceneTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void World_callbacks_preserve_hidden_or_displayed_timer_and_replay_the_same_proposals(bool displayed)
    {
        var session = ComponentSession();
        session.StartNewGame();
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(600).Display(250, 10);
        if (!displayed)
        {
            timer = timer.KillDisplay();
        }
        session.SetExamTimer(timer);
        using var scene = new WorldMapScene(session, GameDirectory());
        GameState initial = session.State;
        Assert.Equal(0, session.ExamTimer!.ClockCalls);
        Assert.Equal(3, scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3)).FramesAdvanced);
        NativeExamTimerState expected = timer.AdvanceCallbacks(NativeTimerCallbackKind.World, 3);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.ExamTimer));
        ReplayJournalEntry entry = session.ReplayJournal!.Endpoint!;
        var replaySession = new GameSession(initial);
        using var replay = new WorldMapScene(replaySession, GameDirectory());
        Assert.Equal(3, replay.ReplayNavigation(entry).FramesAdvanced);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(replaySession.State)));
        Assert.False(scene.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.ExamTimer));
    }

    [Fact]
    public void Refused_world_batch_preserves_timer_before_any_candidate_callback_is_committed()
    {
        var session = ComponentSession();
        session.SetExamTimer(NativeExamTimerState.FromReachedSet(600).Display(250, 10).KillDisplay());
        using var scene = new WorldMapScene(session, GameDirectory());
        session.SetWorldState(scene.State with { X = -5000, CameraTargetX = -1476, CameraYaw = 0, CameraVelocity = 0 });
        GameState before = session.State;
        Assert.Throws<NotSupportedException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2)));
        Assert.Same(before, session.State);
        Assert.Equal(0, session.ExamTimer!.ClockCalls);
    }

    [Fact]
    public void OriginalSpawnAndFirstGroundUpdatePreserveNativeHeightRounding()
    {
        string directory = GameDirectory();
        var session = ComponentSession();
        using var scene = new WorldMapScene(session, directory);
        Assert.Equal(30.5, WorldMapScene.SimulationFps);
        Assert.Equal((24576, -29406, -658), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal((176, 2048), (scene.State.CameraYaw, scene.State.BodyYaw));
        Assert.Equal((24576, -29406, -914), (scene.State.CameraTargetX, scene.State.CameraTargetZ, scene.State.CameraTargetNativeHeight));
        Assert.Equal((byte)28, scene.State.GroundType);
        Assert.Equal((ushort)285, scene.State.ReplacementMask);
        Assert.Empty(session.State.Variables);
        Assert.Empty(scene.State.Actions);

        PlayerActionResult result = scene.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.True(result.Accepted);
        Assert.Equal(1, result.FramesAdvanced);
        Assert.Equal((24576, -29406, -657), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal(0, scene.State.AnimationId);
        Assert.Equal(1, scene.State.Frame);
    }

    [Fact]
    public void CardinalMovementUsesOriginalFiveGroundProbesAndPreCollisionCameraTarget()
    {
        string directory = GameDirectory();
        var session = ComponentSession();
        using var scene = new WorldMapScene(session, directory);
        session.SetWorldState(scene.State with { CameraYaw = 0, BodyYaw = 1024 });

        PlayerActionResult move = scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        Assert.True(move.Accepted);
        Assert.Equal((24607, -29406, -657), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal((1, 0), (scene.State.AnimationId, scene.State.AnimationCounter));
        Assert.Equal((1, 8), (scene.State.CameraYaw, scene.State.CameraVelocity));
        Assert.Equal(24576, scene.State.CameraTargetX);
        Assert.Equal((byte)28, scene.State.GroundType);

        scene.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.Equal((0, 0), (scene.State.AnimationId, scene.State.AnimationCounter));
        Assert.Equal(24591, scene.State.CameraTargetX);
        Assert.Equal((1, 6), (scene.State.CameraYaw, scene.State.CameraVelocity));
        Assert.Equal(2, scene.State.Actions.Count);
    }

    [Fact]
    public void SelectedWorldStateSaveReloadPreservesCameraAnimationActionsAndPixels()
    {
        string directory = GameDirectory();
        string checkpoint = Path.Combine(Path.GetTempPath(), "Memoria.FF8-world-component-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var originalSession = ComponentSession();
            using var original = new WorldMapScene(originalSession, directory);
            original.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
            original.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2));
            originalSession.Save(checkpoint);

            var resumedSession = new GameSession(GameSession.Load(checkpoint));
            using var resumed = new WorldMapScene(resumedSession, directory);
            Assert.Equal(JsonSerializer.Serialize(original.State), JsonSerializer.Serialize(resumed.State));
            Assert.Equal(originalSession.State.WorldArrival, resumedSession.State.WorldArrival);
            Assert.Empty(resumedSession.State.Variables);

            var next = new PlayerAction(PlayerActionKind.Move, Vertical: -1, Frames: 2);
            Assert.Equal(original.Apply(next), resumed.Apply(next));
            Assert.Equal(JsonSerializer.Serialize(original.State), JsonSerializer.Serialize(resumed.State));
            byte[] pixels = original.Render(320, 224);
            Assert.Equal(320 * 224 * 4, pixels.Length);
            Assert.Equal(pixels, resumed.Render(320, 224));
            Assert.Contains(pixels.Where((value, index) => index % 4 != 3), value => value != 0);
        }
        finally
        {
            File.Delete(checkpoint);
        }
    }

    [Fact]
    public void LaterBatchRefusalPreservesEarlierCandidateTicksAndEntireSession()
    {
        string directory = GameDirectory();
        var session = ComponentSession();
        using var scene = new WorldMapScene(session, directory);
        session.SetWorldState(scene.State with { X = -5000, CameraTargetX = -1476, CameraYaw = 0, CameraVelocity = 0 });
        var singleSession = new GameSession(session.State);
        using var single = new WorldMapScene(singleSession, directory);
        Assert.True(single.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(-3238, single.State.CameraTargetX);
        Assert.Throws<NotSupportedException>(() => single.Apply(new PlayerAction(PlayerActionKind.Wait)));

        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<NotSupportedException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2)));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void SuccessfulBatchMatchesIndividualTicksWithOneRecordedAction()
    {
        string directory = GameDirectory();
        var batchSession = ComponentSession();
        using var batch = new WorldMapScene(batchSession, directory);
        var singleSession = new GameSession(batchSession.State);
        using var single = new WorldMapScene(singleSession, directory);
        Assert.Equal(3, batch.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3)).FramesAdvanced);
        for (int tick = 0; tick < 3; tick++)
        {
            Assert.True(single.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        }
        Assert.Single(batch.State.Actions);
        Assert.Equal(3, single.State.Actions.Count);
        Assert.Equal(JsonSerializer.Serialize(single.State with { Actions = [] }), JsonSerializer.Serialize(batch.State with { Actions = [] }));
    }

    [Fact]
    public void ContactRegionRefusalPreservesAllWorldStateAndActions()
    {
        var session = ComponentSession();
        using var scene = new WorldMapScene(session, GameDirectory());
        session.SetWorldState(scene.State with { X = -5000, CameraTargetX = -3238, CameraYaw = 0, CameraVelocity = 0 });
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<NotSupportedException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void UnsupportedEntityTypeIsRejectedBeforeLoadingWorldAssets()
    {
        var session = new GameSession(new GameState { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 1, 0) });
        Assert.Throws<NotSupportedException>(() => new WorldMapScene(session, "missing world corpus"));
    }

    [Fact]
    public void WorldHostReloadKeepsDepartingFieldVmFrozenWhileWorldMovementContinues()
    {
        string directory = GameDirectory();
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var firstSession = new GameSession(GameSession.Load(source));
        using var first = new GameWorld(firstSession, directory);
        first.EnsureField();
        string departingVm = first.Events.Capture().GetRawText();
        firstSession.SetEventState(first.Events.Capture());
        // Isolated host fixture: this direct service request establishes no natural player entry.
        firstSession.Restore(firstSession.State with { SavedWorld = NativeSavedWorldState.CreateNewGame() });
        firstSession.RequestWorld(new WorldTransition(0, 0, 0));
        firstSession.SetDepartingFieldState(first.Events.Capture());
        Assert.True(first.EnsureField());
        Assert.Null(firstSession.State.DepartingFieldState);
        Assert.Equal(PlayerTriggerKind.WorldMap, first.NextTrigger.Kind);
        first.SetRenderSize(320, 224);
        first.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
        Assert.Equal(departingVm, first.Events.Capture().GetRawText());

        string checkpoint = Path.Combine(Path.GetTempPath(), "Memoria.FF8-world-host-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            firstSession.Save(checkpoint);
            var resumedSession = new GameSession(GameSession.Load(checkpoint));
            using var resumed = new GameWorld(resumedSession, directory);
            Assert.True(resumed.EnsureField());
            resumed.SetRenderSize(320, 224);
            Assert.Equal(departingVm, resumed.Events.Capture().GetRawText());
            Assert.Equal(JsonSerializer.Serialize(firstSession.State.World), JsonSerializer.Serialize(resumedSession.State.World));
            var next = new PlayerAction(PlayerActionKind.Move, Vertical: -1, Frames: 2);
            Assert.Equal(first.Apply(next), resumed.Apply(next));
            Assert.Equal(JsonSerializer.Serialize(firstSession.State.World), JsonSerializer.Serialize(resumedSession.State.World));
            Assert.Equal(first.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            Assert.Equal(departingVm, resumed.Events.Capture().GetRawText());
        }
        finally
        {
            File.Delete(checkpoint);
        }
    }

    private static GameSession ComponentSession()
    {
        return new GameSession(new GameState { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0), SavedWorld = NativeSavedWorldState.CreateNewGame() });
    }

    private static string GameDirectory()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world corpus is required for this isolated component fixture.");
        return directory;
    }
}
