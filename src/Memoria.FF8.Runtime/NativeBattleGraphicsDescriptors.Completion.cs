namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleDescriptorState
{
    public static uint CompletionAddress503040(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, 4);
        return checked(0x1D96D18u + (uint)slot * 20 + 14);
    }

    public NativeBattleDescriptorState WriteCompletion501A70(uint address, byte value)
    {
        Validate();
        uint relative = unchecked(address - CompletionAddress503040(0));
        if (relative >= 80 || relative % 20 != 0)
        {
            throw new NotSupportedException("Native501A70 completion requires the actual descriptor task BYTE+0E owner.");
        }
        int slot = checked((int)(relative / 20));
        NativeBattleDescriptorTask task = Tasks[slot];
        return this with { Tasks = Tasks.SetItem(slot, task with { Payload = task.Payload.SetItem(2, value) }) };
    }
}
