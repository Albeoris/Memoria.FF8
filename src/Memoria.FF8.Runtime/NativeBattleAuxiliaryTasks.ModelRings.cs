using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public (NativeBattleAuxiliaryState State, bool Allocated) AppendModelRing(NativeBattleAuxiliaryKind kind, int model, uint flags = 0, byte sequence = 0)
    {
        Validate();
        if (kind is not (NativeBattleAuxiliaryKind.ModelSequence505C70 or NativeBattleAuxiliaryKind.ModelStatus50A000 or NativeBattleAuxiliaryKind.ModelResourceFlags50B080) || (uint)model >= 7)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        int slot = Enumerable.Range(0, Slots.Length).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            return (this, false);
        }
        byte[] payload = Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 0x1D972C0u + 156u * (uint)model);
        uint written = 15;
        if (kind == NativeBattleAuxiliaryKind.ModelStatus50A000)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), flags);
            written |= 0xF0;
        }
        else if (kind == NativeBattleAuxiliaryKind.ModelResourceFlags50B080)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), unchecked((ushort)flags));
            written |= 0x30;
        }
        else
        {
            payload[9] = sequence;
            written |= 0x200;
        }
        NativeBattleAuxiliaryTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = kind, Payload = payload.ToImmutableArray(), UnknownByteMask = Slots[slot].UnknownByteMask & ~written };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return (this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot }, true);
    }

    private static void AdvanceModelRing(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan());
        uint offset = unchecked(pointer - 0x1D972C0u);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Native ring callback has no physical source model pointer.");
        }
        uint argument = task.Kind switch { NativeBattleAuxiliaryKind.ModelStatus50A000 => BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(4)), NativeBattleAuxiliaryKind.ModelResourceFlags50B080 => BinaryPrimitives.ReadUInt16LittleEndian(task.Payload.AsSpan().Slice(4)), _ => task.Payload[9] };
        (inputs?.ApplyModelRing ?? throw new NotSupportedException("Native model ring callback requires its current model/sequence/status owners."))(task.Kind, checked((int)(offset / 156)), argument);
    }
}
