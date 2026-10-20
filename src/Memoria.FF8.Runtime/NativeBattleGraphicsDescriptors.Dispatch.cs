namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDescriptorDispatch(NativeBattleDescriptorState State, byte Result);
public sealed record NativeBattleDescriptorCompletion(NativeBattleDescriptorState State, bool Release);

public sealed partial record NativeBattleDescriptorState
{
    public NativeBattleDescriptorState Advance(Func<NativeBattleDescriptorState, int, NativeBattleDescriptorDispatch> dispatch, Func<NativeBattleDescriptorState, int, NativeBattleDescriptorCompletion> callback, Action<NativeBattleDescriptorState, int>? observe = null)
    {
        Validate();
        NativeBattleDescriptorState state = this;
        int? previous = null;
        for (int? next = TaskHead; next is int slot;)
        {
            observe?.Invoke(state, slot);
            NativeBattleDescriptorCompletion completed;
            if (state.Tasks[slot].Kind == NativeBattleDescriptorTaskKind.Pump500CC0)
            {
                completed = new(state.Pump(dispatch), false);
            }
            else
            {
                completed = callback(state, slot);
            }
            state = completed.State;
            NativeBattleDescriptorTask task = state.Tasks[slot];
            next = task.Next;
            if (completed.Release)
            {
                state = state with { Tasks = state.Tasks.SetItem(slot, task with { Flags = 0 }) };
                if (previous is int retained)
                {
                    state = state with { Tasks = state.Tasks.SetItem(retained, state.Tasks[retained] with { Next = next }) };
                }
                else
                {
                    state = state with { TaskHead = next };
                }
            }
            else
            {
                previous = slot;
            }
        }
        return state with { TaskTail = previous };
    }

    private NativeBattleDescriptorState Pump(Func<NativeBattleDescriptorState, int, NativeBattleDescriptorDispatch> dispatch)
    {
        NativeBattleDescriptorState state = this;
        int count = Queue.Length;
        for (int iteration = 0; iteration < count; iteration++)
        {
            int packetSlot = state.Queue[0];
            NativeBattleDescriptorPacket packet = state.Packets[packetSlot];
            byte result = (byte)(packet.PriorityState & 15);
            if (packet.PriorityState <= state.Priorities[state.Depth])
            {
                state = state.Rotate();
                continue;
            }
            if (result == 0)
            {
                NativeBattleDescriptorDispatch started = dispatch(state, packetSlot);
                if (started.Result > 15 || state.Depth == 15)
                {
                    throw new InvalidDataException("Native descriptor dispatch exceeded its result nibble or priority stack.");
                }
                state = started.State with { Depth = checked((byte)(state.Depth + 1)) };
                result = started.Result;
            }
            if (result == 15)
            {
                if (state.Depth == 0)
                {
                    throw new InvalidDataException("Native descriptor completion underflowed its priority stack.");
                }
                state = state.RemoveFirst() with { Depth = (byte)(state.Depth - 1), CurrentCommand = 0, CurrentSerial = 0 };
            }
            else
            {
                state = state with { Priorities = state.Priorities.SetItem(state.Depth, (byte)(packet.PriorityState & 0xF0)), CurrentCommand = unchecked((byte)packet.Command), CurrentSerial = packet.Serial };
                state = state.SetPacketState(packetSlot, (byte)((packet.PriorityState & 0xF0) | result)).Rotate();
            }
        }
        return state;
    }
}
