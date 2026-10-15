using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PreviousFieldContinuationTests
{
    [Fact]
    public void ActualFrontApproachSetsPreviousFieldBeforeOriginalQuistisInitialization()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string evidence = Path.Combine(repository!.FullName, "../fixtures");
        var session = new GameSession(GameSession.Load(Path.Combine(evidence, "player-garden-async-exterior-explored160-checkpoint.json")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(160, world.Events.FieldId);
        Assert.Equal(24, world.Events.Frame);
        using JsonDocument epoch = JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence, "player-garden-async-exterior-summary.json")));
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        JsonElement[] moves = epoch.RootElement.GetProperty("Traces").GetProperty("ActionLog").EnumerateArray().Where(entry => entry.GetProperty("FieldId").GetInt32() == 160 && entry.GetProperty("Frame").GetInt64() >= 24).ToArray();
        Assert.Equal(203, moves.Length);
        foreach (JsonElement move in moves)
        {
            PlayerAction action = move.GetProperty("Action").Deserialize<PlayerAction>(options)!;
            Assert.Equal(PlayerActionKind.Move, action.Kind);
            Assert.Equal(1, action.Frames);
            Assert.True(world.Apply(action).Accepted);
        }
        world.EnsureField();
        Assert.Equal(159, world.Events.FieldId);
        Assert.Equal(0, world.Events.Frame);
        Assert.Equal(160, session.State.PreviousFieldId);
        Assert.Equal((ushort?)160, world.Events.Scene.PreviousFieldWord84);
        Assert.Equal(160, world.Events.Variables[84]);
        using var resumed = new GameWorld(new GameSession(session.State with { EventMemory = session.State.EventMemory, EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        for (int tick = 0; tick < 10; tick++)
        {
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            var wait = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(wait), resumed.Apply(wait));
            Assert.Null(world.Events.LastFault);
        }
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
    }
}
