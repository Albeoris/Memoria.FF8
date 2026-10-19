using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public NativeBattleAuxiliaryState AppendOutcomeFade501E10(short duration) => AppendScreenFade501D10(duration, 128, 0);

    public NativeBattleAuxiliaryState AppendOpeningFade501CE0(short duration) => AppendScreenFade501D10(duration, 0, 128);

    private NativeBattleAuxiliaryState AppendScreenFade501D10(short duration, byte start, byte target, NativeBattleAuxiliaryKind kind = NativeBattleAuxiliaryKind.OutcomeFade501D10, short elapsed = 0)
    {
        Validate();
        int slot = Enumerable.Range(0, Slots.Length).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            return this;
        }
        byte[] payload = Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, elapsed);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), duration);
        payload[4] = start;
        payload[5] = target;
        NativeBattleAuxiliaryTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = kind, Payload = payload.ToImmutableArray(), UnknownByteMask = Slots[slot].UnknownByteMask & ~0x3Fu };
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
        if ((start != 128 || target != 0) && (start != 0 || target != 128))
        {
            throw new NotSupportedException("Native screen fade requires its actual501E10/501CE0 start/target producer.");
        }
        int weight = (elapsed << 12) / duration;
        byte fade = unchecked((byte)(start + ((target - start) * weight >> 12)));
        bool complete = elapsed >= duration;
        (inputs?.ApplyScreenFade501D10 ?? throw new NotSupportedException("Native501D10 requires its screen packet and retained global BYTE owners."))(fade, complete && target == 0);
        if (target != 0 && elapsed == 5)
        {
            (inputs?.InitializeTimerDisplay47D910 ?? throw new NotSupportedException("Native501D10 requires its original47D910 timer display callback."))();
        }
        byte[] payload = task.Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, unchecked((short)(elapsed + 1)));
        return (task with { Payload = payload.ToImmutableArray() }, complete);
    }
}
