using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SlopedLineContinuationTests
{
    [Fact]
    public void ActualTrainingReturnCrossesTheSlopedDoorAndPreservesTheOriginalMusicBoundary()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        string source = TestResources.FixturePath("App.Tests.SlopedLineContinuationTests.1");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        using var restored = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        restored.EnsureField();
        Assert.Equal(214, world.Events.FieldId);
        Assert.Equal(256, world.Events.Frame);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        for (int tick = 0; tick < 7 && world.Events.FieldId == 214; tick++)
        {
            var move = new PlayerAction(PlayerActionKind.Move, Horizontal: -0.29028f, Vertical: 0.95694f);
            Assert.True(world.Apply(move).Accepted);
            Assert.True(restored.Apply(move).Accepted);
            world.EnsureField();
            restored.EnsureField();
            Assert.Null(world.Events.LastFault);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
        Assert.Equal(226, world.Events.FieldId);
        using var replay = new GameWorld(new GameSession(session.State with { EventMemory = session.State.EventMemory, EventState = world.Events.Capture() }), directory);
        replay.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
        var wait = new PlayerAction(PlayerActionKind.Wait);
        bool reachedBoundary = false;
        for (int tick = 0; tick < 4; tick++)
        {
            Exception? originalFault = Record.Exception(() => Assert.True(world.Apply(wait).Accepted));
            Exception? resumedFault = Record.Exception(() => Assert.True(replay.Apply(wait).Accepted));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
            if (originalFault is not null)
            {
                Assert.IsType<EventScriptException>(originalFault);
                Assert.IsType<EventScriptException>(resumedFault);
                reachedBoundary = true;
                break;
            }
            Assert.Null(resumedFault);
        }
        Assert.True(reachedBoundary);
        Assert.Equal("MUSICSKIP", world.Events.LastFault!.Opcode);
        Assert.Equal(739, world.Events.LastFault.Operation);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
    }
}
