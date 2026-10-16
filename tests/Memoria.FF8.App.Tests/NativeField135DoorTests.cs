using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeField135DoorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Fresh_original135_replays_actual_moves_and_restores_facing_then_original_crossing()
    {
        string directory = GameDirectory();
        GameState arrival = GameSession.Load(Evidence("fifth-arrival-checkpoint.json"));
        Assert.Equal(new FieldTransition(135, 6, -4680, 10, 128), arrival.FieldArrival);
        Assert.Null(arrival.EventState);
        var session = new GameSession(arrival);
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Null(world.Events.Scene.NativeDoorDriverVersion);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(2, world.Events.Scene.NativeDoorDriverVersion);
        Assert.Equal(1, world.Events.Scene.NativeDoorInitializationFrame);
        ActorState line = world.Events.Actors[9];
        Assert.Equal(183, line.LineOperation);
        Assert.Equal(new System.Numerics.Vector3(-265, -4425, 16), line.LineStart);
        Assert.Equal(new System.Numerics.Vector3(105, -4425, 16), line.LineEnd);

        using JsonDocument proof = JsonDocument.Parse(File.ReadAllText(Evidence("lava-continuation-proof.json")));
        PlayerAction[] actions = proof.RootElement.GetProperty("acceptedActions").Deserialize<PlayerAction[]>(JsonOptions)!;
        Assert.Equal(7, actions.Length);
        foreach (PlayerAction action in actions)
        {
            Assert.True(world.Apply(action).Accepted);
        }
        Assert.Equal(9, world.Events.Frame);
        Assert.Equal(23, session.State.Variables[256]);
        GameState legacy = GameSession.Load(Evidence("lava-line-preboundary-checkpoint.json"));
        session.SetEventState(world.Events.Capture());
        JsonElement legacyVm = legacy.EventState!.Value;
        JsonElement freshVm = world.Events.Capture();
        Assert.True(JsonElement.DeepEquals(legacyVm.GetProperty("EventMemory"), freshVm.GetProperty("EventMemory")));
        Assert.True(JsonElement.DeepEquals(legacyVm.GetProperty("Scene").GetProperty("NativePlayerPosition"), freshVm.GetProperty("Scene").GetProperty("NativePlayerPosition")));
        Assert.True(JsonElement.DeepEquals(legacyVm.GetProperty("Scene").GetProperty("PartyTrace"), freshVm.GetProperty("Scene").GetProperty("PartyTrace")));
        Assert.Equal(NativeFieldInitializationStage.Completed, world.Events.Scene.NativeFieldInitialization!.Stage);
        Assert.NotNull(line.NativeSetLineReceipt);

        PlayerAction next = proof.RootElement.GetProperty("nextOrdinaryAction").Deserialize<PlayerAction>(JsonOptions)!;
        Assert.True(world.Apply(next).Accepted);
        Assert.Equal(10, world.Events.Frame);
        Assert.True(line.LineContact);
        Assert.False(line.NativeLineCrossingPending);
        Assert.Equal((byte)3, world.Events.Scene.NativeDoorPendingEvents![9]);
        Assert.Equal(23, session.State.Variables[256]);
        Assert.Empty(world.Events.LineCrossings);
        string save = Path.Combine(Path.GetTempPath(), "Memoria.FF8-door135-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.SetEventState(world.Events.Capture());
            session.Save(save);
            var restoredSession = new GameSession(GameSession.Load(save));
            Assert.NotSame(session.State.Variables, restoredSession.State.Variables);
            using var restored = new GameWorld(restoredSession, directory);
            restored.EnsureField();
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            world.SetRenderSize(320, 240);
            restored.SetRenderSize(320, 240);
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            List<int> scripts = [];
            world.Events.CommandExecuted += command =>
            {
                if (command.ObjectId == 9)
                {
                    scripts.Add(command.ScriptId);
                }
            };
            var wait = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(wait), restored.Apply(wait));
            Assert.Equal(new[] { 39, 39, 41, 41, 43, 43 }, scripts);
            Assert.Equal(23, session.State.Variables[256]);
            Assert.Empty(world.Events.Scene.NativeDoorPendingEvents!);

            int moves = 0;
            while (!line.NativeLineCrossingPending && moves < 8)
            {
                Assert.Equal(world.Apply(next), restored.Apply(next));
                Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
                moves++;
            }
            Assert.True(line.NativeLineCrossingPending);
            Assert.Equal(23, session.State.Variables[256]);
            LineCrossing crossed = Assert.Single(world.Events.LineCrossings);
            Assert.Equal((135, 9, 40, 183), (crossed.FieldId, crossed.ObjectId, crossed.ScriptId, crossed.Operation));
            Assert.Equal(world.Apply(wait), restored.Apply(wait));
            Assert.Equal(24, session.State.Variables[256]);
            Assert.Equal((3, 9, 40, 203), (world.Events.CurrentDialogue!.MessageId, world.Events.CurrentDialogue.ObjectId, world.Events.CurrentDialogue.ScriptId, world.Events.CurrentDialogue.Operation));
            Assert.Null(world.Events.LastFault);
            JsonElement snapshot = world.Events.Capture();
            JsonElement caller = Assert.Single(snapshot.GetProperty("Fibers").EnumerateArray(), fiber => fiber.GetProperty("ObjectId").GetInt32() == 9 && fiber.GetProperty("ScriptId").GetInt32() == 40 && !fiber.GetProperty("Complete").GetBoolean());
            Assert.Equal((2, 5, "Frames", 206), (caller.GetProperty("Priority").GetInt32(), caller.GetProperty("NativeDoorSlot").GetInt32(), caller.GetProperty("WaitReason").GetString(), caller.GetProperty("Position").GetInt32()));
            Assert.True(JsonElement.DeepEquals(snapshot, restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
        finally
        {
            if (File.Exists(save))
            {
                File.Delete(save);
            }
        }
    }

    [Fact]
    public void Historical_v2_literal_endpoints_restore_without_inventing_loader_or_execution_receipt()
    {
        string path = Path.Combine(Path.GetDirectoryName(Evidence("fifth-arrival-checkpoint.json"))!, "player-ifrit-fresh-cavern-door-checkpoint.json");
        GameState saved = GameSession.Load(path);
        string directory = GameDirectory();
        using var world = new GameWorld(new GameSession(saved), directory);
        world.EnsureField();
        Assert.Equal(2, world.Events.Scene.NativeDoorDriverVersion);
        Assert.Null(world.Events.Scene.NativeFieldInitialization);
        Assert.Null(world.Events.Actors[9].NativeSetLineReceipt);
        JsonElement newCapture = world.Events.Capture();
        Assert.True(JsonElement.DeepEquals(NormalizeAbsentMediaCollections(saved.EventState!.Value), NormalizeAbsentMediaCollections(newCapture)));
        var independentSession = new GameSession(GameSession.Load(path) with { EventState = newCapture });
        using var independent = new GameWorld(independentSession, directory);
        independent.EnsureField();
        Assert.True(JsonElement.DeepEquals(newCapture, independent.Events.Capture()));
        var wait = new PlayerAction(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), independent.Apply(wait));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), independent.Events.Capture()));
        Assert.Null(independent.Events.Scene.NativeFieldInitialization);
        Assert.Null(independent.Events.Actors[9].NativeSetLineReceipt);
    }

    [Fact]
    public void Legacy135_stays_unknown_and_changed_original_door_metadata_refuses_before_restore()
    {
        string directory = GameDirectory();
        using var legacy = new GameWorld(new GameSession(GameSession.Load(Evidence("lava-line-preboundary-checkpoint.json"))), directory);
        legacy.EnsureField();
        Assert.Null(legacy.Events.Scene.NativeDoorDriverVersion);
        JsonElement before = legacy.Events.Capture();
        Assert.Contains("Legacy field135", Assert.Throws<NotSupportedException>(() => legacy.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1))).Message);
        Assert.True(JsonElement.DeepEquals(before, legacy.Events.Capture()));
        Assert.Empty(legacy.Events.ActionLog);

        using var fresh = new GameWorld(new GameSession(GameSession.Load(Evidence("fifth-arrival-checkpoint.json"))), directory);
        fresh.EnsureField();
        Assert.True(fresh.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(fresh.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        before = fresh.Events.Capture();
        foreach (string invalid in new[] { "operation", "height", "flags", "legacy", "wrong-owner" })
        {
            JsonNode malformed = JsonNode.Parse(before.GetRawText())!;
            switch (invalid)
            {
                case "operation":
                    malformed["Actors"]![9]!["LineOperation"] = 184;
                    break;
                case "height":
                    malformed["Actors"]![9]!["LineStart"]!["Z"] = 0;
                    break;
                case "flags":
                    malformed["Scene"]!["NativeDoorPendingEvents"]!["9"] = 32;
                    break;
                case "legacy":
                    malformed["Actors"]![9]!["NativeLineContactVersion"] = 1;
                    break;
                case "wrong-owner":
                    malformed["Scene"]!["NativeDoorPendingEvents"]!["12"] = 1;
                    break;
            }
            Assert.Throws<InvalidDataException>(() => fresh.Events.Restore(JsonSerializer.SerializeToElement(malformed)));
            Assert.True(JsonElement.DeepEquals(before, fresh.Events.Capture()));
        }
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        return directory;
    }

    private static JsonElement NormalizeAbsentMediaCollections(JsonElement snapshot)
    {
        JsonNode normalized = JsonNode.Parse(snapshot.GetRawText())!;
        JsonObject scene = normalized["Scene"]!.AsObject();
        foreach (string property in new[] { "NativeFieldSoundRequests", "NativeParticleRequests" })
        {
            if (scene[property] is JsonArray { Count: 0 })
            {
                scene.Remove(property);
            }
        }
        if (scene["NativeParticleSlots"] is JsonObject { Count: 0 })
        {
            scene.Remove("NativeParticleSlots");
        }
        return JsonSerializer.SerializeToElement(normalized);
    }

    private static string Evidence(string suffix)
    {
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        return Path.Combine(repository!.FullName, "../fixtures", "player-dollet-cavern-" + suffix);
    }
}
