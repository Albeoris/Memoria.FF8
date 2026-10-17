using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldInputHistoryTests
{
    [Fact]
    public void ActualSamplesRetainThePreviousWordWithoutInventingAnUnobservedPredecessor()
    {
        NativeWorldInputHistory released = NativeWorldInputHistory.Sample(null, 0);
        Assert.Equal((ushort)0, released.CurrentButtons);
        Assert.Null(released.PreviousButtons);
        Assert.Equal(1, released.Samples);
        NativeWorldInputHistory pressed = NativeWorldInputHistory.Sample(released, 0x40);
        Assert.Equal((ushort)0x40, pressed.CurrentButtons);
        Assert.Equal((ushort)0, pressed.PreviousButtons);
        NativeWorldInputHistory held = NativeWorldInputHistory.Sample(pressed, 0x40);
        Assert.Equal(held.CurrentButtons, held.PreviousButtons);
        NativeWorldInputHistory nextRelease = NativeWorldInputHistory.Sample(held, 0);
        Assert.Equal((ushort)0x40, nextRelease.PreviousButtons);
        Assert.Equal(4, nextRelease.Samples);
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeWorldInputHistory.Sample(held, 65536));
        Assert.Throws<InvalidDataException>(() => (held with { Samples = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (released with { PreviousButtons = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (held with { PreviousButtons = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (NativeWorldProcess.FromColdProcess() with { InputHistory = released }).Validate());
        Assert.Throws<InvalidDataException>(() => (NativeWorldProcess.FromColdProcess().AdvanceMainLoopCallbacks(2) with { InputHistory = held }).Validate());
    }

    [Fact]
    public void SharedProcessHistorySurvivesSnapshotsAndWarmNewGameWhileOldSnapshotsStayUnknown()
    {
        GameSession session = GameSession.CreateColdProcess();
        Assert.Null(session.WorldProcess!.InputHistory);
        NativeWorldInputHistory history = NativeWorldInputHistory.Sample(NativeWorldInputHistory.Sample(null, 0), 0x40);
        session.SetWorldProcess(session.WorldProcess.AdvanceMainLoopCallbacks(2) with { InputHistory = history });
        session.StartNewGame();
        Assert.Equal(history, session.WorldProcess.InputHistory);
        NativeWorldProcess restored = JsonSerializer.Deserialize<NativeWorldProcess>(JsonSerializer.Serialize(session.WorldProcess))!;
        Assert.Equal(history, restored.InputHistory);
        JsonObject old = JsonNode.Parse(JsonSerializer.Serialize(restored))!.AsObject();
        old.Remove("InputHistory");
        Assert.Null(JsonSerializer.Deserialize<NativeWorldProcess>(old.ToJsonString())!.InputHistory);
        foreach (string member in new[] { "CurrentButtons", "PreviousButtons", "Samples" })
        {
            JsonObject incomplete = JsonNode.Parse(JsonSerializer.Serialize(history))!.AsObject();
            incomplete.Remove(member);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldInputHistory>(incomplete.ToJsonString()));
        }
    }

    [Fact]
    public void CallbackTransactionPublishesInputAtomicallyAndPreservesOtherProcessOwners()
    {
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess() with { CameraCooldown = 19, CarCameraFollowSpeed = 23 };
        GameState departure = new() { Scene = SceneKind.WorldMap, World = new WorldState(), WorldProcess = process };
        NativeWorldSalaryState staged = NativeWorldSalaryState.Begin(departure).WithWorldInputSample(0).Advance(null, null, null, 0, 0).WithWorldInputSample(0x40).Advance(null, null, null, 0, 0);
        Assert.Null(departure.WorldProcess.InputHistory);
        WorldState finalWorld = departure.World with { Frame = 2 };
        GameState candidate = departure with { World = finalWorld, WorldProcess = process.AdvanceMainLoopCallbacks(2) };
        GameState published = staged.ApplyTo(candidate, finalWorld);
        Assert.Equal((ushort)0x40, published.WorldProcess!.InputHistory!.CurrentButtons);
        Assert.Equal((ushort)0, published.WorldProcess.InputHistory.PreviousButtons);
        Assert.Equal(19, published.WorldProcess.CameraCooldown);
        Assert.Equal(23, published.WorldProcess.CarCameraFollowSpeed);
        Assert.Equal(2, published.WorldProcess.MainLoopCallbacks);
        Assert.Throws<InvalidDataException>(() => staged.ApplyTo(candidate, finalWorld with { Frame = 1 }));
        Assert.Null(candidate.WorldProcess!.InputHistory);
    }
}
