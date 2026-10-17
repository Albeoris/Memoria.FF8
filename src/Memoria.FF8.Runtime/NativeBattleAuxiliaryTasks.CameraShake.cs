using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public NativeBattleAuxiliaryState AppendCameraShake50F680(byte start, byte end, byte duration)
    {
        Validate();
        int slot = Enumerable.Range(0, 16).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            return this;
        }
        byte[] payload = Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, start);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), end);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(4), duration);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(6), 0);
        NativeBattleAuxiliaryTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = NativeBattleAuxiliaryKind.CameraShake50F6C0, Payload = payload.ToImmutableArray(), UnknownByteMask = Slots[slot].UnknownByteMask & ~0xFFu };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot };
    }

    private static (NativeBattleAuxiliaryTask Task, bool Release) AdvanceCameraShake50F6C0(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        if ((task.UnknownByteMask & 0xFF) != 0)
        {
            throw new InvalidDataException("Native50F6C0 lacks its written amplitude/duration/counter WORDs.");
        }
        short start = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan());
        short end = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(2));
        short duration = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(4));
        short elapsed = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(6));
        short value = unchecked((short)(start + (unchecked((end - start) * ((elapsed << 12) / duration)) >> 12)));
        if ((elapsed & 1) != 0)
        {
            value = unchecked((short)-value);
        }
        (inputs?.WriteCameraTranslationY ?? throw new NotSupportedException("Native50F6C0 requires its actual camera translation Y owner."))(value);
        short next = unchecked((short)(elapsed + 1));
        byte[] payload = task.Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(6), next);
        return (task with { Payload = payload.ToImmutableArray() }, next >= duration);
    }
}
