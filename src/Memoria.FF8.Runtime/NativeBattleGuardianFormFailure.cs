namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleDescriptorState
{
    /// <summary>Publishes the ordinary completed-packet result and releases its failed action task. The pump retains priority-stack ownership.</summary>
    internal NativeBattleDescriptorState CompleteFailedGuardianFormAction(int slot)
    {
        Validate();
        if ((uint)slot >= Tasks.Length || Tasks[slot] is not { Kind: NativeBattleDescriptorTaskKind.Action104, Flags: not 0 } task)
        {
            throw new InvalidDataException("Failed GF presentation does not own a live action descriptor task.");
        }
        int? previous = null;
        int? current = TaskHead;
        while (current is int index && index != slot)
        {
            previous = index;
            current = Tasks[index].Next;
        }
        if (current != slot)
        {
            throw new InvalidDataException("Failed GF presentation task is absent from the descriptor chain.");
        }
        NativeBattleDescriptorState completed = SetPacketState(task.PacketSlot, 255);
        completed = completed with { Tasks = completed.Tasks.SetItem(slot, task with { Flags = 0 }) };
        if (previous is int retained)
        {
            completed = completed with { Tasks = completed.Tasks.SetItem(retained, completed.Tasks[retained] with { Next = task.Next }) };
        }
        else
        {
            completed = completed with { TaskHead = task.Next };
        }
        if (TaskTail == slot)
        {
            completed = completed with { TaskTail = previous };
        }
        completed.Validate();
        return completed;
    }
}

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState CancelGuardianFormPresentationDescriptor(NativeBattleGraphicsState graphics)
    {
        if (graphics.ActionGraphics is not { } action)
        {
            return graphics;
        }
        uint headerAddress = checked(0x1D280C4u + 20u * (uint)action.HeaderIndex);
        NativeBattleDescriptorState descriptors = graphics.Descriptors;
        for (int slot = 0; slot < descriptors.Tasks.Length; slot++)
        {
            NativeBattleDescriptorTask task = descriptors.Tasks[slot];
            if (task.Flags != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104 && descriptors.Packets[task.PacketSlot].Argument == headerAddress)
            {
                descriptors = descriptors.CompleteFailedGuardianFormAction(slot);
            }
        }
        return graphics with { Descriptors = descriptors, ActionGraphics = null };
    }
}
