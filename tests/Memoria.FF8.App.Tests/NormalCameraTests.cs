using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NormalCameraTests
{
    [Fact]
    public void NaturalQuadMatrixBoundaryRendersAndIndependentlyRestoresContinuation()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(185, world.Events.FieldId);
        Assert.Equal(39, world.Events.Frame);
        Assert.Null(world.Events.LastFault);
        BackgroundScrollFrame scroll = NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(new Vector2(-80, 72), scroll.ActorOffset);
        Assert.Equal(scroll.ActorOffset, scroll.LayerOffsets[0]);
        byte[] pixels = world.Render(TimeSpan.Zero);
        using var restored = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
        for (int tick = 0; tick < 4; tick++)
        {
            var action = new PlayerAction(PlayerActionKind.Move, Horizontal: 0.98095f, Vertical: -0.19427f);
            PlayerActionResult result = world.Apply(action);
            Assert.True(result.Accepted);
            Assert.Equal(result, restored.Apply(action));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void NaturalInfirmaryCorridorUsesOriginalWideCameraAndRestoresPixels()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(76, world.Events.Frame);
        Assert.Null(world.Events.LastFault);
        BackgroundScrollFrame scroll = NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(new Vector2(160, 0), scroll.ActorOffset);
        Assert.Equal(scroll.ActorOffset, scroll.LayerOffsets[0]);
        byte[] pixels = world.Render(TimeSpan.Zero);
        using var restored = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
        foreach (string checkpoint in new[] { "player-garden-infirmary-door-current179-checkpoint.json", "player-garden-infirmary-door-playable180-checkpoint.json" })
        {
            string adjacentSource = Path.Combine(repository.FullName, "../fixtures", checkpoint);
            var adjacentSession = new GameSession(GameSession.Load(adjacentSource));
            using var adjacent = new GameWorld(adjacentSession, directory);
            adjacent.EnsureField();
            BackgroundScrollFrame adjacentScroll = NormalCameraCertificate.Prepare(adjacent.Field!, adjacent.Events.Scene, adjacent.Events.Actors)!;
            Assert.Equal(0, adjacentScroll.ActorOffset.X);
            Assert.InRange(adjacentScroll.ActorOffset.Y, -8, 8);
            byte[] adjacentPixels = adjacent.Render(TimeSpan.Zero);
            using var adjacentRestore = new GameWorld(new GameSession(adjacentSession.State with { EventState = adjacent.Events.Capture() }), directory);
            adjacentRestore.EnsureField();
            Assert.True(JsonElement.DeepEquals(adjacent.Events.Capture(), adjacentRestore.Events.Capture()));
            Assert.Equal(adjacentPixels, adjacentRestore.Render(TimeSpan.Zero));
        }
    }

    [Fact]
    public void NaturalWestLobbyRoundingBoundaryRendersAndReplaysFromPreservedState()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string evidence = Path.Combine(repository!.FullName, "../fixtures");
        var session = new GameSession(GameSession.Load(Path.Combine(evidence, "player-garden-west165-diagonal2-wall-preboundary-checkpoint.json")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(245, world.Events.Frame);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(new FieldFixedPosition(-9329120, -22734368, 2322432), world.Events.Scene.NativePlayerPosition);
        BackgroundScrollFrame scroll = NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(new Vector2(24, 64), scroll.ActorOffset);
        Assert.Equal(scroll.ActorOffset, scroll.LayerOffsets[0]);
        byte[] pixels = world.Render(TimeSpan.Zero);
        using var restored = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
        var replaySession = new GameSession(GameSession.Load(Path.Combine(evidence, "player-garden-west165-west3-wall-preboundary-checkpoint.json")));
        using var replay = new GameWorld(replaySession, directory);
        replay.EnsureField();
        Assert.Equal(231, replay.Events.Frame);
        for (int tick = 0; tick < 14; tick++)
        {
            Assert.True(replay.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Vertical: -1)).Accepted);
            _ = replay.Render(TimeSpan.Zero);
        }
        JsonNode historical = JsonNode.Parse(world.Events.Capture().GetRawText())!;
        JsonNode current = JsonNode.Parse(replay.Events.Capture().GetRawText())!;
        // Executed native47A2F6 clears this byte after a certified step;
        // the historical snapshot has no authority for its old value.
        Assert.Null(world.Events.Scene.NativePlayerEarlyRetryState);
        Assert.Equal((byte?)0, replay.Events.Scene.NativePlayerEarlyRetryState);
        Assert.Null(historical["Scene"]!["NativePlayerEarlyRetryState"]);
        Assert.Equal(0, current["Scene"]!["NativePlayerEarlyRetryState"]!.GetValue<int>());
        current["Scene"]!.AsObject().Remove("NativePlayerEarlyRetryState");
        // The archived state predates native door proximity authority; ordinary replay establishes it.
        foreach (JsonNode actor in historical["Actors"]!.AsArray().OfType<JsonNode>())
        {
            Assert.Equal(0, actor["NativeLineContactVersion"]!.GetValue<int>());
            actor.AsObject().Remove("NativeLineContactVersion");
        }
        foreach (JsonNode actor in current["Actors"]!.AsArray().OfType<JsonNode>())
        {
            actor.AsObject().Remove("NativeLineContactVersion");
        }
        Assert.True(JsonNode.DeepEquals(historical, current));
        Assert.Equal(pixels, replay.Render(TimeSpan.Zero));
        using var replayRestore = new GameWorld(new GameSession(replaySession.State with { EventState = replay.Events.Capture() }), directory);
        replayRestore.EnsureField();
        Assert.True(JsonElement.DeepEquals(replay.Events.Capture(), replayRestore.Events.Capture()));
        Assert.Equal(pixels, replayRestore.Render(TimeSpan.Zero));
    }

    [Fact]
    public void NaturalLobbyInitializationUsesInfInputAndNativeNormalTracking()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(0, world.Events.InputAxisDirection);
        using var fresh = new GameWorld(new GameSession(session.State with { EventState = null, DepartingFieldState = null }), directory);
        fresh.EnsureField();
        Assert.Equal(128, fresh.Events.InputAxisDirection);
        Assert.Null(NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors));
        for (int tick = 0; tick < 5 && world.Events.Scene.NativeFieldLoadVersion == 0; tick++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Null(world.Events.LastFault);
        Assert.Equal(1, world.Events.Scene.NativeFieldLoadVersion);
        BackgroundScrollFrame scroll = NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(new Vector2(-15, 72), scroll.ActorOffset);
        Assert.Equal(scroll.ActorOffset, scroll.LayerOffsets[0]);
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
    }
}
