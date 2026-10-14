using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SlopedSplitContinuationTests
{
    [Fact]
    public void ActualSoloElevatorSplitTraversesItsNativeSlopeAndRestoresFullVmAndPixels()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(172, world.Events.FieldId);
        Assert.Equal(59, world.Events.Frame);
        var wait = new PlayerAction(PlayerActionKind.Wait);
        Assert.True(world.Apply(wait).Accepted);
        Assert.Null(world.Events.LastFault);
        JsonElement begun = world.Events.Capture();
        using var replay = new GameWorld(new GameSession(session.State with { EventState = begun }), directory);
        replay.EnsureField();
        bool heightChanged = false;
        for (int tick = 0; tick < 100 && world.Events.Capture().GetProperty("PartySplit").ValueKind != JsonValueKind.Null; tick++)
        {
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
            Assert.Equal(world.Apply(wait), replay.Apply(wait));
            world.EnsureField();
            replay.EnsureField();
            Assert.Null(world.Events.LastFault);
            if (world.Events.FieldId == 172)
            {
                heightChanged |= world.Events.Actors[0].Z != 1344;
            }
        }
        Assert.True(heightChanged);
        Assert.Equal(89, world.Events.Frame);
        Assert.Equal(172, world.Events.FieldId);
        Assert.Equal(172, replay.Events.FieldId);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
    }
}
