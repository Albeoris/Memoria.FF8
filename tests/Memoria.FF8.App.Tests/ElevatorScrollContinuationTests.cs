using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ElevatorScrollContinuationTests
{
    [Fact]
    public void ActualElevatorReturnScrollsTo224AndRestoresEveryFrameUntilSecondFloor()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(172, world.Events.FieldId);
        Assert.Equal(92, world.Events.Frame);
        var wait = new PlayerAction(PlayerActionKind.Wait);
        Assert.True(world.Apply(wait).Accepted);
        Assert.Equal((short)224, world.Events.Scene.BackgroundScrollPositions[0].TargetY);
        using var replay = new GameWorld(new GameSession(session.State with { EventMemory = session.State.EventMemory, EventState = world.Events.Capture() }), directory);
        replay.EnsureField();
        bool scrolled = false;
        for (int tick = 0; tick < 240 && world.Events.FieldId == 172; tick++)
        {
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
            BackgroundScrollPosition position = world.Events.Scene.BackgroundScrollPositions[0];
            scrolled |= position.Y > 0;
            Assert.InRange(Assert.IsType<short>(position.Y), (short)0, (short)224);
            Assert.Equal(world.Apply(wait), replay.Apply(wait));
            world.EnsureField();
            replay.EnsureField();
            Assert.Null(world.Events.LastFault);
        }
        Assert.True(scrolled);
        Assert.Equal(137, world.Events.FieldId);
        Assert.Equal(137, replay.Events.FieldId);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
    }
}
