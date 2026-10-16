using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MusicPositionContinuationTests
{
    [Fact]
    public void ActualTrainingDoorMusicPositionContinuesAndRestoresFullVmAndPixels()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(170, world.Events.FieldId);
        Assert.Equal(313, world.Events.Frame);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -0.83029f, Vertical: -0.55733f)).Accepted);
        world.EnsureField();
        Assert.Null(world.Events.LastFault);
        using var restored = new GameWorld(new GameSession(session.State with { EventMemory = session.State.EventMemory, EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        for (int tick = 0; tick < 4; tick++)
        {
            var wait = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(wait), restored.Apply(wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
        Assert.Equal(226, world.Events.FieldId);
        Assert.Null(world.Events.LastFault);
    }
}
