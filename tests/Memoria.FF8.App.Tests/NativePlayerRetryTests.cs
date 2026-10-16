using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePlayerRetryTests
{
    [Fact]
    public void OriginalDormitoryRefusalAcceptsSecondAttemptAndRestoresVmAndPixels()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        string source = TestResources.FixturePath("App.Tests.NativePlayerRetryTests.1");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(44, world.Events.Frame);
        var action = new PlayerAction(PlayerActionKind.Move, Horizontal: 0.99919f, Vertical: 0.04016f);
        Assert.True(world.Apply(action).Accepted);
        Assert.Equal(new FieldFixedPosition(-627337, -723012, 0), world.Events.Scene.NativePlayerPosition);
        Assert.Equal(87, world.Events.Actors[0].WalkmeshTriangle);
        Assert.Equal(242, world.Events.Actors[0].Direction);
        using var restored = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
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
        Assert.Null(world.Events.LastFault);
        Assert.Equal(237, world.Events.FieldId);
    }
}
