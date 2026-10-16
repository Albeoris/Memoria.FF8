using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MovieContinuationTests
{
    [Fact]
    public void Actual_classroom_fault_retry_preserves_declared_control_bone_face_wait()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        string source = Path.Combine(FindRepository(), "../fixtures/external-fixture.json");
        Assert.True(File.Exists(source), "The independently reached pre-dispatch face checkpoint is required.");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Null(world.Events.LastFault);
        Assert.Equal(232, world.Field!.Id);
        ActorState caller = Assert.Single(world.Events.Actors, actor => actor.ObjectId == 15);
        world.Events.Step();
        for (int frame = 0; frame < 3; frame++)
        {
            world.Events.Step();
        }
        Assert.NotNull(caller.FirstControlRotation);
        Assert.InRange(caller.FaceProgress, 1, 14);
        Assert.Equal(15, caller.FaceDuration);
        session.SetEventState(world.Events.Capture());
        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        using var restored = new GameWorld(new GameSession(saved), directory);
        restored.EnsureField();
        Assert.Equal(world.Events.Capture().GetRawText(), restored.Events.Capture().GetRawText());
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(PlayerTriggerKind.Dialogue, world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1000)).Trigger.Kind);
        Assert.Equal(PlayerTriggerKind.Dialogue, restored.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1000)).Trigger.Kind);
        Assert.Equal(1, world.Events.CurrentDialogue!.MessageId);
        Assert.Null(caller.FirstControlRotation);
        Assert.Equal(world.Events.Capture().GetRawText(), restored.Events.Capture().GetRawText());
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }

    [Fact]
    public async Task Actual_corridor_movie_clock_restore_and_terminal_skip_preserve_coordinate_arrival()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        string repository = FindRepository();
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        var clock = new MovieClock(session, directory);
        using var world = new GameWorld(session, directory, clock);
        StorySafeMovieSkipTests.ReachCorridorMovie(session, world, "Cecilia");
        MovieInformation information = clock.Prepare();
        Assert.Equal(326, information.FrameCount);
        Assert.Equal(325, clock.State.TerminalFrame);
        StorySafeMovieSkipTests.DecodeThrough(session, world, clock, directory, 74);
        using (var decoder = new FfmpegMovieDecoder(clock.ToolsDirectory, clock.SourcePath, 5, information))
        {
            MovieFrame? frame = null;
            Stopwatch timeout = Stopwatch.StartNew();
            while (frame is null && timeout.Elapsed < TimeSpan.FromSeconds(10))
            {
                decoder.TryGetFrame(0, out frame);
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
            Assert.NotNull(frame);
            int absoluteFrame = MovieClock.AbsoluteFrame(5, information) + checked((int)frame.Index);
            Assert.Equal(75, absoluteFrame);
            session.SetMoviePosition(5);
            session.SetMoviePlayback(new MoviePlaybackState(absoluteFrame, information.FrameCount - 1, true, false, false));
        }
        long before = world.Events.Frame;
        world.Events.Step();
        Assert.Equal(before + 1, world.Events.Frame);
        Assert.True(session.IsMoviePlaying);
        Assert.Equal(75, world.Events.CurrentMoviePlayback!.FrameIndex);
        session.SetEventState(world.Events.Capture());
        string checkpoint = Path.Combine(Path.GetTempPath(), "memoria-movie-arrival-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(checkpoint);
            var resumed = new GameSession(GameSession.Load(checkpoint));
            var resumedClock = new MovieClock(resumed, directory);
            using var restored = new GameWorld(resumed, directory, resumedClock);
            restored.EnsureField();
            Assert.Equal(world.Events.Capture().GetRawText(), restored.Events.Capture().GetRawText());
            Assert.Equal(clock.State, resumedClock.State);
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.SkipMovie)).Accepted);
            Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.SkipMovie)).Accepted);
            Assert.Equal(new MoviePlaybackState(325, 325, false, true, true), resumed.State.MoviePlayback);
            for (int frame = 0; frame < 300 && resumed.State.FieldId == 229; frame++)
            {
                world.Events.Step();
                restored.Events.Step();
            }
            Assert.Equal(new FieldTransition(232, 1389, -3580, 50, 0), resumed.State.FieldArrival);
            Assert.Equal(session.State.FieldArrival, resumed.State.FieldArrival);
            Assert.Equal(232, resumed.State.FieldId);
            Assert.Contains("Cecilia", restored.Events.Scene.Names.Values);
            Assert.True(restored.EnsureField());
            Assert.Equal(232, restored.Field!.Id);
            FieldTransition arrival = resumed.State.FieldArrival!;
            WalkmeshPosition spawn = restored.Events.ResolveFieldArrival!(arrival)!;
            Assert.Equal(1389, spawn.Position.X);
            Assert.Equal(-3580, spawn.Position.Y);
            Assert.Equal(50, spawn.Triangle);
            Assert.True(float.IsFinite(spawn.Position.Z));
            resumed.SetEventState(restored.Events.Capture());
            resumed.Save(Path.Combine(repository, "../fixtures/external-fixture.json"));
            for (int frame = 0; frame < 300 && !restored.Events.Capture().GetProperty("DefaultsStarted").GetBoolean(); frame++)
            {
                restored.Events.Step();
            }
            Assert.True(restored.Events.Capture().GetProperty("DefaultsStarted").GetBoolean());
            ActorState player = Assert.Single(restored.Events.Actors, actor => actor.CharacterId == 0);
            Assert.Equal(spawn.Position.X, player.X);
            Assert.Equal(spawn.Position.Y, player.Y);
            Assert.Equal(spawn.Position.Z, player.Z);
            Assert.Equal(50, player.WalkmeshTriangle);
            Assert.Equal(0, player.Direction);
            Assert.Equal(JsonValueKind.Null, restored.Events.Capture().GetProperty("PendingArrival").ValueKind);
            PlayerActionResult next = restored.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1000));
            Assert.True(next.Accepted);
            Assert.Equal(PlayerTriggerKind.Dialogue, next.Trigger.Kind);
            Assert.Equal(0, next.Trigger.MessageId);
            Assert.False(restored.Events.IsUserControlEnabled);
            Assert.Null(restored.Events.LastFault);
            Assert.Equal(1, restored.Events.Scene.CameraIndex);
            Assert.Equal(2, restored.Field.Cameras.Count);
            Assert.Equal(439, restored.Field.GetCamera(1).FocalLength);
            Assert.Equal(SoundResourceStatus.Muted, restored.Events.Scene.LoadedSoundEffects[11].Status);
            Assert.Contains("Cecilia", restored.Events.Scene.Names.Values);
            Assert.Contains(3, restored.Events.GetBackgroundModuleShades().Keys);
            Assert.Equal(320 * 240 * 4, restored.Render(TimeSpan.Zero).Length);
            resumed.SetEventState(restored.Events.Capture());
            resumed.Save(Path.Combine(repository, "../fixtures/external-fixture.json"));
            using var initialized = new GameWorld(new GameSession(GameSession.Load(Path.Combine(repository, "../fixtures/external-fixture.json"))), directory);
            initialized.EnsureField();
            Assert.Equal(restored.Events.Capture().GetRawText(), initialized.Events.Capture().GetRawText());
            Assert.Equal(restored.Render(TimeSpan.Zero), initialized.Render(TimeSpan.Zero));
            Assert.Equal(232, resumed.State.FieldId);
            File.WriteAllText(Path.Combine(repository, "../fixtures/external-fixture.json"), JsonSerializer.Serialize(new
            {
                restored.Field.Id,
                restored.Field.Name,
                LogicalWidth = 320,
                LogicalHeight = 240,
                Models = restored.Field.Models.Count,
                Tiles = restored.Field.Background.Count,
                Triangles = restored.Field.Walkmesh.Triangles.Count,
                RequestedArrival = arrival,
                ResolvedGeometry = new { spawn.Position.X, spawn.Position.Y, spawn.Position.Z, spawn.Triangle },
                restored.Events.NextTrigger,
                restored.Events.Scene.CameraIndex,
                FieldAudio = restored.Events.Scene.LoadedSoundEffects,
                PendingArrival = false
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            File.Delete(checkpoint);
        }
    }

    private static string FindRepository()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory?.FullName ?? throw new DirectoryNotFoundException("FF8 research root was not found.");
    }
}
