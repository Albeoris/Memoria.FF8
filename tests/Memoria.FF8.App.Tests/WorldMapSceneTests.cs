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
    public void World_callbacks_preserve_hidden_or_displayed_timer_and_recompute_the_same_native_motion(bool displayed)
    {
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, GameDirectory());
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
        var replaySession = new GameSession(initial);
        using var replay = new WorldMapScene(replaySession, GameDirectory());
        Assert.Equal(3, replay.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3)).FramesAdvanced);
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
    public void Unproved_first_ground_candidate_refuses_before_any_world_tick_or_salary_result()
    {
        string directory = GameDirectory();
        var session = ComponentSession();
        using var scene = new WorldMapScene(session, directory);
        session.SetWorldState(scene.State with { X = -5000, CameraTargetX = -1476, CameraYaw = 0, CameraVelocity = 0 });
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
        Assert.Equal(JsonSerializer.Serialize(single.State), JsonSerializer.Serialize(batch.State));
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

    private static GameSession ComponentSession()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, GameDirectory());
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        return session;
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world corpus is required for this isolated component fixture.");
        return directory;
    }
}
