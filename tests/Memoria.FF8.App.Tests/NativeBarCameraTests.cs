using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBarCameraTests
{
    [Fact]
    public void AuthenticBarCameraZeroIgnoresTheUnselectedRecordAndRestoresItsActualPresentation()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original bar field corpus is required.");
        string source = Path.Combine(Repository(), "../fixtures/external-fixture.json");
        GameState recreated = GameWorld.RecreateSave(GameSession.LoadReplaySource(source), directory);
        var session = new GameSession(recreated);
        using var world = new GameWorld(session, directory, persistReplayJournal: false);
        world.EnsureField();
        FieldAssets field = world.Field!;
        Assert.Equal(717, field.Id);
        Assert.Equal(39, world.Events.Frame);
        Assert.Equal(2, field.Cameras.Count);
        Assert.Equal(0, world.Events.Scene.CameraIndex);
        BackgroundScrollFrame original = NormalCameraCertificate.Prepare(field, world.Events.Scene, world.Events.Actors)!;
        FieldAssets onlySelected = field with { Cameras = [field.Cameras[0]] };
        BackgroundScrollFrame single = NormalCameraCertificate.Prepare(onlySelected, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(original.ActorOffset, single.ActorOffset);
        Assert.Equal(original.LayerOffsets.OrderBy(pair => pair.Key), single.LayerOffsets.OrderBy(pair => pair.Key));
        Assert.False(NormalCameraCertificate.HasBaseline(field with { Cameras = [] }, 0));
        Assert.False(NormalCameraCertificate.HasBaseline(field with { Camera = field.Cameras[1] }, 0));
        ActorState leader = NativeFieldPartyBinding.ResolveActor(world.Events.Scene, world.Events.Actors, 0)!;
        FieldNativeCameraResult projection = NormalCameraCertificate.ProjectActorCenter(field, world.Events.Scene, leader);
        Assert.True(projection.Supported);
        Assert.Equal(new System.Numerics.Vector2(-projection.Center!.Value.X, -projection.Center.Value.Y), original.ActorOffset);

        string saved = Path.Combine(Path.GetTempPath(), "ff8-authentic-bar-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.SetEventState(world.Events.Capture());
            session.Save(saved, registerJournalReference: false);
            var restoredSession = new GameSession(GameSession.Load(saved));
            using var restored = new GameWorld(restoredSession, directory, persistReplayJournal: false);
            restored.EnsureField();
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            PlayerAction next = new(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(next), restored.Apply(next));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            Assert.Null(world.Events.LastFault);
        }
        finally
        {
            File.Delete(saved);
        }
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs", "recreate-checkpoint.md")))
        {
            directory = directory.Parent;
        }
        return directory!.FullName;
    }
}
