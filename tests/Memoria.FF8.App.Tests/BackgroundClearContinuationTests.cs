using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BackgroundClearContinuationTests
{
    [Fact]
    public void NaturalEastArrivalClearsAndRestoresIndependentVmAndPixels()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        string source = TestResources.FixturePath("App.Tests.BackgroundClearContinuationTests.1");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(170, world.Events.FieldId);
        Assert.Equal(0, world.Events.Frame);
        var action = new PlayerAction(PlayerActionKind.Wait);
        Assert.True(world.Apply(action).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(1, world.Events.Frame);
        Assert.Equal(1, world.Events.Scene.BackgroundClearValue);
        byte[] pixels = world.Render(TimeSpan.Zero);
        using var restored = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
        for (int tick = 0; tick < 4; tick++)
        {
            PlayerActionResult result = world.Apply(action);
            Assert.True(result.Accepted);
            Assert.Null(world.Events.LastFault);
            Assert.Equal(result, restored.Apply(action));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
    }
}
