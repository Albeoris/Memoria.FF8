using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePlayerRadiusTests
{
    [Fact]
    public void AuthenticEvacuationUsesScriptedWordRadiusAndRestoresOrdinaryMovement()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        GameState recreated = GameSession.Load(source);
        var session = new GameSession(recreated);
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(340, world.Events.FieldId);
        Assert.Equal(125, session.State.Variables[256]);
        ActorState player = world.Events.Actors[0];
        Assert.Equal(64, player.PushRadius);
        Assert.True(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, player));
        foreach (int radius in new[] { 0, 1, 49, 64, 65535, -1, 65536 })
        {
            JsonNode altered = JsonNode.Parse(JsonSerializer.Serialize(player))!;
            altered["PushRadius"] = radius;
            Assert.Equal(radius is >= 0 and <= ushort.MaxValue, NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, altered.Deserialize<ActorState>()!));
        }
        using var restored = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        FieldFixedPosition before = world.Events.Scene.NativePlayerPosition!;
        var move = new PlayerAction(PlayerActionKind.Move, Horizontal: -1);
        Assert.True(world.Apply(move).Accepted);
        Assert.True(restored.Apply(move).Accepted);
        Assert.NotEqual(before, world.Events.Scene.NativePlayerPosition);
        Assert.Equal(1, world.Events.Scene.NativePlayerMovementVersion);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        var wait = new PlayerAction(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), restored.Apply(wait));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Null(world.Events.LastFault);
    }
}
