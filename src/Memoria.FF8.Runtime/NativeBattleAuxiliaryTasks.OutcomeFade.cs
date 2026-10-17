using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public NativeBattleAuxiliaryState AppendOutcomeFade501E10(short duration)
    {
        Validate();
        int slot = Enumerable.Range(0, Slots.Length).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            return this;
        }
        byte[] payload = Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, 0);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), duration);
        payload[4] = 128;
        payload[5] = 0;
        NativeBattleAuxiliaryTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = NativeBattleAuxiliaryKind.OutcomeFade501D10, Payload = payload.ToImmutableArray(), UnknownByteMask = Slots[slot].UnknownByteMask & ~0x3Fu };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot };
    }

    private static (NativeBattleAuxiliaryTask Task, bool Completed) AdvanceOutcomeFade501D10(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        short elapsed = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan());
        short duration = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(2));
        byte start = task.Payload[4];
        byte target = task.Payload[5];
        if (start != 128 || target != 0)
        {
            throw new NotSupportedException("Native outcome fade requires its actual501E10 start/target producer.");
        }
        int weight = (elapsed << 12) / duration;
        byte fade = unchecked((byte)(start + ((target - start) * weight >> 12)));
        bool complete = elapsed >= duration;
        (inputs?.ApplyScreenFade501D10 ?? throw new NotSupportedException("Native501D10 requires its screen packet and retained global BYTE owners."))(fade, complete);
        byte[] payload = task.Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, unchecked((short)(elapsed + 1)));
        return (task with { Payload = payload.ToImmutableArray() }, complete);
    }
}
