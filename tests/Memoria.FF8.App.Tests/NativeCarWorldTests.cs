using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarWorldTests
{
    [Fact]
    public void Original_car_spawn_controls_mesh_and_independent_restore_preserve_native_vehicle_authority()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        // Isolated component fixture; actual gameplay acceptance belongs to the authentic New Game journal.
        var session = new GameSession(new GameState());
        session.StartNewGame();
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), Variables = new Dictionary<int, int> { [256] = 40, [265] = 128 } });
        using var scene = new WorldMapScene(session, directory);
        Assert.Equal((24511, -28167, -552), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal((2256, 2256), (scene.State.CameraYaw, scene.State.BodyYaw));
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal(73, session.State.SavedWorld!.Slot4.ShapeId);
        Assert.Equal(0, scene.State.NativeCarRuntime!.Speed);
        using var assets = new WorldAssets(directory);
        Assert.Equal(73, assets.ExamCar.ShapeId);
        Assert.Equal(9, assets.ExamCar.ModelIndex);
        Assert.Equal(84, assets.ExamCar.NativeVertices.Count);
        Assert.Equal(4, assets.ExamCar.Faces.Count(face => face.Vertices.Length == 3));
        Assert.Equal(54, assets.ExamCar.Faces.Count(face => face.Vertices.Length == 4));
        Assert.Equal(2, assets.ExamCar.Textures.Count);
        Assert.All(assets.ExamCar.Textures, texture => Assert.Equal((64, 64), (texture.Width, texture.Height)));
        Assert.Equal(8, assets.ExamCar.Faces.Count(face => face.Palette == 0));
        Assert.Equal(50, assets.ExamCar.Faces.Count(face => face.Palette == 1));
        GameState before = session.State;
        byte[] first = scene.Render(320, 224).ToArray();
        Assert.True(scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Vertical: -1)).Accepted);
        Assert.Equal(2, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(2287, scene.State.BodyYaw);
        ReplayJournalEntry action = session.ReplayJournal!.Endpoint!;
        var replaySession = new GameSession(before);
        using var replay = new WorldMapScene(replaySession, directory);
        Assert.Equal(1, replay.ReplayNavigation(action).FramesAdvanced);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(replaySession.State)));
        GameState endpoint = session.State;
        using var restored = new WorldMapScene(new GameSession(endpoint), directory);
        Assert.Equal(scene.Render(320, 224), restored.Render(320, 224));
        Assert.NotEqual(first, scene.Render(320, 224));
        var next = new PlayerAction(PlayerActionKind.Wait);
        Assert.Equal(scene.Apply(next), restored.Apply(next));
        Assert.Equal(0, scene.State.NativeCarRuntime.Speed);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(scene.State), JsonSerializer.SerializeToElement(restored.State)));
    }

    [Fact]
    public void Native_car_field_entry_selects_the_actual_entrance_polygon_and_atomically_retires_shape73()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        var session = new GameSession(new GameState());
        session.StartNewGame();
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), Variables = new Dictionary<int, int> { [256] = 40, [265] = 128 } });
        using (var entry = new WorldMapScene(session, directory))
        {
            // Isolated component contact fixture at an installed entrance-polygon centroid;
            // this neither records a causal route nor establishes ordinary player coverage.
            session.SetWorldState(entry.State with { X = 12390, Z = -26709, NativeHeight = -301, CameraTargetX = 12390, CameraTargetZ = -26709, CameraTargetNativeHeight = -557 });
        }
        using var world = new GameWorld(session, directory, persistReplayJournal: false);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(106, session.State.FieldId);
        Assert.Equal(new FieldTransition(106, 58, 4327, 197, 0), session.State.FieldArrival);
        Assert.Equal(262, session.State.PreviousFieldId);
        Assert.Null(session.State.World);
        NativeSavedWorldCarReturn receipt = session.State.SavedWorld!.CarReturn!;
        Assert.Equal(33, receipt.EntityType105);
        Assert.Equal(255, session.State.SavedWorld.Slot4.ShapeId);
        Assert.Equal(receipt.Pose, session.State.SavedWorld.Slot4.Pose);
        Assert.Null(session.State.SavedWorld.FootReturn);
        Assert.Equal(1, receipt.SourceWorldFrame);
        Assert.Equal(40, receipt.StoryWord);
        Assert.NotNull(session.State.DepartingWorldState!.NativeCarRuntime);
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-car-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path, registerJournalReference: false);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(GameSession.Load(path))));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Refused_car_batch_does_not_commit_partial_pose_speed_timer_or_journal_changes()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        var session = new GameSession(new GameState());
        session.StartNewGame();
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), Variables = new Dictionary<int, int> { [256] = 40, [265] = 128 } });
        session.SetExamTimer(NativeExamTimerState.FromReachedSet(600).KillDisplay());
        using var scene = new WorldMapScene(session, directory);
        session.SetWorldState(scene.State with { X = -5000, CameraTargetX = -1476, CameraYaw = 0 });
        GameState before = session.State;
        Assert.Throws<NotSupportedException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1, Frames: 2)));
        Assert.Same(before, session.State);
        Assert.Equal(0, session.ExamTimer!.ClockCalls);
    }

    [Fact]
    public void Prepared_car_entry_saved_slot_writes_are_validated_before_live_publication()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        var session = new GameSession(new GameState());
        session.StartNewGame();
        session.Restore(session.State with { FieldId = 262, Variables = new Dictionary<int, int> { [256] = 40, [265] = 128 } });
        var transition = new WorldTransition(63, 33, -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, transition);
        GameState before = session.State;
        Assert.Equal(0, before.SavedWorld!.Slot4.ShapeId);
        Assert.Equal(73, ticket.InitialSavedWorld!.Slot4.ShapeId);
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedWorldEntry(transition, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption));
        Assert.Same(before, session.State);
        session.CommitPreparedWorldEntry(transition, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        Assert.Equal(73, session.State.SavedWorld!.Slot4.ShapeId);
        Assert.Equal(33, scene.State.NativeCarRuntime!.EntityType);
        Assert.Throws<InvalidOperationException>(() => ticket.Adopt(session));
    }
}
