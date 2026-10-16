using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDescriptorPoolTests
{
    [Fact]
    public void ReusingAPhysicalPacketRetainsTheUnwrittenPayloadWord()
    {
        NativeBattleDescriptorState state = NativeBattleDescriptorState.Clear();
        (state, int first) = state.Enqueue(105, 128, 0, 6, 0x4321);
        state = state.RemoveFirst();
        (state, int second) = state.Enqueue(113, 128, 0x48AD10);
        Assert.Equal(first, second);
        Assert.Equal((short)6, state.Packets[second].Index);
        Assert.Equal((ushort)0x4321, state.Packets[second].Value);
        Assert.Equal(0x48AD10u, state.Packets[second].Argument);
        Assert.Equal((byte)1, state.Packets[second].Serial);
        state.Validate();
        NativeBattleDescriptorState restored = JsonSerializer.Deserialize<NativeBattleDescriptorState>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void QueueRotationAndSentinelOccupancyUseThePhysicalNodeBank()
    {
        NativeBattleDescriptorState state = NativeBattleDescriptorState.Clear();
        for (int index = 0; index < 29; index++)
        {
            state = state.Enqueue(112, 128, 0, (ushort)index).State;
        }
        Assert.Equal(Enumerable.Range(0, 29), state.Queue);
        Assert.Throws<InvalidOperationException>(() => state.Enqueue(112, 128, 0));
        state = state.Rotate();
        Assert.Equal(Enumerable.Range(1, 28).Append(0), state.Queue);
        state = state.RemoveFirst();
        state = state.Enqueue(106, 128, 0, 3).State;
        Assert.Equal(1, state.Queue[^1]);
        state.Validate();
    }

    [Fact]
    public void AsyncCallbacksShareFourPhysicalRowsWithThePermanentPump()
    {
        NativeBattleDescriptorState state = NativeBattleDescriptorState.Clear();
        (state, int packet) = state.Enqueue(113, 128, 0x487670, 3);
        for (int index = 0; index < 3; index++)
        {
            state = state.AppendTask(NativeBattleDescriptorTaskKind.Callback502F30, packet).State;
        }
        Assert.Equal(0, state.TaskHead);
        Assert.Equal(3, state.TaskTail);
        Assert.Throws<InvalidOperationException>(() => state.AppendTask(NativeBattleDescriptorTaskKind.Callback502F30, packet));
        state.Validate();
    }

    [Fact]
    public void AsyncPriorityBlocksPeersAllowsHigherPriorityAndReleasesOnTheFollowingPumpPass()
    {
        NativeBattleDescriptorState state = NativeBattleDescriptorState.Clear();
        state = state.Enqueue(113, 128, 0x487670, 3).State;
        state = state.Enqueue(106, 128, 0, 4).State;
        state = state.Enqueue(106, 144, 0, 5).State;
        List<short> dispatched = [];
        NativeBattleDescriptorDispatch Dispatch(NativeBattleDescriptorState current, int packet)
        {
            dispatched.Add(current.Packets[packet].Index);
            return current.Packets[packet].Command == 113 ? new(current.AppendTask(NativeBattleDescriptorTaskKind.Callback502F30, packet).State, 8) : new(current, 15);
        }
        NativeBattleDescriptorCompletion Complete(NativeBattleDescriptorState current, int task)
        {
            return new(current.SetPacketState(current.Tasks[task].PacketSlot, 255), true);
        }
        state = state.Advance(Dispatch, Complete);
        Assert.Equal(new short[] { 3, 5 }, dispatched);
        Assert.Equal((byte)1, state.Depth);
        Assert.Equal(2, state.Queue.Length);
        Assert.Equal((byte)255, state.Packets[state.Queue[0]].PriorityState);
        state = state.Advance(Dispatch, Complete);
        Assert.Equal(new short[] { 3, 5, 4 }, dispatched);
        Assert.Empty(state.Queue);
        Assert.Equal((byte)0, state.Depth);
        Assert.Equal((byte)0, state.CurrentCommand);
        state.Validate();
    }
}
