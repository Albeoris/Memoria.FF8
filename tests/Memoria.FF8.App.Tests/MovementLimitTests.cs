using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MovementLimitTests
{
    [Fact]
    public void ActualLobbyLimitAndOriginalBackgroundLoopRestoreTogether()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(4, world.Events.Frame);
        Assert.Equal(new Vector2(-15, 72), NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!.ActorOffset);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(5, world.Events.Frame);
        ActorState actor = world.Events.Actors.Single(actor => actor.ObjectId == 20);
        Assert.Equal(4, actor.MovementDirectionLimit);
        Assert.Null(actor.MovementTarget);
        Assert.Null(world.Events.LastFault);
        JsonElement background = world.Events.Capture().GetProperty("BackgroundAnimations").EnumerateArray().Single(animation => animation.GetProperty("Id").GetInt32() == 0);
        Assert.True(background.GetProperty("Loop").GetBoolean());
        Assert.True(background.GetProperty("Active").GetBoolean());
        Assert.False(background.GetProperty("Complete").GetBoolean());
        Assert.Equal(0, background.GetProperty("First").GetInt32());
        Assert.Equal(2, background.GetProperty("Last").GetInt32());
        Assert.Equal(6, background.GetProperty("Speed").GetInt32());
        Assert.Equal(5, background.GetProperty("Counter").GetInt32());
        Assert.Equal(0, background.GetProperty("Position").GetInt32());
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
    }
}
