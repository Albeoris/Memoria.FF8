using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleCallbackTests
{
    [Fact]
    public void ExplicitCallbackUsesTheSamePhysicalBankAndExecutesDuringPause()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        (state, int slot) = state.AppendCallback(0x56FAA0);
        Assert.Equal(0, slot);
        Assert.Equal(0x209FAA8u, NativeBattleModuleState.DescriptorAddress);
        Assert.Equal(0x209D078u, NativeBattleModuleState.BankAddress);
        Assert.Equal((ushort)36, NativeBattleModuleState.TaskStride);
        Assert.Equal((ushort)300, NativeBattleModuleState.TaskCapacity);
        Assert.Equal((byte)0, state.PublishedCount);
        int calls = 0;
        NativeBattleModuleStep step = state.Advance(new(1), (current, index, inputs) =>
        {
            calls++;
            Assert.Equal(slot, index);
            Assert.Equal(1u, inputs.GraphicsFlags);
            Assert.Equal(0x56FAA0u, current.Slots[index].Callback);
            return new(current, false, []);
        });
        Assert.Equal(1, calls);
        Assert.Equal((byte)1, step.State.PublishedCount);
    }

    [Fact]
    public void ExplicitCallbackChildrenShareTheCommonSamePassAndReleaseContract()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        (state, _) = state.AppendCallback(0x56FAA0);
        List<int> order = [];
        NativeBattleModuleStep step = state.Advance(new(0), (current, index, inputs) =>
        {
            order.Add(index);
            if (index == 0)
            {
                (current, _) = current.AppendCallback(0x56FAA4);
                current = current.AppendFlash(0, 1, 0, 128);
                return new(current, true, []);
            }
            return new(current, false, []);
        });
        Assert.Equal(new[] { 0, 1 }, order);
        Assert.Equal((byte)2, step.State.PublishedCount);
        Assert.Equal(1, step.State.Head);
        Assert.Equal(2, step.State.Tail);
        Assert.Equal(2, step.State.Slots[1].Next);
        Assert.Equal((ushort)0, step.State.Slots[0].Flags);
        Assert.Single(step.Draws);
        Assert.Equal(NativeBattleModuleDrawKind.Flash, step.Draws[0].Kind);
    }

    [Fact]
    public void ReuseClearsThePriorCallbackIdentityWithoutClearingRetainedPayload()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendFlash(0, 1, 0, 128);
        NativeBattleModuleStep step = state.Advance(new(0));
        step = step.State.Advance(new(0));
        byte[] retained = step.State.Slots[0].Payload.ToArray();
        (state, int slot) = step.State.AppendCallback(0x56FAA0);
        Assert.Equal(0, slot);
        Assert.Equal(NativeBattleModuleKind.None, state.Slots[slot].Kind);
        Assert.Equal(retained, state.Slots[slot].Payload.ToArray());
        state = state.Advance(new(0), (current, _, _) => new(current, true, [])).State;
        (state, slot) = state.Append(NativeBattleModuleKind.CriticalFlash571300);
        Assert.Null(state.Slots[slot].Callback);
        Assert.Equal(retained, state.Slots[slot].Payload.ToArray());
    }

    [Fact]
    public void FullBankPublishesTheNativeByteAndDoesNotGrow()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        for (int slot = 0; slot < 300; slot++)
        {
            (state, _) = state.AppendCallback(0x56FAA0);
        }
        Assert.Throws<InvalidOperationException>(() => state.AppendCallback(0x56FAA0));
        NativeBattleModuleStep step = state.Advance(new(0), (current, _, _) => new(current, false, []));
        Assert.Equal((byte)44, step.State.PublishedCount);
    }

    [Fact]
    public void MissingDispatcherFailsWithoutFabricatingCompletion()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        (state, _) = state.AppendCallback(0x56FAA0);
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => state.Advance(new(0)));
        Assert.Contains("0056FAA0", error.Message);
        Assert.Equal((ushort)1, state.Slots[0].Flags);
        Assert.Equal((byte)0, state.PublishedCount);
    }

    [Fact]
    public void OptionalCallbackPreservesOldSnapshotsAndRetainsExplicitIdentity()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendFlash(0, 1, 0, 128);
        string oldJson = JsonSerializer.Serialize(state).Replace(",\"Callback\":null", "").Replace("\"Callback\":null,", "");
        NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(oldJson)!;
        restored.Validate();
        Assert.Null(restored.Slots[0].Callback);
        (state, _) = state.AppendCallback(0x56FAA0);
        string json = JsonSerializer.Serialize(state);
        restored = JsonSerializer.Deserialize<NativeBattleModuleState>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(0x56FAA0u, restored.Slots[1].Callback);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.AppendCallback(0));
        Assert.Throws<InvalidDataException>(() => (state with { Slots = state.Slots.SetItem(1, state.Slots[1] with { Callback = 0 }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Callback = 0x56FAA0 }) }).Validate());
    }
}
