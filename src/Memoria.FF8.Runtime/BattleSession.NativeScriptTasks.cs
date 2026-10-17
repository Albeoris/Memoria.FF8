namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void DispatchNativeScriptTasks()
    {
        NativeBattleScriptTasks tasks = nativeScriptState.Tasks.Advance(Dispatch);
        nativeScriptState = nativeScriptState with { Tasks = tasks };

        NativeBattleScriptTasks Dispatch(NativeBattleScriptTasks state, int slot)
        {
            NativeBattleScriptTask task = state.Slots[slot];
            switch (task.ReadDword(0))
            {
                case 0x48ACD0:
                    ushort remaining = task.ReadWord(8);
                    bool paused = remaining != 0;
                    NativeBattleAllocationControl control = nativeScriptState.AllocationControl ?? new(null, false, false);
                    nativeScriptState = nativeScriptState with { AllocationControl = control with { PauseGate = paused } };
                    if (nativeBattleClock is not null)
                    {
                        SetNativeBattleClockActive(!paused);
                    }
                    task = paused ? task.WriteWord(8, (ushort)(remaining - 1)) : task.WriteByte(15, 1);
                    break;
                case 0x48AC60:
                    QueueNativeDescriptorMessage(task.Message ?? throw new NotSupportedException("Native48AC60 lacks its original logical text pointer."), 0);
                    task = task.WriteByte(15, 1);
                    break;
                case 0x48AC90:
                    ushort delay = task.ReadWord(8);
                    if (delay == 0)
                    {
                        QueueNativeDescriptorMessage(task.Message ?? throw new NotSupportedException("Native48AC90 lacks its original logical text pointer."), 0, 240);
                        task = task.WriteByte(15, 1);
                    }
                    task = task.WriteWord(8, unchecked((ushort)(delay - 1)));
                    break;
                case 0x47E030:
                    task = PollNativeOutcome47E030(task);
                    break;
                case 0x48E620:
                    task = PollNativeGuardianScriptTask(task) ? task.WriteByte(15, 1) : task;
                    break;
                default:
                    throw new NotSupportedException($"Native script task callback{task.ReadDword(0):X} requires its original producer/consumer.");
            }
            return state with { Slots = state.Slots.SetItem(slot, task) };
        }
    }

    private int EnqueueNativeGuardianScriptTask(BattleCombatant actor, ushort targetMask, byte gfId)
    {
        (NativeBattleScriptTasks tasks, int slot) = nativeScriptState.Tasks.Allocate(0x48E620);
        NativeBattleScriptTask task = tasks.Slots[slot].WriteByte(12, checked((byte)actor.NativeIndex)).WriteByte(13, 254).WriteByte(14, gfId).WriteWord(10, targetMask);
        nativeScriptState = nativeScriptState with { Tasks = tasks with { Slots = tasks.Slots.SetItem(slot, task) } };
        return slot;
    }

}
