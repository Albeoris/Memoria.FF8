using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SoundStopContinuationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Actual_cavern_arrival_stops_voice_one_and_restores_full_vm_and_pixels()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        string source = TestResources.FixturePath("App.Tests.SoundStopContinuationTests.1");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal((129, 0L), (world.Events.FieldId, world.Events.Frame));
        GlobalSoundRequest priorVoice = world.Events.Scene.GlobalSoundLatestAttempts[1];
        int priorRequests = world.Events.Scene.GlobalSoundRequests.Count;
        var wait = new PlayerAction(PlayerActionKind.Wait);
        Assert.True(world.Apply(wait).Accepted);
        Assert.Null(world.Events.LastFault);
        NativeSoundStop stopped = Assert.IsType<NativeSoundStop>(world.Events.Scene.SoundStop);
        Assert.Equal((1, 129, 11, 62, 460, 1L), (stopped.Version, stopped.FieldId, stopped.ObjectId, stopped.ScriptId, stopped.Operation, stopped.Frame));
        Assert.Equal((1u, 1u, 0, 0u, GlobalSoundBackendStatus.Muted), (stopped.RawChannelMask, stopped.ChannelMask, stopped.StopMode, stopped.PollResult, stopped.BackendStatus));
        Assert.Equal(new[] { 1 }, stopped.Voices);
        Assert.Equal(priorVoice, world.Events.Scene.GlobalSoundLatestAttempts[1]);
        Assert.Equal(priorRequests, world.Events.Scene.GlobalSoundRequests.Count);
        JsonElement stoppedState = world.Events.Capture();
        JsonElement fiber = Assert.Single(stoppedState.GetProperty("Fibers").EnumerateArray(), fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 62);
        Assert.True(fiber.GetProperty("Position").GetInt32() > 460);

        GameState copy = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State with { EventState = stoppedState }, JsonOptions), JsonOptions)!;
        Assert.NotSame(session.State.Variables, copy.Variables);
        using var replay = new GameWorld(new GameSession(copy), directory);
        replay.EnsureField();
        Assert.True(JsonElement.DeepEquals(stoppedState, replay.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
        Assert.Equal(world.Apply(wait), replay.Apply(wait));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));

        JsonElement before = world.Events.Capture();
        foreach (string invalid in new[] { "mask", "voices", "poll", "operation" })
        {
            JsonNode malformed = JsonNode.Parse(before.GetRawText())!;
            JsonNode stop = malformed["Scene"]!["SoundStop"]!;
            switch (invalid)
            {
                case "mask":
                    stop["ChannelMask"] = 0;
                    break;
                case "voices":
                    stop["Voices"]![0] = 2;
                    break;
                case "poll":
                    stop["PollResult"] = 1;
                    break;
                case "operation":
                    stop["Operation"] = 459;
                    break;
            }
            Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(malformed)));
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        }
    }
}
