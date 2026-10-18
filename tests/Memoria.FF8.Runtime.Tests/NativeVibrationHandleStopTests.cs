using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeVibrationHandleStopTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(-32768)]
    [InlineData(16)]
    public void NonmatchingHandlesPreserveEveryOwner(int handle)
    {
        NativeVibrationProcessState state = Active();
        Assert.Same(state, state.Stop4A2940(handle));
    }

    [Theory]
    [InlineData(32)]
    [InlineData(36)]
    [InlineData(40)]
    [InlineData(44)]
    public void MatchingGenerationIgnoresHandleBitsTwoAndThreeAndClearsOnlyPriority(int handle)
    {
        NativeVibrationProcessState state = Active();
        NativeVibrationProcessState expected = state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Priority = 0 }) };
        NativeVibrationProcessState stopped = state.Stop4A2940(handle);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(stopped));
        Assert.Same(stopped, stopped.Stop4A2940(handle));
    }

    [Fact]
    public void MinusOneUsesTheFullStopAllContract()
    {
        NativeVibrationProcessState state = Active();
        Assert.Equal(JsonSerializer.Serialize(state.StopAll()), JsonSerializer.Serialize(state.Stop4A2940(-1)));
        Assert.All(state.Stop4A2940(-1).Slots, slot => Assert.Equal((byte)0, slot.Priority));
        Assert.True(state.Stop4A2940(-1).Ports[0].Pending);
    }

    private static NativeVibrationProcessState Active()
    {
        NativeVibrationProcessState state = NativeVibrationProcessState.FromColdProcess();
        return state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Priority = 128, Generation = 2 }).SetItem(1, state.Slots[1] with { Priority = 64, Generation = 3 }) };
    }
}
