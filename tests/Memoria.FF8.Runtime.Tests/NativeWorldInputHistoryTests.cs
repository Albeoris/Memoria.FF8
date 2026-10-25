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
        Assert.Throws<InvalidDataException>(() => (released with { PreviousButtons = 1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (held with { PreviousButtons = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (NativeWorldProcess.FromColdProcess() with { InputHistory = released }).Validate());
        Assert.Throws<InvalidDataException>(() => (NativeWorldProcess.FromColdProcess().AdvanceMainLoopCallbacks(2) with { InputHistory = held }).Validate());
    }

    [Fact]
    public void SharedProcessHistorySurvivesSnapshotsButNewGameResetsItWhileOldSnapshotsStayUnknown()
    {
        GameSession session = GameSession.CreateColdProcess();
        Assert.Equal(NativeWorldInputHistory.FromColdProcess(), session.WorldProcess!.InputHistory);
        NativeWorldInputHistory history = NativeWorldInputHistory.Sample(NativeWorldInputHistory.Sample(null, 0), 0x40);
        session.SetWorldProcess(session.WorldProcess.AdvanceMainLoopCallbacks(2) with { InputHistory = history });
        Assert.Equal(history, session.WorldProcess.InputHistory);
        NativeWorldProcess restored = JsonSerializer.Deserialize<NativeWorldProcess>(JsonSerializer.Serialize(session.WorldProcess))!;
        Assert.Equal(history, restored.InputHistory);
        session.Restore(session.State with { WorldProcess = restored });
        Assert.Equal(history, session.WorldProcess!.InputHistory);
        session.StartNewGame();
        Assert.Equal(NativeWorldInputHistory.FromColdProcess(), session.WorldProcess!.InputHistory);
        Assert.Equal(0, session.WorldProcess.MainLoopCallbacks);
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
        Assert.Equal(NativeWorldInputHistory.FromColdProcess(), departure.WorldProcess.InputHistory);
        WorldState finalWorld = departure.World with { Frame = 2 };
        GameState candidate = departure with { World = finalWorld, WorldProcess = process.AdvanceMainLoopCallbacks(2) };
        GameState published = staged.ApplyTo(candidate, finalWorld);
        Assert.Equal((ushort)0x40, published.WorldProcess!.InputHistory!.CurrentButtons);
        Assert.Equal((ushort)0, published.WorldProcess.InputHistory.PreviousButtons);
        Assert.Equal(19, published.WorldProcess.CameraCooldown);
        Assert.Equal(23, published.WorldProcess.CarCameraFollowSpeed);
        Assert.Equal(2, published.WorldProcess.MainLoopCallbacks);
        Assert.Throws<InvalidDataException>(() => staged.ApplyTo(candidate, finalWorld with { Frame = 1 }));
        Assert.Equal(NativeWorldInputHistory.FromColdProcess(), candidate.WorldProcess!.InputHistory);
    }

    [Fact]
    public void ColdBuffersEstablishTheFirstButtonEdgeAndHeldNextSample()
    {
        NativeWorldInputHistory cold = NativeWorldInputHistory.FromColdProcess();
        cold.Validate();
        NativeWorldInputHistory restored = JsonSerializer.Deserialize<NativeWorldInputHistory>(JsonSerializer.Serialize(cold))!;
        Assert.Equal(cold, restored);
        NativeWorldInputHistory pressed = NativeWorldInputHistory.Sample(restored, 0x8000);
        pressed.Validate();
        Assert.Equal(1, pressed.Samples);
        Assert.Equal((ushort)0, pressed.PreviousButtons);
        Assert.Equal(0x8000, pressed.CurrentButtons & (pressed.CurrentButtons ^ pressed.PreviousButtons!.Value));
        NativeWorldInputHistory held = NativeWorldInputHistory.Sample(pressed, 0x8000);
        held.Validate();
        Assert.Equal(0, held.CurrentButtons & (held.CurrentButtons ^ held.PreviousButtons!.Value));
        Assert.Throws<InvalidDataException>(() => (cold with { CurrentButtons = 0x8000 }).Validate());
        Assert.Throws<InvalidDataException>(() => (cold with { PreviousButtons = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (cold with { PreviousButtons = 1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (cold with { Samples = -1 }).Validate());
    }

    [Theory]
    [InlineData(0, 0x8000, 0x8000, 0x8000)]
    [InlineData(0x8000, 0, 0x8000, 0)]
    public void ReloadUsesRetainedSlotZeroAfterEitherBufferParity(int slotZeroButtons, int lastButtons, int nextButtons, int expectedEdge)
    {
        NativeWorldInputHistory odd = NativeWorldInputHistory.Sample(NativeWorldInputHistory.Sample(NativeWorldInputHistory.Sample(NativeWorldInputHistory.FromColdProcess(), 0), (uint)slotZeroButtons), (uint)lastButtons);
        Assert.Equal((byte)1, odd.BufferIndex);
        Assert.Equal((ushort)slotZeroButtons, odd.PreviousButtonsAfterWorldLoad);
        NativeWorldInputHistory loadedOdd = NativeWorldInputHistory.SampleAfterWorldLoad(odd, (uint)nextButtons);
        loadedOdd.Validate();
        Assert.Equal((byte)1, loadedOdd.BufferIndex);
        Assert.Equal((ushort)slotZeroButtons, loadedOdd.PreviousButtons);
        Assert.Equal(expectedEdge, loadedOdd.CurrentButtons & (loadedOdd.CurrentButtons ^ loadedOdd.PreviousButtons!.Value));
        NativeWorldInputHistory even = NativeWorldInputHistory.Sample(odd, (uint)slotZeroButtons);
        Assert.Equal((byte)0, even.BufferIndex);
        Assert.Equal((ushort)slotZeroButtons, even.PreviousButtonsAfterWorldLoad);
        NativeWorldInputHistory loadedEven = NativeWorldInputHistory.SampleAfterWorldLoad(even, (uint)nextButtons);
        loadedEven.Validate();
        Assert.Equal(loadedOdd.PreviousButtons, loadedEven.PreviousButtons);
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess().AdvanceMainLoopCallbacks(odd.Samples) with { InputHistory = odd };
        GameState departure = new() { World = new WorldState(), WorldProcess = process };
        NativeWorldSalaryState staged = NativeWorldSalaryState.Begin(departure).WithWorldInputSample((uint)nextButtons);
        Assert.Equal(loadedOdd, staged.InputHistory);
    }

    [Fact]
    public void LegacyUnknownParityRemainsUnknownUntilTheActualLoaderResetsIt()
    {
        NativeWorldInputHistory legacy = new(0x8000, 0, 3);
        legacy.Validate();
        Assert.Null(legacy.BufferIndex);
        Assert.Null(legacy.PreviousButtonsAfterWorldLoad);
        Assert.Null(NativeWorldInputHistory.Sample(legacy, 0).BufferIndex);
        NativeWorldInputHistory first = NativeWorldInputHistory.SampleAfterWorldLoad(legacy, 0x8000);
        first.Validate();
        Assert.Null(first.PreviousButtons);
        Assert.Equal((byte)1, first.BufferIndex);
        NativeWorldInputHistory second = NativeWorldInputHistory.Sample(first, 0x8000);
        second.Validate();
        Assert.Equal((byte)0, second.BufferIndex);
        Assert.Equal((ushort)0x8000, second.PreviousButtons);
        Assert.Throws<InvalidDataException>(() => (first with { BufferIndex = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (first with { BufferIndex = null }).Validate());
        NativeWorldInputHistory restored = JsonSerializer.Deserialize<NativeWorldInputHistory>(JsonSerializer.Serialize(first))!;
        Assert.Equal(first, restored);
    }
}
