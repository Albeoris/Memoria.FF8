using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAuxiliaryTests
{
    [Fact]
    public void CameraBlendUsesItsByteCounterSineAndReleasesOnTheFinalCallback()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendCameraBlend(4);
        foreach (ushort expected in new ushort[] { 1567, 2896, 3784, 4096 })
        {
            NativeBattleAuxiliaryStep step = state.Advance();
            Assert.Equal(expected, step.CameraBlend);
            state = JsonSerializer.Deserialize<NativeBattleAuxiliaryState>(JsonSerializer.Serialize(step.State))!;
            state.Validate();
        }
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
        Assert.Equal(4, state.Slots[0].Payload[0]);
        Assert.Equal(4, state.Slots[0].Payload[1]);
        Assert.Equal((ushort)0, state.Slots[0].Flags);
        Assert.Null(state.Advance().CameraBlend);
    }

    [Fact]
    public void AuxiliaryPoolRetainsPhysicalSlotsAndLastCallbackOwnsTheBlendWord()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendCameraBlend(1).AppendCameraBlend(4);
        NativeBattleAuxiliaryStep first = state.Advance();
        Assert.Equal((ushort)1567, first.CameraBlend);
        Assert.Equal(1, first.State.Head);
        Assert.Equal(1, first.State.Tail);
        state = first.State.AppendCameraBlend(2);
        Assert.Equal(0, state.Tail);
        Assert.Equal(0, state.Slots[1].Next);
        NativeBattleAuxiliaryStep second = state.Advance();
        Assert.Equal((ushort)2896, second.CameraBlend);
        Assert.Equal(1, second.State.Head);
        Assert.Equal(0, second.State.Tail);
    }

    [Fact]
    public void AuxiliaryPoolHasSixteenSlotsAndZeroDurationFailsAtTheOriginalCallback()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        for (int index = 0; index < 16; index++)
        {
            state = state.AppendCameraBlend(255);
        }
        Assert.Throws<InvalidOperationException>(() => state.AppendCameraBlend(1));
        state = NativeBattleAuxiliaryState.Clear().AppendCameraBlend(0);
        Assert.Throws<DivideByZeroException>(() => state.Advance());
    }
}
