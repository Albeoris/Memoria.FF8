using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleEffectsTests
{
    [Fact]
    public void CriticalFlashDrawsOneWhiteFrameAndReleasesOnTheFollowingCallback()
    {
        NativeBattleModuleState initial = NativeBattleModuleState.Clear().AppendFlash(0, 1, 0, 255);
        NativeBattleModuleStep first = initial.Advance(new(0));
        Assert.Equal(new NativeBattleModuleDraw(NativeBattleModuleDrawKind.Flash, 255), Assert.Single(first.Draws));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(first.State.Slots[0].Payload.AsSpan()));
        NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(first.State))!;
        restored.Validate();
        NativeBattleModuleStep second = restored.Advance(new(0));
        Assert.Empty(second.Draws);
        Assert.Null(second.State.Head);
        Assert.Null(second.State.Tail);
        Assert.Equal((ushort)0, second.State.Slots[0].Flags);
        Assert.Equal(JsonSerializer.Serialize(first.State.Advance(new(0))), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void FlashUsesOriginalIntegerPhaseDivisionAndBitOnePauseBeforeIncrementOrDraw()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendFlash(3, 1, 2, 100);
        NativeBattleModuleStep paused = state.Advance(new(1));
        Assert.Equal((byte)1, paused.State.PublishedCount);
        Assert.Equal(state.Slots, paused.State.Slots);
        Assert.Empty(paused.Draws);
        foreach (byte expected in new byte[] { 0, 33, 66, 100, 100, 50 })
        {
            NativeBattleModuleStep step = state.Advance(new(0x100));
            Assert.Equal(expected, Assert.Single(step.Draws).Intensity);
            state = step.State;
        }
        Assert.Empty(state.Advance(new(0)).Draws);
    }

    [Fact]
    public void EffectModuleUsesThreeHundredPhysicalSlotsAndRetainsReleasedPayload()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        for (int index = 0; index < 300; index++)
        {
            state = state.AppendFlash(0, 1, 0, (short)index);
        }
        Assert.Throws<InvalidOperationException>(() => state.AppendFlash(0, 1, 0, 255));
        NativeBattleModuleStep first = state.Advance(new(0));
        Assert.Equal(300, first.Draws.Length);
        Assert.Equal((byte)44, first.State.PublishedCount);
        NativeBattleModuleState released = first.State.Advance(new(0)).State;
        Assert.Null(released.Head);
        Assert.Null(released.Tail);
        Assert.All(released.Slots, task => Assert.Equal((ushort)0, task.Flags));
        (NativeBattleModuleState reused, int slot) = released.Append(NativeBattleModuleKind.CriticalFlash571300);
        Assert.Equal(0, slot);
        Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(reused.Slots[slot].Payload.AsSpan()));
    }
}
