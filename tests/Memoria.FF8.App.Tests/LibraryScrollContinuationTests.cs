using System.Text.Json;
using System.Numerics;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LibraryScrollContinuationTests
{
    [Fact]
    public void ActualLibraryApproachScrollInitializesAndRestoresFullVmAndPixels()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(FindSource()));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(230, world.Events.FieldId);
        Assert.Equal(2, world.Events.Frame);
        Assert.Empty(world.Events.Scene.BackgroundScrollLayers);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(2, world.Events.Scene.BackgroundScrollLayers.Count);
        Assert.Equal(new BackgroundScrollSettings(0, 0, 64, 0), world.Events.Scene.BackgroundScrollLayers[1]);
        Assert.Equal(new BackgroundScrollSettings(0, 0, 48, 0), world.Events.Scene.BackgroundScrollLayers[2]);
        BackgroundScrollFrame scroll = NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(new Vector2(160, 0), scroll.ActorOffset);
        Assert.Equal(new Vector2(160, 0), scroll.LayerOffsets[0]);
        Assert.Equal(new Vector2(40, 0), scroll.LayerOffsets[1]);
        Assert.Equal(new Vector2(30, 0), scroll.LayerOffsets[2]);
        Assert.Equal(new Vector2(160, 0), scroll.LayerOffsets[3]);
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
        Assert.Equal(230, world.Events.FieldId);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(3, world.Events.Scene.BackgroundScrollLayers.Count);
        Assert.Equal(new BackgroundScrollSettings(0, 0, 32, 0), world.Events.Scene.BackgroundScrollLayers[3]);
        scroll = NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(new Vector2(20, 0), scroll.LayerOffsets[3]);
    }

    private static string FindSource()
    {
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        return Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
    }
}
