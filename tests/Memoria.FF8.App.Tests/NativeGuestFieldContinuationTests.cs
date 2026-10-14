using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGuestFieldContinuationTests
{
    [Fact]
    public void AuthenticDreamForestTracksAndMovesTheBoundGuestAndRestoresItsCurrentOwners()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original dream field corpus is required.");
        string source = Path.Combine(Repository(), "../fixtures/external-fixture.json");
        GameState recreated = GameWorld.RecreateSave(GameSession.LoadReplaySource(source), directory);
        var session = new GameSession(recreated);
        using var world = new GameWorld(session, directory, persistReplayJournal: false);
        world.EnsureField();
        Assert.Equal(819, world.Events.FieldId);
        Assert.Equal(3, world.Events.Frame);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Equal(new[] { 8, 9, 10 }, world.Events.Scene.Party);
        ActorState leader = NativeFieldPartyBinding.ResolveActor(world.Events.Scene, world.Events.Actors, 0)!;
        Assert.Equal((1, 8), (leader.ObjectId, leader.CharacterId));
        ActorState squall = world.Events.Actors.Single(actor => actor.CharacterId == 0);
        Assert.False(squall.Active);
        Assert.NotEqual(squall.ObjectId, leader.ObjectId);
        Assert.True(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, leader));
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, squall));
        BackgroundScrollFrame camera = NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!;
        FieldNativeCameraResult projection = NormalCameraCertificate.ProjectActorCenter(world.Field!, world.Events.Scene, leader);
        Assert.True(projection.Supported);
        FieldNativeCameraCenter center = projection.Center!.Value;
        Assert.Equal(new Vector2(-center.X, -center.Y), camera.ActorOffset);

        string saved = Path.Combine(Path.GetTempPath(), "ff8-authentic-guest-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.SetEventState(world.Events.Capture());
            session.Save(saved, registerJournalReference: false);
            var restoredSession = new GameSession(GameSession.Load(saved));
            using var restored = new GameWorld(restoredSession, directory, persistReplayJournal: false);
            restored.EnsureField();
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            Vector3 guestBefore = new(leader.X, leader.Y, leader.Z);
            Vector3 squallBefore = new(squall.X, squall.Y, squall.Z);
            PlayerAction move = new(PlayerActionKind.Move, Horizontal: 1);
            PlayerActionResult moved = world.Apply(move);
            Assert.Equal(moved, restored.Apply(move));
            Assert.True(moved.Accepted, moved.Detail);
            Assert.Equal(4, world.Events.Frame);
            Assert.NotEqual(guestBefore, new Vector3(leader.X, leader.Y, leader.Z));
            Assert.Equal(squallBefore, new Vector3(squall.X, squall.Y, squall.Z));
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
