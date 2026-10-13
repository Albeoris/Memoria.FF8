using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTalkTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Actual_guard_approach_selects_original_contact_and_restores_its_action_script()
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
        Assert.Equal((129, 78L), (world.Events.FieldId, world.Events.Frame));
        ActorState player = world.Events.Actors[0];
        ActorState guard = world.Events.Actors[10];
        double distance = Math.Sqrt(Math.Pow(guard.X - player.X, 2) + Math.Pow(guard.Y - player.Y, 2));
        Assert.True(distance > guard.TalkRadius);
        Assert.True(distance < player.PushRadius + guard.TalkRadius);
        JsonElement before = world.Events.Capture();
        Assert.Equal(10, world.Events.SelectInteractionTarget());
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.False(world.Apply(new PlayerAction(PlayerActionKind.Interact, 9)).Accepted);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Interact, 10)).Accepted);
        Assert.Equal(78, world.Events.Frame);
        JsonElement queued = world.Events.Capture();
        JsonElement requested = Assert.Single(queued.GetProperty("Fibers").EnumerateArray(), fiber => fiber.GetProperty("ObjectId").GetInt32() == 10 && !fiber.GetProperty("Complete").GetBoolean());
        Assert.Equal(56, requested.GetProperty("ScriptId").GetInt32());
        Assert.Equal(0, requested.GetProperty("Priority").GetInt32());
        Assert.False(requested.GetProperty("Started").GetBoolean());
        Assert.Empty(requested.GetProperty("Stack").EnumerateArray());

        GameState copy = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State with { EventState = queued }, JsonOptions), JsonOptions)!;
        Assert.NotSame(session.State.Variables, copy.Variables);
        using var replay = new GameWorld(new GameSession(copy), directory);
        replay.EnsureField();
        Assert.True(JsonElement.DeepEquals(queued, replay.Events.Capture()));
        var wait = new PlayerAction(PlayerActionKind.Wait);
        for (int tick = 0; tick < 20 && world.Events.CurrentDialogue is null; tick++)
        {
            Assert.Equal(world.Apply(wait), replay.Apply(wait));
            Assert.Null(world.Events.LastFault);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        }
        Assert.NotNull(world.Events.CurrentDialogue);
        Assert.Equal(11, world.Events.CurrentDialogue.ObjectId);
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
    }
}
