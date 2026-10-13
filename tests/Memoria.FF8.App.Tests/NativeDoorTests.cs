using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeDoorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };
    private static readonly (float Horizontal, float Vertical)[] RetainedCornerInputs =
    [
        (-0.62778157f, -0.77838945f),
        (-0.6270195f, -0.77900356f),
        (-0.6260573f, -0.779777f),
        (-0.62480456f, -0.7807812f),
        (-0.62651265f, -0.7794112f),
        (-0.6248197f, -0.78076905f),
        (-0.62746567f, -0.7786442f),
        (-0.6248578f, -0.7807385f)
    ];

    [Fact]
    public void Fresh_original_door_contacts_restore_and_cross_to_the_original_story_request()
    {
        string directory = GameDirectory();
        var session = new GameSession(GameSession.Load(Evidence("entry-preboundary-checkpoint.json")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(1649, session.State.World!.Frame);
        Assert.Equal(PlayerTriggerKind.FieldTransition, world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1, Vertical: -1)).Trigger.Kind);
        Assert.True(world.EnsureField());
        Assert.Null(world.Events.Scene.NativeDoorDriverVersion);
        AdvanceToCorner(world);
        Assert.Equal((2, 74L), (world.Events.Scene.NativeDoorDriverVersion, world.Events.Frame));
        Assert.Equal("Cecilia", world.Events.Scene.Names[0]);
        Assert.Equal([0, 3], world.Events.Scene.Party);
        foreach (var (horizontal, vertical) in RetainedCornerInputs.Take(7))
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical)).Accepted);
        }
        Assert.Equal(new FieldFixedPosition(1275170, -6954277, 0), world.Events.Scene.NativePlayerPosition);
        Assert.Equal(81, world.Events.Frame);
        var next = new PlayerAction(PlayerActionKind.Move, Horizontal: RetainedCornerInputs[7].Horizontal, Vertical: RetainedCornerInputs[7].Vertical);
        Assert.True(world.Apply(next).Accepted);
        Assert.Equal(82, world.Events.Frame);
        Assert.Equal((byte)3, world.Events.Scene.NativeDoorPendingEvents![9]);
        Assert.True(world.Events.Actors[9].LineContact);
        Assert.False(world.Events.Actors[9].NativeLineCrossingPending);
        Assert.Equal(17, session.State.Variables[256]);

        var restoredSession = new GameSession(Clone(session.State with { EventState = world.Events.Capture() }));
        Assert.NotSame(session.State.Variables, restoredSession.State.Variables);
        using var restored = new GameWorld(restoredSession, directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        List<int> lineScripts = [];
        world.Events.CommandExecuted += command =>
        {
            if (command.ObjectId == 9)
            {
                lineScripts.Add(command.ScriptId);
            }
        };
        var wait = new PlayerAction(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), restored.Apply(wait));
        Assert.Equal(new[] { 40, 40, 42, 42, 44, 44 }, lineScripts);
        Assert.Equal(17, session.State.Variables[256]);
        Assert.Empty(world.Events.Scene.NativeDoorPendingEvents!);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));

        int guideSteps = 0;
        while (!world.Events.Actors[9].NativeLineCrossingPending && guideSteps < 8)
        {
            ActorState player = world.Events.Actors[0];
            double dx = 345 - player.X;
            double dy = -1443.3334 - player.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            PlayerAction move = new(PlayerActionKind.Move, Horizontal: (float)(-dx / length), Vertical: (float)(-dy / length));
            Assert.Equal(world.Apply(move), restored.Apply(move));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            guideSteps++;
        }
        Assert.True(world.Events.Actors[9].NativeLineCrossingPending);
        Assert.Equal((byte)6, world.Events.Scene.NativeDoorPendingEvents![9]);
        Assert.Equal(17, session.State.Variables[256]);
        Assert.Equal(41, world.Events.LineCrossings.Single().ScriptId);
        Assert.Equal(world.Apply(wait), restored.Apply(wait));
        Assert.Equal(20, session.State.Variables[256]);
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.Null(world.Events.LastFault);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        JsonElement[] fibers = world.Events.Capture().GetProperty("Fibers").EnumerateArray().ToArray();
        JsonElement caller = Assert.Single(fibers, fiber => fiber.GetProperty("ObjectId").GetInt32() == 9 && fiber.GetProperty("ScriptId").GetInt32() == 41 && !fiber.GetProperty("Complete").GetBoolean());
        Assert.Equal((41, 2, 268, "RequestEnd"), (caller.GetProperty("ScriptId").GetInt32(), caller.GetProperty("Priority").GetInt32(), caller.GetProperty("Position").GetInt32(), caller.GetProperty("WaitReason").GetString()));
        Assert.Empty(caller.GetProperty("Stack").EnumerateArray());
        Assert.Equal(5, caller.GetProperty("NativeDoorSlot").GetInt32());
        JsonElement requested = Assert.Single(fibers, fiber => fiber.GetProperty("Sequence").GetInt64() == caller.GetProperty("RequestSequence").GetInt64());
        Assert.Equal((0, 4, 1), (requested.GetProperty("ObjectId").GetInt32(), requested.GetProperty("ScriptId").GetInt32(), requested.GetProperty("Priority").GetInt32()));
        Assert.False(requested.GetProperty("Started").GetBoolean());
        Assert.Empty(requested.GetProperty("Stack").EnumerateArray());
        JsonElement contact = Assert.Single(fibers, fiber => fiber.GetProperty("ObjectId").GetInt32() == 9 && fiber.GetProperty("ScriptId").GetInt32() == 42 && !fiber.GetProperty("Complete").GetBoolean());
        Assert.Equal(3, contact.GetProperty("Priority").GetInt32());
        Assert.Equal(4, contact.GetProperty("NativeDoorSlot").GetInt32());
        Assert.False(contact.GetProperty("Started").GetBoolean());
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }

    [Fact]
    public void Legacy_door_state_remains_unknown_and_malformed_v2_restore_is_atomic()
    {
        string directory = GameDirectory();
        var legacySession = new GameSession(GameSession.Load(Evidence("door-preboundary-checkpoint.json")));
        using var legacy = new GameWorld(legacySession, directory);
        legacy.EnsureField();
        Assert.Null(legacy.Events.Scene.NativeDoorDriverVersion);
        JsonElement before = legacy.Events.Capture();
        var next = new PlayerAction(PlayerActionKind.Move, Horizontal: RetainedCornerInputs[7].Horizontal, Vertical: RetainedCornerInputs[7].Vertical);
        Assert.Contains("Legacy field136", Assert.Throws<NotSupportedException>(() => legacy.Apply(next)).Message);
        Assert.True(JsonElement.DeepEquals(before, legacy.Events.Capture()));
        Assert.Empty(legacy.Events.ActionLog);

        var freshSession = new GameSession(GameSession.Load(Evidence("arrival-checkpoint.json")));
        using var fresh = new GameWorld(freshSession, directory);
        fresh.EnsureField();
        AdvanceToCorner(fresh);
        before = fresh.Events.Capture();
        foreach (string invalid in new[] { "version", "unknown", "owner", "flags", "mirror" })
        {
            JsonNode malformed = JsonNode.Parse(before.GetRawText())!;
            switch (invalid)
            {
                case "version":
                    malformed["Scene"]!["NativeDoorDriverVersion"] = 3;
                    break;
                case "unknown":
                    malformed["Scene"]!.AsObject().Remove("NativeDoorDriverVersion");
                    break;
                case "owner":
                    malformed["Scene"]!["NativeDoorPendingEvents"]!["0"] = 1;
                    break;
                case "flags":
                    malformed["Scene"]!["NativeDoorPendingEvents"]!["9"] = 32;
                    break;
                case "mirror":
                    malformed["Actors"]![9]!["NativeLineCrossingPending"] = true;
                    break;
            }
            Assert.Throws<InvalidDataException>(() => fresh.Events.Restore(JsonSerializer.SerializeToElement(malformed)));
            Assert.True(JsonElement.DeepEquals(before, fresh.Events.Capture()));
        }
    }

    private static void AdvanceToCorner(GameWorld world)
    {
        for (int tick = 0; tick < 61; tick++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        for (int tick = 0; tick < 13; tick++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1)).Accepted);
        }
    }

    private static GameState Clone(GameState state) => JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state, JsonOptions), JsonOptions)!;

    private static string GameDirectory()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field/world corpus is required.");
        return directory;
    }

    private static string Evidence(string suffix)
    {
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        return Path.Combine(repository!.FullName, "../fixtures", "player-dollet-fire-cavern-" + suffix);
    }
}
