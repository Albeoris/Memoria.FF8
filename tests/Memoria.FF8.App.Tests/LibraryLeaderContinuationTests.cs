using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LibraryLeaderContinuationTests
{
    [Fact]
    public void ActualLibraryLeaderInCompactSlotOneRendersMovesAndRestoresFullVmAndPixels()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(FindSource()));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(145, world.Events.FieldId);
        Assert.Equal(14, world.Events.Frame);
        Assert.Equal(1, world.Events.Scene.FieldPartyNativeActorIds[0]);
        Assert.Equal(2, world.Events.Scene.NativeActorObjectIds[1]);
        Assert.NotEmpty(world.Render(TimeSpan.Zero));
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
        FieldFixedPosition? before = world.Events.Scene.NativePlayerPosition;
        var move = new PlayerAction(PlayerActionKind.Move, Vertical: -1);
        Assert.True(world.Apply(move).Accepted);
        Assert.True(restored.Apply(move).Accepted);
        Assert.NotEqual(before, world.Events.Scene.NativePlayerPosition);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(145, world.Events.FieldId);
        Assert.Null(world.Events.LastFault);
    }

    private static string FindSource()
    {
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        return Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
    }
}
