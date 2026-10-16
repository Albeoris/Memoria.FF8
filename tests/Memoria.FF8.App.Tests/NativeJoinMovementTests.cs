using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeJoinMovementTests
{
    [Fact]
    public void ReachedUnequalHeightJoinRestoresItsNativeSlopeStepsAndCameraCompletion()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        ActorState follower = world.Events.Actors.Single(actor => actor.ObjectId == 6);
        Assert.True(NativeJoinMovementAdapter.Supports(world.Field!, world.Events.Scene, follower));
        Assert.False(NativeJoinMovementAdapter.Supports(world.Field!, world.Events.Scene, world.Events.Actors.Single(actor => actor.ObjectId == 0)));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(32, world.Events.Frame);
        Assert.Equal(1344, world.Events.Actors.Single(actor => actor.ObjectId == 0).Z);
        Assert.True(follower.Z <= 1347);
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        for (int tick = 0; tick < 100 && !world.Events.IsUserControlEnabled; tick++)
        {
            var action = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(action), resumed.Apply(action));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        }
        Assert.Null(world.Events.LastFault);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Equal(172, world.Events.FieldId);
        Assert.Equal((short)0, world.Events.Scene.BackgroundScrollPositions[0].Y);
        Assert.Equal((byte)2, world.Events.Scene.BackgroundScrollPositions[0].Status);
        Assert.Equal(1, world.Events.Scene.NativePartyTraceVersion);
        Assert.NotNull(world.Events.Scene.PartyTrace);
        Assert.Equal(1344, follower.Z);
    }
}
